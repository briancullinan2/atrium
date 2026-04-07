using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Retheme.Grammars;

namespace Retheme;

public static class Theme
{
    public static List<ThemeData> ExtractThemes(string cssInput)
    {
        // AntlrInputStream is the 'standard' way in the 4.6.6 runtime
        AntlrInputStream stream = new(cssInput);

        // The rest of the logic remains the same
        css3Lexer lexer = new(stream);
        ITokenStream tokens = new CommonTokenStream(lexer);
        css3Parser parser = new(tokens);

        var tree = parser.stylesheet();
        var listener = new ThemeListener();
        ParseTreeWalker.Default.Walk(listener, tree);

        return [.. listener.Themes.Values];
    }
}
