using System.Text;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Assets;

// Visual AST допускает source «color = hsv { ... }» и сохраняет повторы/anonymous blocks.
// Общий gameplay parser не расширяется этим ограниченным reader.
internal static class GeneratedWorldVisualDefinitions
{
    internal static ScriptBlock Parse(string text, string source)
    {
        if (text.Length > 16 * 1024 * 1024 || Encoding.UTF8.GetByteCount(text) > 16 * 1024 * 1024) throw new InvalidDataException("Visual definitions limit.");
        return new Reader(text, source).Parse();
    }
    private sealed record Token(char Kind, string Value);
    private sealed class Reader(string text, string source)
    {
        private int _position;
        private int _tokens;
        private readonly List<Token?> _lookahead = [];
        private InvalidDataException Error(string detail) => new($"Visual definitions {source}: {detail} (position {_position}).");
        private Token? Read()
        {
            while (_position < text.Length)
            {
                if (char.IsWhiteSpace(text[_position]) || _position == 0 && text[_position] == '\uFEFF') { _position++; continue; }
                if (text[_position] == '#') { while (_position < text.Length && text[_position] is not ('\n' or '\r')) _position++; continue; }
                break;
            }
            if (_position == text.Length) return null;
            if (++_tokens > 250000) throw Error("Token limit");
            var first = text[_position++];
            if (first is '{' or '}') return new(first, "");
            if (first is '=' or '<' or '>' or '!')
            {
                var op = first.ToString(); if (_position < text.Length && text[_position] == '=') { op += '='; _position++; }
                if (op is "!" or "==") throw Error("Operator"); return new('o', op);
            }
            if (first == '"')
            {
                var result = new StringBuilder();
                while (_position < text.Length)
                {
                    var c = text[_position++]; if (c == '"') return new('s', result.ToString());
                    if (char.IsControl(c)) throw Error("Control in quoted string");
                    if (c == '\\')
                    {
                        if (_position == text.Length) throw Error("Escape");
                        c = text[_position++] switch { '"' => '"', '\\' => '\\', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => throw Error("Escape") };
                    }
                    result.Append(c);
                }
                throw Error("Unclosed quote");
            }
            var scalar = new StringBuilder().Append(first);
            while (_position < text.Length && !char.IsWhiteSpace(text[_position]) && !"{}=<>!#\"".Contains(text[_position])) scalar.Append(text[_position++]);
            if (scalar.ToString().Any(char.IsControl)) throw Error("Control in scalar");
            return new('s', scalar.ToString());
        }
        private Token? Peek(int index = 0) { while (_lookahead.Count <= index) _lookahead.Add(Read()); return _lookahead[index]; }
        private Token? Take() { var t = Peek(); _lookahead.RemoveAt(0); return t; }
        internal ScriptBlock Parse() => Entries(0, false);
        private ScriptBlock Entries(int depth, bool nested)
        {
            if (depth > 32) throw Error("Depth limit");
            var entries = new List<ScriptEntry>();
            while (Peek() is { } token)
            {
                if (token.Kind == '}') { if (!nested) throw Error("Extra closing brace"); Take(); return new(entries, source); }
                string key = "", op = "";
                if (token.Kind == 's' && Peek(1)?.Kind == 'o') { key = Take()!.Value; op = Take()!.Value; }
                token = Take() ?? throw Error("Missing value");
                if (token.Kind == 's') entries.Add(new(key, op, token.Value, null, 1, _position, source));
                else if (token.Kind == '{') entries.Add(new(key, op, null, Entries(depth + 1, true), 1, _position, source));
                else throw Error("Expected scalar/block");
            }
            if (nested) throw Error("Unclosed block"); return new(entries, source);
        }
    }
}
