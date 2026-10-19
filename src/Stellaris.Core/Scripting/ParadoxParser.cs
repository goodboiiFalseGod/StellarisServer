using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stellaris.Core.Scripting;

public sealed class ScriptBlock
{
    public readonly IReadOnlyList<ScriptEntry> Entries;
    private readonly string _source;
    private readonly int _line;
    private readonly int _column;

    public ScriptBlock(IEnumerable<ScriptEntry> entries, string source = "<memory>", int line = 1, int column = 1)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = Array.AsReadOnly(entries.ToArray());
        _source = source;
        _line = line;
        _column = column;
    }

    public IEnumerable<ScriptEntry> Named(string key)
    {
        return Entries.Where(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
    }

    public ScriptEntry? Optional(string key)
    {
        ScriptEntry? found = null;
        foreach (var entry in Named(key))
        {
            if (found is not null)
            {
                throw new FormatException($"{entry.Source}:{entry.Line}:{entry.Column}: повторное одиночное поле '{key}'.");
            }

            found = entry;
        }

        return found;
    }

    public ScriptEntry Required(string key)
    {
        return Optional(key) ?? throw new FormatException($"{_source}:{_line}:{_column}: отсутствует обязательное поле '{key}'.");
    }
}

public sealed class ScriptEntry
{
    public readonly string Key;
    public readonly string Operator;
    public readonly string? Scalar;
    public readonly ScriptBlock? Block;
    public readonly int Line;
    public readonly int Column;
    public readonly string Source;

    public ScriptEntry(string key, string operation, string? scalar, ScriptBlock? block, int line, int column, string source)
    {
        Key = key;
        Operator = operation;
        Scalar = scalar;
        Block = block;
        Line = line;
        Column = column;
        Source = source;
    }

    public ScriptBlock AsBlock()
    {
        return Block ?? throw new FormatException($"{Source}:{Line}:{Column}: поле '{Key}' должно быть блоком.");
    }

    public string AsScalar()
    {
        return Scalar ?? throw new FormatException($"{Source}:{Line}:{Column}: поле '{Key}' должно быть скаляром.");
    }
}

