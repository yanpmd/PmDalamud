namespace Una.Drawing;

internal static class QuerySelectorParser
{
    // Cache now stores: query string -> List of LEAF QuerySelectors for that query
    private static readonly Dictionary<string, (List<QuerySelector> roots, List<QuerySelector> leafs)> Cache = [];

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static List<QuerySelector> Parse(string query, bool returnRoots = false)
    {
        // Return empty list immediately for null/whitespace queries
        if (string.IsNullOrWhiteSpace(query)) {
            return [];
        }

        // Check cache first
        if (Cache.TryGetValue(query, out var cachedResult)) {
            return returnRoots ? cachedResult.roots : cachedResult.leafs;
        }

        List<QuerySelectorToken> tokens = QuerySelectorTokenizer.Tokenize(query);

        List<QuerySelector> leafSelectors = [];
        List<QuerySelector> rootSelectors = [];

        if (tokens.Count == 0) {
            Cache[query] = (rootSelectors, leafSelectors);
            return leafSelectors;
        }

        // Initialize the root and the current scope for the first selector chain
        QuerySelector currentRoot  = new();
        QuerySelector currentScope = currentRoot; // 'currentScope' tracks the current leaf being built

        rootSelectors.Add(currentRoot);

        foreach (QuerySelectorToken token in tokens) {
            switch (token.Type) {
                case QuerySelectorTokenType.All:
                    if (currentScope.Identifier != null) {
                        currentScope = currentScope.NestedChild = new() { MatchAll = true, };
                    } else {
                        currentScope.MatchAll = true;
                    }

                    continue; // Token processed
                case QuerySelectorTokenType.Identifier:
                    if (currentScope.MatchAll) {
                        throw new Exception("Unexpected identifier after '*' in selector.");
                    }
                    
                    // If the current scope already has an identifier,
                    // assume a descendant combinator (' ') was intended.
                    if (currentScope.Identifier != null) {
                        // Create a new nested child scope and update currentScope to point to it
                        currentScope = currentScope.NestedChild = new() {
                            Identifier = token.Value, Parent = currentScope
                        };
                    } else {
                        // Assign identifier to the current scope
                        currentScope.Identifier = token.Value;
                    }

                    continue; // Token processed

                case QuerySelectorTokenType.Class:
                    // Add class to the current scope if it's not already there
                    if (!currentScope.ClassList.Contains(token.Value)) {
                        currentScope.ClassList.Add(token.Value);
                    }

                    continue; // Token processed

                case QuerySelectorTokenType.Child: // '>' combinator
                    // Create a new direct child scope and update currentScope to point to it
                    currentScope = currentScope.DirectChild = new() { Parent = currentScope };
                    continue; // Token processed, new scope ready for next part

                case QuerySelectorTokenType.DeepChild: // ' ' combinator
                    // Create a new nested child scope and update currentScope to point to it
                    currentScope = currentScope.NestedChild = new() { Parent = currentScope };
                    continue; // Token processed, new scope ready for next part

                case QuerySelectorTokenType.TagList: // ':tag' custom syntax
                    // Add tag to the current scope
                    currentScope.TagList.Add(token.Value);
                    continue; // Token processed

                case QuerySelectorTokenType.Separator: // ',' separator
                    // A selector chain is complete. Add its leaf node ('currentScope') to our results list.
                    leafSelectors.Add(currentScope);

                    // Start building the next selector chain from scratch
                    currentRoot  = new(); // Create a new root for the next selector chain
                    currentScope = currentRoot;     // Reset scope to the new root
                    rootSelectors.Add(currentRoot); // Add the new root to the list
                    continue;                       // Token processed

                default:
                    // Handle unexpected token types
                    throw new ArgumentOutOfRangeException(
                        $"Invalid token type during parsing: [{token.Type}] with value '{token.Value}'");
            }
        }

        // After the loop finishes, add the leaf node of the very last selector chain
        // 'currentScope' currently points to this leaf node.
        leafSelectors.Add(currentScope);

        // Cache the computed list of leaf selectors before returning
        Cache[query] = (rootSelectors, leafSelectors);

        return returnRoots ? rootSelectors : leafSelectors;
    }

    /// <summary>
    /// Clears the internal cache of parsed selectors.
    /// </summary>
    internal static void Dispose()
    {
        Cache.Clear();
    }
}