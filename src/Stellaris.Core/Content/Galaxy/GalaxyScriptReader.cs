using System.Text;

namespace Stellaris.Core.Content.Galaxy;

public enum GalaxySyntaxKind { Scalar, Block, TaggedBlock, Conditional }
public sealed record GalaxySourceLocation(string Source, int Line, int Column);

/// <summary>Source AST; условные шаблоны и теги не исполняются при чтении.</summary>
public sealed class GalaxySyntaxValue
{
    public GalaxySyntaxKind Kind { get; }
    public string? Text { get; }
    public bool Quoted { get; }
    public GalaxySourceLocation Location { get; }
    public IReadOnlyList<GalaxySyntaxEntry> Entries { get; }
    internal GalaxySyntaxValue(GalaxySyntaxKind kind, string? text, bool quoted,
        GalaxySourceLocation location, IEnumerable<GalaxySyntaxEntry>? entries = null)
    {
        Kind = kind; Text = text; Quoted = quoted; Location = location;
        Entries = Array.AsReadOnly((entries ?? []).ToArray());
    }
}
public sealed record GalaxySyntaxEntry(string? Key, string? Operator, GalaxySyntaxValue Value,
    GalaxySourceLocation Location);

public static class GalaxyScriptReader
{
    public static GalaxySyntaxValue Parse(string text, string source = "<memory>")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(source);
        return new Reader(text, source).Parse();
    }

    private sealed record Token(string Text, string Kind, GalaxySourceLocation Location);
    private sealed class Reader
    {
        private readonly string text;
        private readonly string source;
        private int position, line = 1, column = 1;
        private Token current;
        public Reader(string text, string source)
        {
            this.text = text; this.source = source;
            if (text.StartsWith('\uFEFF')) position++;
            current = Next();
        }
        public GalaxySyntaxValue Parse() => Block("end", new(source, 1, 1), 0);
        private GalaxySyntaxValue Block(string close, GalaxySourceLocation start, int depth)
        {
            if (depth > 256) throw Error(start, "глубина AST превышает 256");
            var entries = new List<GalaxySyntaxEntry>();
            while (current.Kind != close)
            {
                if (current.Kind is "end" or "}" or "]") throw Error(current.Location, $"ожидалось {close}");
                var first = current;
                if (first.Kind is "{" or "conditional")
                {
                    entries.Add(new(null, null, Value(depth), first.Location));
                    continue;
                }
                if (first.Kind is not ("word" or "quoted")) throw Error(first.Location, "ожидалось поле или элемент списка");
                Advance();
                if (current.Kind == "operator")
                {
                    var operation = current.Text; Advance();
                    entries.Add(new(first.Text, operation, Value(depth), first.Location));
                }
                else entries.Add(new(null, null, Scalar(first), first.Location));
            }
            if (close != "end") Advance();
            return new(GalaxySyntaxKind.Block, null, false, start, entries);
        }
        private GalaxySyntaxValue Value(int depth)
        {
            var first = current; Advance();
            if (first.Kind == "{") return Block("}", first.Location, depth + 1);
            if (first.Kind == "conditional")
                return new(GalaxySyntaxKind.Conditional, first.Text, false, first.Location,
                    Block("]", first.Location, depth + 1).Entries);
            if (first.Kind is not ("word" or "quoted")) throw Error(first.Location, "ожидалось значение");
            if (current.Kind == "{")
            {
                Advance();
                return new(GalaxySyntaxKind.TaggedBlock, first.Text, first.Kind == "quoted", first.Location,
                    Block("}", first.Location, depth + 1).Entries);
            }
            return Scalar(first);
        }
        private static GalaxySyntaxValue Scalar(Token token) =>
            new(GalaxySyntaxKind.Scalar, token.Text, token.Kind == "quoted", token.Location);
        private void Advance() => current = Next();
        private Token Next()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(text[position])) { Consume(); continue; }
                if (text[position] != '#') break;
                while (position < text.Length && text[position] is not ('\r' or '\n')) Consume();
            }
            var location = new GalaxySourceLocation(source, line, column);
            if (position == text.Length) return new("", "end", location);
            var c = text[position];
            if (c is '{' or '}' or ']') { Consume(); return new(c.ToString(), c.ToString(), location); }
            if (c == '"')
            {
                Consume(); var result = new StringBuilder();
                while (position < text.Length)
                {
                    c = text[position]; Consume();
                    if (c == '"') return new(result.ToString(), "quoted", location);
                    if (c != '\\') { result.Append(c); continue; }
                    if (position == text.Length) break;
                    c = text[position]; Consume();
                    result.Append(c switch { 'n' => "\n", 'r' => "\r", 't' => "\t", '"' => "\"", '\\' => "\\", _ => "\\" + c });
                }
                throw Error(location, "незавершённая строка");
            }
            var begin = position;
            if (c == '@' && position + 1 < text.Length && text[position + 1] == '[')
            {
                Consume(); Consume(); var nesting = 1;
                while (position < text.Length)
                {
                    c = text[position]; Consume();
                    if (c == '[') nesting++;
                    if (c == ']') nesting--;
                    if (nesting == 0) return new(text[begin..position], "word", location);
                }
                throw Error(location, "незавершённое inline math");
            }
            if (c == '[')
            {
                Consume();
                if (position == text.Length || text[position] != '[') throw Error(location, "неподдержанный одиночный [");
                Consume();
                if (position < text.Length && text[position] == '!') Consume();
                var nameStart = position;
                while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] == '_')) Consume();
                if (position == nameStart || position == text.Length || text[position] != ']') throw Error(location, "неверный conditional header");
                Consume(); return new(text[begin..position], "conditional", location);
            }
            if (c is '=' or '!' or '<' or '>')
            {
                Consume();
                if (position < text.Length && text[position] == '=') Consume();
                var op = text[begin..position];
                if (op == "!") throw Error(location, "ожидалось !=");
                return new(op, "operator", location);
            }
            while (position < text.Length && !char.IsWhiteSpace(text[position]) &&
                text[position] is not ('{' or '}' or '[' or ']' or '#' or '"' or '=' or '!' or '<' or '>')) Consume();
            if (position == begin) throw Error(location, "неподдержанный символ");
            return new(text[begin..position], "word", location);
        }
        private void Consume()
        {
            var c = text[position++];
            if (c == '\r' || c == '\n' && (position < 2 || text[position - 2] != '\r')) { line++; column = 1; }
            else if (c == '\n') column = 1;
            else column++;
        }
        private static FormatException Error(GalaxySourceLocation location, string message) =>
            new($"{location.Source}:{location.Line}:{location.Column}: {message}");
    }
}