public static class ParadoxParser
{
    public static ScriptBlock Parse(string text, string source = "<memory>")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(source);
        return new Reader(text, source).ReadRoot();
    }

    private sealed class Reader
    {
        private readonly string _text;
        private readonly string _source;
        private int _position;
        private int _line = 1;
        private int _column = 1;
        private Token _current;

        public Reader(string text, string source)
        {
            _text = text;
            _source = source;
            if (_text.Length > 0 && _text[0] == '\uFEFF')
            {
                _position = 1;
            }

            _current = ReadToken();
        }

        public ScriptBlock ReadRoot()
        {
            return ReadBlock(false, _current);
        }

        private ScriptBlock ReadBlock(bool nested, Token opening)
        {
            var entries = new List<ScriptEntry>();
            while (_current.Kind != TokenKind.End)
            {
                if (_current.Kind == TokenKind.Close)
                {
                    if (!nested)
                    {
                        throw Error(_current, "лишняя закрывающая скобка.");
                    }

                    Advance();
                    return new ScriptBlock(entries, _source, opening.Line, opening.Column);
                }

                var key = _current;
                if (key.Kind != TokenKind.Value)
                {
                    throw Error(key, "ожидался ключ или элемент списка.");
                }

                Advance();
                if (_current.Kind != TokenKind.Operator)
                {
                    entries.Add(new ScriptEntry(key.Text, "", key.Text, null, key.Line, key.Column, _source));
                    continue;
                }

                var operation = _current.Text;
                Advance();
                if (_current.Kind == TokenKind.Open)
                {
                    var blockOpening = _current;
                    Advance();
                    var block = ReadBlock(true, blockOpening);
                    entries.Add(new ScriptEntry(key.Text, operation, null, block, key.Line, key.Column, _source));
                }
                else if (_current.Kind == TokenKind.Value)
                {
                    entries.Add(new ScriptEntry(key.Text, operation, _current.Text, null, key.Line, key.Column, _source));
                    Advance();
                }
                else
                {
                    throw Error(_current, $"ожидалось значение поля '{key.Text}'.");
                }
            }

            if (nested)
            {
                throw Error(opening, "незавершённый блок.");
            }

            return new ScriptBlock(entries, _source, opening.Line, opening.Column);
        }

        private void Advance()
        {
            _current = ReadToken();
        }

        private Token ReadToken()
        {
            SkipTrivia();
            var line = _line;
            var column = _column;
            if (_position == _text.Length)
            {
                return new Token(TokenKind.End, "", line, column);
            }

            var character = _text[_position];
            if (character == '{' || character == '}')
            {
                Consume();
                return new Token(character == '{' ? TokenKind.Open : TokenKind.Close, character.ToString(), line, column);
            }

            if (character == '"')
            {
                return ReadQuoted(line, column);
            }

            if (character == '@' && _position + 1 < _text.Length && _text[_position + 1] == '[')
            {
                return ReadInlineMath(line, column);
            }

            if (character == '[' && _position + 1 < _text.Length && _text[_position + 1] == '[')
            {
                var headerStart = _position;
                Consume();
                Consume();
                if (_position < _text.Length && _text[_position] == '!') { Consume(); }
                var nameStart = _position;
                while (_position < _text.Length && (char.IsAsciiLetterOrDigit(_text[_position]) || _text[_position] == '_'))
                {
                    Consume();
                    if (_position - nameStart > 128) { throw Error(new Token(TokenKind.Value, "[[", line, column), "Conditional template name exceeds 128 characters."); }
                }
                if (_position == nameStart || _position == _text.Length || _text[_position] != ']') { throw Error(new Token(TokenKind.Value, "[[", line, column), "Invalid or unterminated conditional template header."); }
                Consume();
                return new Token(TokenKind.Value, _text[headerStart.._position], line, column);
            }

            if (IsOperator(character))
            {
                Consume();
                var operation = character.ToString();
                if (_position < _text.Length && _text[_position] == '=' && character != '=')
                {
                    operation += '=';
                    Consume();
                }

                if (operation == "!")
                {
                    throw Error(new Token(TokenKind.Operator, operation, line, column), "ожидался оператор !=.");
                }

                return new Token(TokenKind.Operator, operation, line, column);
            }

            var start = _position;
            while (_position < _text.Length)
            {
                character = _text[_position];
                if (char.IsWhiteSpace(character) || character is '{' or '}' or '#' or '"' || IsOperator(character))
                {
                    break;
                }

                Consume();
            }

            return new Token(TokenKind.Value, _text[start.._position], line, column);
        }

        private Token ReadInlineMath(int line, int column)
        {
            var start = _position;
            Consume();
            Consume();
            var depth = 1;
            while (_position < _text.Length)
            {
                var character = _text[_position];
                Consume();
                if (character == '[')
                {
                    depth++;
                }
                else if (character == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return new Token(TokenKind.Value, _text[start.._position], line, column);
                    }
                }
            }

            throw Error(new Token(TokenKind.Value, "", line, column), "незавершённое inline math выражение.");
        }

        private Token ReadQuoted(int line, int column)
        {
            Consume();
            var result = new StringBuilder();
            while (_position < _text.Length)
            {
                var character = _text[_position];
                Consume();
                if (character == '"')
                {
                    return new Token(TokenKind.Value, result.ToString(), line, column);
                }

                if (character != '\\')
                {
                    result.Append(character);
                    continue;
                }

                if (_position == _text.Length)
                {
                    break;
                }

                var escaped = _text[_position];
                Consume();
                switch (escaped)
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    default: result.Append('\\').Append(escaped); break;
                }
            }

            throw Error(new Token(TokenKind.Value, "", line, column), "незавершённая строка.");
        }

        private void SkipTrivia()
        {
            while (_position < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_position]))
                {
                    Consume();
                    continue;
                }

                if (_text[_position] != '#')
                {
                    return;
                }

                while (_position < _text.Length && _text[_position] is not '\r' and not '\n')
                {
                    Consume();
                }
            }
        }

        private void Consume()
        {
            var character = _text[_position++];
            if (character == '\r')
            {
                _line++;
                _column = 1;
            }
            else if (character == '\n')
            {
                if (_position < 2 || _text[_position - 2] != '\r')
                {
                    _line++;
                }

                _column = 1;
            }
            else
            {
                _column++;
            }
        }

        private FormatException Error(Token token, string message)
        {
            return new FormatException($"{_source}:{token.Line}:{token.Column}: {message}");
        }

        private static bool IsOperator(char character)
        {
            return character is '=' or '!' or '<' or '>';
        }
    }

    private enum TokenKind
    {
        End,
        Value,
        Operator,
        Open,
        Close
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Line, int Column);
}
