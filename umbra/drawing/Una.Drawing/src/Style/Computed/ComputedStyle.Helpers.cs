namespace Una.Drawing;

public partial struct ComputedStyle
{
    internal bool HasDrawables()
    {
        return BackgroundColor is not null
               || BorderColor is not null
               || StrokeColor is not null
               || BackgroundGradient is not null
               || BackgroundImage is not null
               || OutlineColor is not null
               || TextShadowColor is not null
               || IconId is not null
               || ImageBytes is not null
               || DropShadow != Vector4.Zero
               || (!string.IsNullOrWhiteSpace(UldResource) && UldPartsId.HasValue && UldPartId.HasValue);
    }

    internal CommitResult Commit(ref ComputedStyle previous)
    {
        PaintStyleSnapshot  = PaintStyleSnapshot.Create(ref this);
        LayoutStyleSnapshot = LayoutStyleSnapshot.Create(ref this);

        CommitResult result = CommitResult.None;

        if (!AreStructsEqual(ref LayoutStyleSnapshot, ref previous.LayoutStyleSnapshot)) {
            result |= CommitResult.LayoutUpdated;
        }

        if (!AreStructsEqual(ref PaintStyleSnapshot, ref previous.PaintStyleSnapshot)) {
            result |= CommitResult.PaintUpdated;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool AreStructsEqual<T>(ref readonly T a, ref readonly T b) where T : unmanaged
    {
        return MemoryMarshal
              .AsBytes(new ReadOnlySpan<T>(in a))
              .SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in b)));
    }

    [Flags]
    internal enum CommitResult
    {
        None          = 0,
        LayoutUpdated = 1,
        PaintUpdated  = 2,
    }
}