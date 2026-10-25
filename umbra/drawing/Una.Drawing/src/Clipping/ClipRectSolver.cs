namespace Una.Drawing.Clipping;

public static class ClipRectSolver
{
    public static RectSolverResult Solve(ClipRect area)
    {
        List<ClipRect> nativeRects = ClipRectProvider.FindClipRectsIntersectingWith(area);

        if (nativeRects.Count == 0) {
            return new() {
                SolvedRects  = [area],
                NativeRects  = nativeRects,
                IsOverlapped = false,
            };
        }

        // --- Fast path 2: Check if a single native window completely covers the area ---
        // Note: The original code used provider.FindOverlappingRect(area).
        // If that provider method is faster (e.g., uses spatial indexing), keep it.
        // Otherwise, we can check it here from the already retrieved list.
        foreach (var nativeRect in nativeRects) {
            if (nativeRect.Overlaps(area)) {
                return new() {
                    SolvedRects  = [], // Area is fully clipped
                    NativeRects  = nativeRects,
                    IsOverlapped = true, // Indicate full overlap
                };
            }
        }

        // --- Geometric Subtraction Approach ---
        // Start with the initial area as the only visible rectangle.
        List<ClipRect> visibleRects = [area];
        // Temporary list to hold results from the subtraction step for the next iteration.
        // Initialize capacity to avoid reallocations during the loop if possible.
        List<ClipRect> nextVisibleRects = new(visibleRects.Count * 2); // Heuristic capacity

        foreach (var clipRect in nativeRects) {
            // If a clipRect doesn't even intersect the original area, skip it.
            // (This check might be redundant if provider.FindClipRectsIntersectingWith is accurate)
            if (!area.IntersectsWith(clipRect)) continue;

            // For each currently visible rectangle, subtract the current clipRect
            foreach (var visible in visibleRects) {
                // Subtract returns 0-4 smaller rectangles representing the parts of 'visible'
                // not covered by 'clipRect'.
                var resultingRects = visible.Subtract(clipRect);
                // Add the valid resulting pieces to the list for the next iteration.
                nextVisibleRects.AddRange(resultingRects);
            }

            // The results of the subtraction become the input for the next clipping rectangle
            // Swap lists to avoid allocation: clear visibleRects and swap references
            visibleRects.Clear();
            (visibleRects, nextVisibleRects) = (nextVisibleRects, visibleRects); // Swap references
            // nextVisibleRects is now empty and ready for the next iteration
        }

        // --- Optional: Merge adjacent/overlapping rectangles ---
        // Merging can reduce the number of rectangles sent to ImGui, which might be beneficial.
        // The quality/necessity of merging depends on how many rectangles are generated
        // and how ImGui handles many clip rects.
        List<ClipRect> mergedRects = MergeRectanglesIterative(visibleRects);


        return new() {
            SolvedRects  = mergedRects,
            NativeRects  = nativeRects,
            IsOverlapped = mergedRects.Count == 0, // Overlapped if no area remains visible
        };
    }

    // --- Optimized Merging ---
    // Iteratively merges rectangles until no more merges can be performed.
    private static List<ClipRect> MergeRectanglesIterative(List<ClipRect> rectangles)
    {
        if (rectangles.Count < 2) return rectangles;

        // Use a list that we can modify efficiently
        List<ClipRect> currentRects = new(rectangles); // Create a mutable copy
        bool       merged;

        do {
            merged = false;
            int count = currentRects.Count;
            // Use index-based loops for safe removal/modification
            for (int i = count - 1; i >= 0; i--) {
                // Avoid merging with self or already processed pairs in this pass
                for (int j = i - 1; j >= 0; j--) {
                    ClipRect rect1 = currentRects[i];
                    ClipRect rect2 = currentRects[j];

                    // Check if they can be merged into a single larger rectangle
                    if (TryMerge(rect1, rect2, out ClipRect combined)) {
                        // Replace rect j with the combined one
                        currentRects[j] = combined;
                        // Remove rect i (since it's now part of combined)
                        // Remove higher index first to not mess up lower index
                        currentRects.RemoveAt(i);

                        merged = true;
                        // Break the inner loop and restart the check for the new combined[j]
                        // from the elements before it. Alternatively, restart the outer loop.
                        // For simplicity, we just let the outer loop continue, and the `do-while`
                        // handles iterating until stability. This isn't the most optimal O(N^2)
                        // merge, but it's robust and simpler than sweep-line.
                        goto nextOuterIteration; // Jump to the next i
                    }
                }

                nextOuterIteration: ;
            }
        } while (merged); // Repeat if any merges happened in the pass

        // Optional: Final cleanup for potential duplicates if TryMerge isn't perfect
        // If TryMerge guarantees non-duplicates, this can be skipped.
        // return currentRects.Distinct().ToList(); // Requires Linq, adds overhead
        return currentRects;
    }

    // Tries to merge two rectangles if they are adjacent and form a simple larger rectangle.
    // Returns true if merge happened, false otherwise. 'combined' contains the result.
    private static bool TryMerge(ClipRect r1, ClipRect r2, out ClipRect combined)
    {
        const float epsilon = 1e-4f; // Tolerance for floating point comparisons

        // Check for horizontal adjacency and same height
        if (Math.Abs(r1.Y1 - r2.Y1) < epsilon && Math.Abs(r1.Y2 - r2.Y2) < epsilon) {
            // r1 is left of r2
            if (Math.Abs(r1.X2 - r2.X1) < epsilon) {
                combined = new ClipRect(r1.X1, r1.Y1, r2.X2, r1.Y2);
                return true;
            }

            // r2 is left of r1
            if (Math.Abs(r2.X2 - r1.X1) < epsilon) {
                combined = new ClipRect(r2.X1, r1.Y1, r1.X2, r1.Y2);
                return true;
            }
        }

        // Check for vertical adjacency and same width
        if (Math.Abs(r1.X1 - r2.X1) < epsilon && Math.Abs(r1.X2 - r2.X2) < epsilon) {
            // r1 is above r2
            if (Math.Abs(r1.Y2 - r2.Y1) < epsilon) {
                combined = new ClipRect(r1.X1, r1.Y1, r1.X2, r2.Y2);
                return true;
            }

            // r2 is above r1
            if (Math.Abs(r2.Y2 - r1.Y1) < epsilon) {
                combined = new ClipRect(r1.X1, r2.Y1, r1.X2, r1.Y2);
                return true;
            }
        }

        // Check for overlap containment (simplification: if one contains the other, merge is just the larger one)
        // This helps simplify cases where subtraction might create overlapping results (though it shouldn't ideally)
        if (r1.Overlaps(r2)) {
            combined = r1;
            return true;
        }

        if (r2.Overlaps(r1)) {
            combined = r2;
            return true;
        }


        combined = default;
        return false;
    }
}

public struct RectSolverResult
{
    public List<ClipRect> SolvedRects;
    public List<ClipRect> NativeRects;
    public bool       IsOverlapped; // True if the original area is completely covered
}
