using System.Text.RegularExpressions;

namespace Stellaris.Core.Scripting;

// Значения inline аргументов и их исходные местоположения передаются вместе.
internal sealed class InlineScriptParameters : Dictionary<string, string>
{
    internal Dictionary<string, ScriptEntry> Origins { get; } = new(StringComparer.Ordinal);
    internal InlineScriptParameters() : base(StringComparer.Ordinal) { }
    internal InlineScriptParameters(IReadOnlyDictionary<string, string> parent) : this()
    {
        foreach (var pair in parent) Add(pair.Key, pair.Value);
        if (parent is InlineScriptParameters sourced)
            foreach (var pair in sourced.Origins) Origins.Add(pair.Key, pair.Value);
    }
}

// Только две формы TRIGGER из native cost switcher 4.5.2. Остальные macro forms
// сохраняются как неподдержанные условия, а не превращаются в true/пустой блок.
internal static class InlineTriggerFragment
{
    private const string Source = "common/inline_scripts/buildings/nomadic_cost_switcher_with_additional_trigger.txt";
    private static readonly Regex Supported = new(
        @"\A\s*(?:has_ringworld_output_boost\s*=\s*(?:yes|no)|owner\?\s*=\s*\{\s*is_lithoid_or_infernal_empire\s*=\s*(?:yes|no)\s*\})\s*\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal static bool TryExpand(ScriptEntry placeholder, IReadOnlyDictionary<string, string> parameters, out ScriptEntry expanded)
    {
        expanded = null!;
        if (placeholder.Source != Source || placeholder.Key != "$TRIGGER$" || placeholder.Operator != ""
            || placeholder.Scalar != "$TRIGGER$" || placeholder.Block is not null
            || parameters is not InlineScriptParameters sourced || !sourced.TryGetValue("TRIGGER", out var text)
            || !sourced.Origins.TryGetValue("TRIGGER", out var argument)) return false;
        // Проверка до Parse ограничивает и размер, и глубину: grammar допускает
        // один root и не более одного вложенного predicate, без строк/inline calls.
        if (text.Length > 512 || !Supported.IsMatch(text)) return false;
        var parsed = ParadoxParser.Parse(text, argument.Source);
        if (parsed.Entries is not [var root]) return false;
        expanded = Rebase(root);
        return true;

        ScriptEntry Rebase(ScriptEntry node) => new(node.Key, node.Operator, node.Scalar,
            node.Block is null ? null : new ScriptBlock(node.Block.Entries.Select(Rebase), argument.Source, argument.Line, argument.Column),
            argument.Line, argument.Column, argument.Source);
    }
}
