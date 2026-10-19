using Stellaris.Core.Construction;

static class TemplateChecks
{
    public static Task Ordinary()
    {
        var definitions = new[]
        {
            ("economic", new[] { "ethic_fanatic_authoritarian", "ethic_materialist" }, "civic_mining_guilds", new[] { "trait_ingenious" }),
            ("scientific", new[] { "ethic_fanatic_materialist", "ethic_authoritarian" }, "civic_hyperspace_specialty", new[] { "trait_intelligent" }),
            ("military", new[] { "ethic_fanatic_militarist", "ethic_authoritarian" }, "civic_distinguished_admiralty", new[] { "trait_strong", "trait_resilient" })
        };
        foreach (var (id, ethics, civic, traits) in definitions)
        {
            var template = ConstructionOrdinaryTemplate.Get(id);
            Check(template.Id == id && template.AuthorityId == "auth_imperial" && template.OriginId == "origin_default", "Template identity changed.");
            Check(template.PreferenceTraitId == "trait_pc_continental_preference" && template.IdealPlanetClass == "pc_continental", "Accepted founder preference binding changed.");
            Check(template.Ethics.SetEquals(ethics) && template.Civics.SetEquals(new[] { civic, "civic_functional_architecture" }) && template.Traits.SetEquals(traits.Append("trait_pc_continental_preference")), "Accepted template definition changed.");
            Check(template.Traits.Count(trait => trait.EndsWith("_preference", StringComparison.Ordinal)) == 1, "Another founder preference leaked into ordinary creation.");
            Check(template.StartingTechnologies.Count == 31 && template.StartingTechnologies.Contains("tech_hydroponics") && template.StartingTechnologies.Contains("tech_power_plant_1") && template.StartingTechnologies.Contains("tech_interplanetary_commerce"), "Accepted researched startup requirement changed.");
            Check(!template.StartingTechnologies.Contains("tech_nomads_space_construction") && !template.StartingTechnologies.Contains("tech_power_plant_2"), "Other template/research technology leaked into startup.");
            foreach (var values in new[] { template.Ethics, template.Civics, template.Traits, template.StartingTechnologies })
            {
                if (values is not ISet<string> set) { continue; }
                Check(set.IsReadOnly, "Mutable template collections exposed.");
                try { set.Add("unapproved_value"); }
                catch (NotSupportedException) { continue; }
                throw new InvalidOperationException("Frozen template accepted mutation.");
            }
            var unknown = template.CreateInitialConstructionContext(new());
            Check(unknown.OwnerExists is null && unknown.OwnerFacts.Count == 0 && unknown.FromNomadic is null && unknown.PlanetClass is null && unknown.Modifiers is null && unknown.CarrierFlags is null && unknown.BuildingCounts is null, "Template guessed runtime facts/layout/from binding.");
            Check(unknown.Technologies!.SetEquals(template.StartingTechnologies), "Initial researched state not initialized.");
            var facts = new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Regular] = true, [ConstructionOwnerPredicate.UsesFood] = null };
            var sourceFacts = new ConstructionSourceFacts(districtSet: "standard", civics: ["explicit_civic"]);
            var runtime = new ConstructionContext(true, facts, false, "pc_arid", ["explicit_modifier"], [], ["explicit_previous_technology"], new Dictionary<string, decimal> { ["building_hydroponics_farm"] = 2 }, sourceFacts);
            var initialized = template.CreateInitialConstructionContext(runtime);
            facts.Clear();
            Check(initialized.OwnerExists == true && initialized.OwnerFacts[ConstructionOwnerPredicate.Regular] == true && initialized.OwnerFacts[ConstructionOwnerPredicate.UsesFood] is null && initialized.FromNomadic == false && initialized.PlanetClass == "pc_arid" && initialized.Modifiers!.Contains("explicit_modifier") && initialized.CarrierFlags!.Count == 0 && initialized.BuildingCounts!["building_hydroponics_farm"] == 2, "Explicit runtime context lost or aliased.");
            Check(!initialized.Technologies!.Contains("explicit_previous_technology") && runtime.Technologies!.Contains("explicit_previous_technology"), "Initial-tech replacement mutated input or merged unapproved state.");
            Check(ReferenceEquals(initialized.SourceFacts, sourceFacts) && initialized.SourceFacts.Civics!.Contains("explicit_civic") && unknown.SourceFacts is null, "Initial context dropped or invented immutable source facts.");
        }
        foreach (var id in new[] { null, "", "Economic", "hive", "machine", "voidfarers", new string('x', 161) })
        {
            try { ConstructionOrdinaryTemplate.Get(id!); }
            catch (ArgumentException) { continue; }
            throw new InvalidOperationException("Unknown template received a fallback.");
        }
        return Task.CompletedTask;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
