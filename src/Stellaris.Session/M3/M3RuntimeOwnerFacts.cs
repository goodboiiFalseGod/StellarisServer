using System.Collections.Frozen;

namespace Stellaris.Session.M3;

// M3 пока не реализует получение ascension perks или традиций; оба snapshot заведомо пусты.
internal static class M3RuntimeOwnerFacts
{
    internal const bool OwnerGalacticCommunityMember = false;
    internal static readonly IReadOnlySet<string> AscensionPerks = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> HeldTraditions = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
}
