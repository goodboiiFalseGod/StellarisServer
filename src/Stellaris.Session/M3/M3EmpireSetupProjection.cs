using Stellaris.Core.Construction;

namespace Stellaris.Session.M3;

internal static class M3EmpireSetupProjection
{
    public static readonly IReadOnlyList<M3EmpireTemplateView> Catalog = Array.AsReadOnly(new[] { "economic", "scientific", "military" }.Select(id =>
    {
        var template = ConstructionOrdinaryTemplate.Get(id);
        return new M3EmpireTemplateView(template.Id, template.AuthorityId, template.OriginId, Array.AsReadOnly(template.Ethics.Order(StringComparer.Ordinal).ToArray()), Array.AsReadOnly(template.Civics.Order(StringComparer.Ordinal).ToArray()), Array.AsReadOnly(template.Traits.Order(StringComparer.Ordinal).ToArray()));
    }).ToArray());
}
