using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

public sealed record M3DistrictCapacityResult(string? SharedPoolId, bool? Uncapped, int? Capacity, IReadOnlyList<string> Members, string? Gate);

/// <summary>Source-пул районов текущего ordinary graph; неизвестные predicates и множители не заменяются нулём.</summary>
public static class M3DistrictCapacity
{
    public static M3DistrictCapacityResult Evaluate(M3ConstructionContent source, M3StartupColony startup,
        ConstructionLiveRuntime state, string definitionId, ConstructionContext context, IReadOnlyDictionary<string, bool> economyFacts)
    {
        M3DistrictCapacityResult Unknown(string code) => new(null, null, null, [], code);
        if (!source.Content.Catalogs.TryGetValue(definitionId, out var catalog)) return Unknown("district-binding-unresolved");
        var definition = catalog.Definitions[definitionId];
        var bindings = definition.Fields.Where(field => field.Key == "shared_capacity_modifier").ToArray();
        var uncappedFields = definition.Fields.Where(field => field.Key == "is_uncapped").ToArray();
        if (definition.Kind != "district" || bindings.Length != 1 || bindings[0].Operator != "=" || bindings[0].Scalar is not { Length: > 0 } pool || bindings[0].Children.Count != 0
            || uncappedFields.Length != 1 || uncappedFields[0].Operator != "=" || uncappedFields[0].Scalar is not null) return Unknown("district-binding-unresolved");
        var uncapped = All(uncappedFields[0].Children, false, 0);
        if (uncapped is null) return Unknown("district-uncapped-context-unresolved");
        var members = source.Content.Catalogs.Keys.Where(id => source.Content.Catalogs[id].Definitions[id].Kind == "district"
            && source.Content.Catalogs[id].Definitions[id].Fields.Any(field => field.Key == "shared_capacity_modifier" && field.Scalar == pool)).Order(StringComparer.Ordinal).ToArray();
        if (uncapped.Value) return new(pool, true, null, members, null);
        var key = pool + "_max_add";
        decimal total = startup.Plan.DistrictCaps.Where(cap => cap.ModifierId == key).Sum(cap => cap.Value);
        foreach (var installed in state.Installed.Values)
        {
            if (installed.Kind == ConstructionKind.Zone || !installed.Enabled) continue;
            var definitions = installed.Kind == ConstructionKind.Building ? source.Content.Economy.Buildings : source.Content.Economy.Districts;
            if (!definitions.TryGetValue(installed.DefinitionId, out var recipe)) return Unknown("district-capacity-installed-unresolved");
            if (!Add(recipe, 1)) return Unknown("district-capacity-modifier-unresolved");
        }
        foreach (var zone in M3ZoneGraph.Current(startup, state))
        {
            var level = M3ZoneGraph.AttachedLevel(startup, state, zone.DistrictInstanceId);
            if (!source.Content.Economy.Zones.TryGetValue(zone.ZoneId, out var recipe))
            {
                if (!startup.Plan.ZoneRecurringEconomyProofs.Any(proof => proof.ZoneId == zone.ZoneId)) return Unknown("district-capacity-zone-unresolved");
                continue;
            }
            if (!Add(recipe, level)) return Unknown("district-capacity-modifier-unresolved");
        }
        if (total != decimal.Truncate(total) || total < 0 || total > int.MaxValue) return Unknown("district-capacity-range-unresolved");
        return new(pool, false, (int)total, members, null);

        bool Add(PlanetEconomyDefinition recipe, int attachedLevel)
        {
            foreach (var term in recipe.Terms.Where(term => term.TargetId == key || term.TargetId == pool + "_max_mult"))
            {
                if (term.ConditionDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) continue;
                var active = term.ConditionDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext ? ConstructionLiveConditions.Evaluate(term.Condition, economyFacts) : true;
                if (active == false) continue;
                if (active is null || term.Value is null || term.Kind is not (PlanetEconomyTermKind.PlanetModifier or PlanetEconomyTermKind.PlanetModifierMultiplier)
                    || term.TargetId.EndsWith("_max_mult", StringComparison.Ordinal) && term.Value != 0) return false;
                if (term.TargetId == key) total = checked(total + term.Value.Value * (term.ApplicationScope == PlanetEconomyApplicationScope.PerAttachedDistrictLevel ? attachedLevel : 1));
            }
            return true;
        }
        bool? All(IReadOnlyList<ConstructionField> fields, bool owner, int depth)
        {
            var values = fields.Select(field => Condition(field, owner, depth + 1)).ToArray();
            return values.Any(value => value == false) ? false : values.Any(value => value is null) ? null : true;
        }
        bool? Condition(ConstructionField field, bool owner, int depth)
        {
            if (depth > 24 || field.Operator != "=") return null;
            if (field.Key is "AND" or "OR" or "NOT")
            {
                if (field.Scalar is not null) return null;
                if (field.Key != "OR") { var result = All(field.Children, owner, depth); return field.Key == "NOT" ? result is null ? null : !result.Value : result; }
                var values = field.Children.Select(child => Condition(child, owner, depth + 1)).ToArray();
                return values.Any(value => value == true) ? true : values.Any(value => value is null) ? null : false;
            }
            if (!owner && field.Key == "owner_or_space_owner" && field.Scalar is null) return context.OwnerExists == true ? All(field.Children, true, depth) : null;
            if (source.DistrictCapacityConditions.Definitions.TryGetValue(field.Key, out var trigger) && trigger.Kind == (owner ? "ownerCondition" : "planetCondition"))
            {
                if (field.Scalar is not ("yes" or "no") || field.Children.Count != 0) return null;
                var result = All(trigger.Fields, owner, depth);
                return field.Scalar == "yes" ? result : result is null ? null : !result.Value;
            }
            if (field.Children.Count != 0 || field.Scalar is null) return null;
            if (owner) return field.Key switch
            {
                "has_origin" => startup.Plan.Context.Origin == field.Scalar,
                "has_valid_civic" => context.SourceFacts?.ValidCivics?.Contains(field.Scalar),
                _ => null
            };
            return field.Key switch
            {
                "exists" when field.Scalar == "owner_or_space_owner" => context.OwnerExists == true ? true : null,
                "is_planet_class" => context.PlanetClass is null ? null : context.PlanetClass == field.Scalar,
                "uses_district_set" => context.SourceFacts?.DistrictSet is { } set ? set == field.Scalar : null,
                "has_carrier_flag" => context.CarrierFlags?.Contains(field.Scalar),
                "always" when field.Scalar is "yes" or "no" => field.Scalar == "yes",
                _ => null
            };
        }
    }
}
