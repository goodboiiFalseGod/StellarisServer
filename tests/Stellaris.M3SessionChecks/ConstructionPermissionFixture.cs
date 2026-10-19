using System.Reflection;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Session.M3.Construction;

/// <summary>Только транзакционные тесты: явный permission override, не доказательство native default.</summary>
internal static class ConstructionPermissionFixture
{
    public static M3ConstructionContent AllowDisable(M3ConstructionContent source, params string[] definitions)
    {
        var ids = definitions.ToHashSet(StringComparer.Ordinal);
        var origin = new ConstructionOrigin("test-only/explicit-disable-permission", 1, 1);
        var field = (ConstructionField)Constructor(typeof(ConstructionField)).Invoke(new object[]
            { "can_be_disabled", "=", "yes", Array.Empty<ConstructionField>(), origin, Array.Empty<ConstructionOrigin>() });
        return ReplaceField(source, ids, field);
    }

    public static M3ConstructionContent UntilBuildingCount(M3ConstructionContent source, string definitionId, string buildingId, int count)
    {
        var origin = new ConstructionOrigin("test-only/completion-condition", 1, 1);
        ConstructionField Field(string key, string operation, string? scalar, params ConstructionField[] children)
            => (ConstructionField)Constructor(typeof(ConstructionField)).Invoke(new object?[] { key, operation, scalar, children, origin, Array.Empty<ConstructionOrigin>() });
        return ReplaceField(source, new HashSet<string> { definitionId }, Field("potential", "=", null,
            Field("num_buildings", "=", null, Field("type", "=", buildingId), Field("value", "<", count.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
    }

    private static M3ConstructionContent ReplaceField(M3ConstructionContent source, IReadOnlySet<string> ids, ConstructionField field)
    {
        var catalogs = source.Content.Catalogs.Values.Distinct().Select(catalog =>
        {
            if (!catalog.Definitions.Keys.Any(ids.Contains)) return catalog;
            var next = catalog.Definitions.Values.Select(definition => !ids.Contains(definition.Id) ? definition
                : (ConstructionPlacementDefinition)Constructor(typeof(ConstructionPlacementDefinition)).Invoke(new object[]
                    { definition.Id, definition.Kind, definition.Origin, definition.Fields.Any(item => item.Key == field.Key)
                        ? definition.Fields.Select(item => item.Key == field.Key ? field : item) : definition.Fields.Append(field) }));
            return (ConstructionCatalogProjection)Constructor(typeof(ConstructionCatalogProjection)).Invoke(new object[]
                // Profile ID связывает числовую policy исходного определения. Override
                // явно помечен отдельными field Origin/Coverage; это не native import.
                { catalog.ProfileId, next, catalog.Parameters, catalog.Files, catalog.Coverage.Append(new ConstructionCoverage("test-only", "fixture", field.Origin.Source, field.Origin)) });
        }).ToArray();
        return (M3ConstructionContent)Constructor(typeof(M3ConstructionContent)).Invoke(new object[]
            { new ConstructionLiveContent(catalogs, source.Content.Economy, source.Content.Conditions), source.Zones, source.Placement, source.Civic, source.DistrictCapacityConditions });
    }
    private static ConstructorInfo Constructor(Type type) => type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
}
