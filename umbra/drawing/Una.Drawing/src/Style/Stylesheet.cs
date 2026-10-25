using System.Text;

namespace Una.Drawing;

public class Stylesheet
{
    internal Dictionary<Rule, Style> Rules = [];

    public Stylesheet(List<StyleDefinition> rules)
    {
        foreach (var rule in rules) AddRule(rule.Query, rule.Style);
    }

    /// <summary>
    /// Adds a style rule matching the given query.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="style"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void AddRule(string query, Style style)
    {
        List<QuerySelector> results = QuerySelectorParser.Parse(query);

        foreach (var qs in results) {
            Rules.Add(new(qs, Rules.Count), style);
        }
    }

    /// <summary>
    /// Imports the rules from another stylesheet into this one.
    /// </summary>
    public void ImportFrom(Stylesheet other)
    {
        foreach (var rule in other.Rules) {
            Rules.Add(rule.Key, rule.Value);
        }
    }

    /// <summary>
    /// <para>
    /// Returns a dictionary of all rules in this stylesheet.
    /// </para>
    /// <para>
    /// Modifying this dictionary will not affect the original stylesheet.
    /// </para>
    /// </summary>
    public Dictionary<string, Style> GetRuleList()
    {
        Dictionary<string, Style> rules = new();

        foreach (var rule in Rules) {
            string key = rule.Key.ToString();
            if (!rules.ContainsKey(key)) {
                rules.Add(key, rule.Value);
            }
        }

        return rules;
    }

    internal class Rule(QuerySelector querySelector, int sourceOrderIndex)
    {
        public readonly int SourceOrderIndex = sourceOrderIndex;

        public Specificity Specificity = Specificity.Calculate(querySelector.ToString());

        public override string ToString()
        {
            return querySelector.ToString();
        }

        public bool Matches(Node node)
        {
            return querySelector.Matches(node);
        }
    }

    internal readonly struct Specificity(int idCount, int classTagCount) : IComparable<Specificity>
    {
        private readonly int _idCount       = idCount;
        private readonly int _classTagCount = classTagCount;

        public int CompareTo(Specificity other)
        {
            return _idCount != other._idCount 
                ? _idCount.CompareTo(other._idCount) 
                : _classTagCount.CompareTo(other._classTagCount);
        }

        public override string ToString() => $"({_idCount},{_classTagCount})";

        public static Specificity Calculate(string selector)
        {
            var idCount       = selector.Split('#').Length - 1;
            var classTagCount = (selector.Split('.').Length - 1) + (selector.Split(':').Length - 1);

            return new Specificity(idCount, classTagCount);
        }
    }

    public readonly struct StyleDefinition(string query, Style style)
    {
        public string Query => query;
        public Style  Style => style;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var rule in Rules) {
            sb.AppendLine(rule.Key.ToString());
            sb.AppendLine("{");
            sb.AppendLine(rule.Value.ToString());
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}