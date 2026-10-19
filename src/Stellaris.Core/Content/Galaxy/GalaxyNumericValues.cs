using System.Globalization;

namespace Stellaris.Core.Content.Galaxy;

public sealed class GalaxyStructureException : Exception
{
    public string Code { get; }
    public GalaxySourceLocation Location { get; }
    public GalaxyStructureException(string code, string message, GalaxySourceLocation location)
        : base($"{location.Source}:{location.Line}:{location.Column}: {code}: {message}")
    { Code = code; Location = location; }
}
public readonly record struct GalaxyNumericRange(decimal Minimum, decimal Maximum)
{
    public static GalaxyNumericRange Constant(decimal value) => new(value, value);
}

/// <summary>Checked decimal arithmetic; RNG и округление принадлежат consumer.</summary>
public sealed class GalaxyNumericValues(GalaxyRuntimeCatalog catalog)
{
    public decimal Evaluate(GalaxySyntaxValue value, string? variableSource = null) =>
        Evaluate(value, variableSource ?? value.Location.Source, new HashSet<string>(StringComparer.Ordinal), 0);

    public GalaxyNumericRange Range(GalaxySyntaxValue value, string? variableSource = null)
    {
        if (value.Kind == GalaxySyntaxKind.Scalar) return GalaxyNumericRange.Constant(Evaluate(value, variableSource));
        if (value.Kind != GalaxySyntaxKind.Block) throw Error("numeric-form", "Ожидалось scalar или { min max }.", value.Location);
        if (value.Entries.Any(e => e.Operator != "=" || e.Key is not ("min" or "max")))
            throw Error("numeric-range", "Range поддерживает только min и max.", value.Location);
        var min = value.Entries.LastOrDefault(e => e.Key == "min");
        var max = value.Entries.LastOrDefault(e => e.Key == "max");
        var result = new GalaxyNumericRange(min is null ? 0 : Evaluate(min.Value, variableSource), max is null ? 0 : Evaluate(max.Value, variableSource));
        return result;
    }

    private decimal Evaluate(GalaxySyntaxValue value, string source, HashSet<string> active, int depth)
    {
        if (depth > 64) throw Error("numeric-depth", "Variable/expression depth превышает 64.", value.Location);
        if (value.Kind != GalaxySyntaxKind.Scalar) throw Error("numeric-form", "Ожидался scalar.", value.Location);
        var text = value.Text!;
        try
        {
            decimal Variable(string token)
            {
                var name = token.StartsWith('@') ? token : "@" + token;
                var found = catalog.Lookup(GalaxyRuntimeFamily.Variable, name, source);
                if (found.Status != GalaxyLookupStatus.Unique) throw Error("numeric-variable", $"{name}: {found.Status}", value.Location);
                var definition = found.Candidates[0]; var identity = definition.File.Path + ":" + name;
                if (!active.Add(identity)) throw Error("numeric-cycle", identity, value.Location);
                try { return Evaluate(definition.Syntax.Value, definition.File.Path, active, depth + 1); }
                finally { active.Remove(identity); }
            }
            if (text.StartsWith("@[", StringComparison.Ordinal) && text.EndsWith(']'))
                return new Arithmetic(text[2..^1], Variable, value.Location).Read();
            if (text.StartsWith('@')) return Variable(text);
            if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number)) return number;
            throw Error("numeric-symbol", "Не поддержан numeric token: " + text, value.Location);
        }
        catch (Exception error) when (error is OverflowException or DivideByZeroException)
        { throw Error("numeric-arithmetic", error.Message, value.Location); }
    }
    private static GalaxyStructureException Error(string code, string message, GalaxySourceLocation location) => new(code, message, location);
    private sealed class Arithmetic(string text, Func<string, decimal> variable, GalaxySourceLocation location)
    {
        private int position, depth;
        public decimal Read()
        {
            var value = Sum(); Space();
            if (position != text.Length) throw Error("numeric-expression", "Лишний token.", location);
            return value;
        }
        private decimal Sum()
        {
            var value = Product();
            while (true)
            {
                Space(); if (Take('+')) value = checked(value + Product());
                else if (Take('-')) value = checked(value - Product()); else return value;
            }
        }
        private decimal Product()
        {
            var value = Atom();
            while (true)
            {
                Space(); if (Take('*')) value = checked(value * Atom());
                else if (Take('/')) value = checked(value / Atom()); else return value;
            }
        }
        private decimal Atom()
        {
            if (++depth > 64) throw Error("numeric-depth", "Expression depth превышает 64.", location);
            try
            {
                Space(); if (Take('+')) return Atom(); if (Take('-')) return checked(-Atom());
                if (Take('('))
                {
                    var nested = Sum(); Space();
                    if (!Take(')')) throw Error("numeric-expression", "Нет закрывающей ).", location);
                    return nested;
                }
                var start = position;
                if (position < text.Length && (text[position] == '@' || char.IsAsciiLetter(text[position]) || text[position] == '_'))
                {
                    position++;
                    while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] == '_')) position++;
                    return variable(text[start..position]);
                }
                while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.')) position++;
                if (start == position || !decimal.TryParse(text[start..position], NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value)) throw Error("numeric-expression", "Ожидалось число/variable.", location);
                return value;
            }
            finally { depth--; }
        }
        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char c) { if (position == text.Length || text[position] != c) return false; position++; return true; }
    }
}
