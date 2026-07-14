using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MediaColor = System.Windows.Media.Color;

namespace Scratchdeck.Services;

public static class SyntaxHighlightingService
{
    private static readonly XNamespace Namespace = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";

    public static IReadOnlyList<string> Modes { get; } =
    [
        "Plain Text", "C++", "C#", "JSON", "XML", "PowerShell", "Python",
        "JavaScript", "Markdown", "SQL", "Go", "INI"
    ];

    public static bool IsKnownMode(string? mode) =>
        mode is not null && Modes.Contains(mode, StringComparer.Ordinal);

    public static IHighlightingDefinition? GetDefinition(string mode)
    {
        if (mode == "Plain Text")
        {
            return null;
        }

        var palette = Palette.FromApplicationResources();
        var definition = mode switch
        {
            "C++" => CreateCStyle(mode, palette,
                "alignas alignof and and_eq asm atomic_cancel atomic_commit atomic_noexcept auto bitand bitor bool break case catch char char8_t char16_t char32_t class compl concept const consteval constexpr constinit const_cast continue co_await co_return co_yield decltype default delete do double dynamic_cast else enum explicit export extern false float for friend goto if inline int long mutable namespace new noexcept not not_eq nullptr operator or or_eq private protected public reflexpr register reinterpret_cast requires return short signed sizeof static static_assert static_cast struct switch synchronized template this thread_local throw true try typedef typeid typename union unsigned using virtual void volatile wchar_t while xor xor_eq"),
            "C#" => CreateCStyle(mode, palette,
                "abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile while yield"),
            "JavaScript" => CreateCStyle(mode, palette,
                "as async await break case catch class const continue debugger default delete do else export extends false finally for from function get if import in instanceof let new null of return set static super switch this throw true try typeof undefined var void while with yield"),
            "Go" => CreateCStyle(mode, palette,
                "break default func interface select case defer go map struct chan else goto package switch const fallthrough if range type continue for import return var true false nil"),
            "JSON" => CreateJson(mode, palette),
            "XML" => CreateXml(mode, palette),
            "PowerShell" => CreatePowerShell(mode, palette),
            "Python" => CreatePython(mode, palette),
            "Markdown" => CreateMarkdown(mode, palette),
            "SQL" => CreateSql(mode, palette),
            "INI" => CreateIni(mode, palette),
            _ => CreateCStyle(mode, palette, string.Empty)
        };

        using var reader = definition.CreateReader();
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static XDocument CreateBase(string name, Palette palette, XElement ruleSet)
    {
        return new XDocument(
            new XElement(Namespace + "SyntaxDefinition",
                new XAttribute("name", name),
                Color("Comment", palette.Comment),
                Color("String", palette.String),
                Color("Keyword", palette.Keyword, fontWeight: "bold"),
                Color("Number", palette.Number),
                Color("Type", palette.Type),
                ruleSet));
    }

    private static XDocument CreateCStyle(string name, Palette palette, string keywords)
    {
        var rules = RuleSet();
        rules.Add(Span("Comment", "//"));
        rules.Add(Span("Comment", @"/\*", @"\*/", multiline: true));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(Span("String", "'", "'"));
        rules.Add(KeywordRule(keywords));
        rules.Add(Rule("Number", @"\b(0[xX][0-9a-fA-F]+|\d+(\.\d+)?([eE][+-]?\d+)?)\b"));
        rules.Add(Rule("Type", @"\b[A-Z][A-Za-z0-9_]*\b"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreateJson(string name, Palette palette)
    {
        var rules = RuleSet();
        rules.Add(Rule("Type", "\"(\\\\.|[^\"\\\\])*\"(?=\\s*:)"));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(KeywordRule("true false null"));
        rules.Add(Rule("Number", @"-?\b\d+(\.\d+)?([eE][+-]?\d+)?\b"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreateXml(string name, Palette palette)
    {
        var rules = RuleSet();
        rules.Add(Span("Comment", "<!--", "-->", multiline: true));
        rules.Add(Rule("Keyword", @"</?[A-Za-z_][\w:.-]*"));
        rules.Add(Rule("Keyword", @"/?>"));
        rules.Add(Rule("Type", @"\b[A-Za-z_:][\w:.-]*(?=\s*=)"));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(Span("String", "'", "'"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreatePowerShell(string name, Palette palette)
    {
        var rules = RuleSet(ignoreCase: true);
        rules.Add(Span("Comment", "#"));
        rules.Add(Span("Comment", "<#", "#>", multiline: true));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(Span("String", "'", "'"));
        rules.Add(KeywordRule("begin break catch class continue data define do dynamicparam else elseif end exit filter finally for foreach from function if in inline parallel param process return sequence switch throw trap try until using var while workflow true false null"));
        rules.Add(Rule("Type", @"\$[A-Za-z_][\w:]*"));
        rules.Add(Rule("Number", @"\b(0x[0-9a-f]+|\d+(\.\d+)?)\b"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreatePython(string name, Palette palette)
    {
        var rules = RuleSet();
        rules.Add(Span("Comment", "#"));
        rules.Add(Span("String", "\"\"\"", "\"\"\"", multiline: true));
        rules.Add(Span("String", "'''", "'''", multiline: true));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(Span("String", "'", "'"));
        rules.Add(KeywordRule("and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield match case"));
        rules.Add(Rule("Type", @"(?<=\bclass\s)[A-Za-z_]\w*|(?<=\bdef\s)[A-Za-z_]\w*"));
        rules.Add(Rule("Number", @"\b(0[xX][0-9a-fA-F]+|\d+(\.\d+)?)\b"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreateMarkdown(string name, Palette palette)
    {
        var rules = RuleSet();
        rules.Add(Rule("Keyword", @"^\s{0,3}#{1,6}\s+.*$"));
        rules.Add(Rule("Type", @"(?:\*\*(?=\S).+?(?<=\S)\*\*|__(?=\S).+?(?<=\S)__)"));
        rules.Add(Rule("String", @"`[^`]+`"));
        rules.Add(Rule("Comment", @"^\s*>.*$"));
        rules.Add(Rule("Number", @"^\s*(\d+\.|[-+*])\s+"));
        rules.Add(Rule("Type", @"!?\[[^\]]*\]\([^\)]+\)"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreateSql(string name, Palette palette)
    {
        var rules = RuleSet(ignoreCase: true);
        rules.Add(Span("Comment", "--"));
        rules.Add(Span("Comment", @"/\*", @"\*/", multiline: true));
        rules.Add(Span("String", "'", "'"));
        rules.Add(Span("Type", @"\[", @"\]"));
        rules.Add(KeywordRule("add all alter and any as asc authorization backup begin between break browse bulk by cascade case check checkpoint close clustered coalesce collate column commit compute constraint contains containstable continue convert create cross current current_date current_time current_timestamp current_user cursor database dbcc deallocate declare default delete deny desc disk distinct distributed double drop dump else end errlvl escape except exec execute exists exit external fetch file fillfactor for foreign freetext freetexttable from full function goto grant group having holdlock identity identity_insert identitycol if in index inner insert intersect into is join key kill left like lineno load merge national nocheck nonclustered not null nullif of off offsets on open opendatasource openquery openrowset openxml option or order outer over percent pivot plan precision primary print proc procedure public raiserror read readtext reconfigure references replication restore restrict return revert revoke right rollback rowcount rowguidcol rule save schema securityaudit select semantickeyphrasetable semanticsimilaritydetail semanticsimilaritytable session_user set setuser shutdown some statistics system_user table tablesample textsize then to top tran transaction trigger truncate try_convert tsequal union unique unpivot update updatetext use user values varying view waitfor when where while with within group writetext true false"));
        rules.Add(Rule("Number", @"\b\d+(\.\d+)?\b"));
        return CreateBase(name, palette, rules);
    }

    private static XDocument CreateIni(string name, Palette palette)
    {
        var rules = RuleSet();
        rules.Add(Span("Comment", ";"));
        rules.Add(Span("Comment", "#"));
        rules.Add(Rule("Keyword", @"^\s*\[[^\]]+\]"));
        rules.Add(Rule("Type", @"^[^=;#\r\n]+(?=\s*=)"));
        rules.Add(Span("String", "\"", "\""));
        rules.Add(Rule("Number", @"\b\d+(\.\d+)?\b"));
        return CreateBase(name, palette, rules);
    }

    private static XElement RuleSet(bool ignoreCase = false)
    {
        var element = new XElement(Namespace + "RuleSet");
        if (ignoreCase)
        {
            element.Add(new XAttribute("ignoreCase", "true"));
        }
        return element;
    }

    private static XElement Color(string name, MediaColor color, string? fontWeight = null)
    {
        var element = new XElement(Namespace + "Color",
            new XAttribute("name", name),
            new XAttribute("foreground", color.ToString()));
        if (fontWeight is not null)
        {
            element.Add(new XAttribute("fontWeight", fontWeight));
        }
        return element;
    }

    private static XElement Span(
        string color,
        string begin,
        string? end = null,
        bool multiline = false)
    {
        var element = new XElement(Namespace + "Span", new XAttribute("color", color));
        if (multiline)
        {
            element.Add(new XAttribute("multiline", "true"));
        }
        element.Add(new XElement(Namespace + "Begin", begin));
        if (end is not null)
        {
            element.Add(new XElement(Namespace + "End", end));
        }
        return element;
    }

    private static XElement KeywordRule(string words)
    {
        return new XElement(Namespace + "Keywords",
            new XAttribute("color", "Keyword"),
            words.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => new XElement(Namespace + "Word", word)));
    }

    private static XElement Rule(string color, string expression)
    {
        return new XElement(Namespace + "Rule", new XAttribute("color", color), expression);
    }

    private readonly record struct Palette(MediaColor Comment, MediaColor String, MediaColor Keyword, MediaColor Number, MediaColor Type)
    {
        public static Palette FromApplicationResources() => new(
            GetColor("SyntaxCommentBrush", Colors.Gray),
            GetColor("SyntaxStringBrush", Colors.Gold),
            GetColor("SyntaxKeywordBrush", Colors.Cyan),
            GetColor("SyntaxNumberBrush", Colors.Magenta),
            GetColor("SyntaxTypeBrush", Colors.DeepSkyBlue));

        private static MediaColor GetColor(string resourceKey, MediaColor fallback) =>
            System.Windows.Application.Current?.TryFindResource(resourceKey) is SolidColorBrush brush
                ? brush.Color
                : fallback;
    }
}
