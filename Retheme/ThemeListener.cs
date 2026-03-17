using Retheme.Grammars;

namespace Retheme
{
    [CLSCompliant(false)]
    public class ThemeListener : css3ParserBaseListener
    {
        public Dictionary<string, ThemeData> Themes { get; } = [];
        private ThemeData? _currentTheme;

        // Triggered when a selector like .theme-deepseakelp is found
        public override void EnterClassName( css3Parser.ClassNameContext context)
        {
            var className = context.GetText().TrimStart('.');
            if (className.StartsWith("theme-"))
            {
                if (!Themes.TryGetValue(className, out _currentTheme))
                {
                    _currentTheme = new ThemeData { ClassName = className };
                    Themes.Add(className, _currentTheme);
                }
            }
        }


        // Use the Labeled Alternative name from your .g4 file
        public override void EnterKnownDeclaration(css3Parser.KnownDeclarationContext context)
        {
            if (_currentTheme == null) return;

            // In your grammar: declaration : property_ ':' ws expr prio?
            // property_ handles 'Variable' tokens (your --vars)
            var propNode = context.property_();
            var propText = propNode?.GetText()?.Trim();

            if (propText != null && propText.StartsWith("--"))
            {
                // expr is the rule for the value
                var valueText = context.expr()?.GetText()?.Trim();

                if (valueText != null)
                {
                    _currentTheme.Variables[propText] = valueText;
                }
            }
        }
    }

    public class ThemeData
    {
        public string? ClassName { get; set; }
        public Dictionary<string, string> Variables { get; set; } = [];
    }
}
