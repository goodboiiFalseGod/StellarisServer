using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class ZoneReplacementChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var binding = M3ConstructionContent.FromBinding.ActingCountry(false);
        var storage = economy.Resources.Where(pair => pair.Value.StoragePolicy != PlanetEconomyStoragePolicy.Unspecified)
            .ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum));
        var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = economy.Resources.Keys.ToDictionary(id => id, _ => 10000m) }, storage);
        var actor = new M3ConstructionActor(1, true, true, true);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("replace-zone-" + preset, 1, home.Homeworld, home.Plan);
            var initial = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(preset);
            var production = new M3ConstructionAdapter(content, startup, template, fromBinding: binding);
            // Явная fixture сохраняет нулевую скорость зоны по принятому building-модификатору.
            var adapter = new M3ConstructionAdapter(content, startup, template, new("replacement-test-only", -.15m, 0, 0, 0), binding);
            var zone = initial.Installed.Values.Single(item => item.Kind == ConstructionKind.Zone && item.DefinitionId == "zone_industrial");
            var instance = initial.Slots[zone.SlotId].ZoneInstanceId!;
            M3ZoneReplacementPreviewResult Preview(ConstructionLiveRuntime state, M3ConstructionAdapter? owner = null) => (owner ?? adapter)
                .PreviewZoneReplacement(state, "zone_foundry", "zone_industrial", zone.SlotId, instance, bank);
            var native = Preview(initial, production).Preview ?? throw new Exception("Нет production preview");
            var buildingModifiers = content.PresetModifiers(template, ConstructionKind.Building);
            Check(native.CanEnqueue && native.Quote is { } sourceQuote
                && sourceQuote.Costs["minerals"] == 1000m * (1 + buildingModifiers.Cost)
                && sourceQuote.DurationDays == 360m / (1 + buildingModifiers.Speed), "Production replacement quote не использует source modifiers здания");
            var preview = Preview(initial).Preview ?? throw new Exception("Нет fixture preview");
            Check(preview.CanEnqueue && preview.Quote is { DurationDays: 360 } && preview.Quote.Costs["minerals"] == 850m,
                "Source quote replacement: " + string.Join(',', preview.Gates.Select(gate => gate.DependencyId)));
            var factory = initial.Installed.Values.Single(item => item.DefinitionId == "building_factory_1" && initial.Slots[item.SlotId].ZoneInstanceId == instance);
            Check(preview.RemovedBuildings.Count == 1 && preview.RemovedBuildings[0].SlotId == factory.SlotId
                && preview.RemovedBuildings[0].DefinitionId == factory.DefinitionId, "Preview потерял точный factory tuple");
            var command = new M3ClientCommand("replace", "enqueueZoneReplacement", ColonyId: initial.ColonyId, ExpectedColonyRevision: initial.Revision,
                DefinitionId: "zone_foundry", SourceDefinitionId: "zone_industrial", SlotId: zone.SlotId, ZoneInstanceId: instance, OrderId: "zone-order",
                ZoneReplacementPreviewId: preview.PreviewId, ConfirmZoneRemoval: true);
            void Rejected(M3ClientCommand request, string code, M3ConstructionActor? who = null, M3ConstructionAdapter? owner = null)
            {
                var result = (owner ?? adapter).Execute(initial, bank, who ?? actor, request);
                Check(!result.Accepted && result.Code == code && ReferenceEquals(result.State, initial) && result.ResourceDeltas.Count == 0, "Отказ " + code + ": " + result.Code);
            }
            Rejected(command with { ConfirmZoneRemoval = false }, "ZONE_REMOVAL_CONFIRMATION_REQUIRED");
            Rejected(command with { ConfirmZoneRemoval = null }, "INVALID_COMMAND");
            Rejected(command with { ZoneReplacementPreviewId = "wrong" }, "ZONE_REPLACEMENT_PREVIEW_CHANGED");
            Rejected(command with { ExpectedColonyRevision = initial.Revision + 1 }, "ZONE_REPLACEMENT_PREVIEW_CHANGED");
            Rejected(command with { Enabled = false }, "INVALID_COMMAND");
            Rejected(command, "OUT_OF_SCOPE", actor with { ParticipantId = 2 });
            var capitalFixture = new ConstructionLiveRuntime(initial.ColonyId, initial.OwnerId, initial.Slots.Values,
                initial.Installed.Values.Select(item => item.SlotId == factory.SlotId ? item with { DefinitionId = "building_capital" } : item),
                initial.Revision, initial.StartupBuildingBySlot);
            var capitalPreview = Preview(capitalFixture).Preview!;
            Check(!capitalPreview.CanEnqueue && capitalPreview.Gates.Any(gate => gate.DependencyId == "zone-capital-building-present"), "Capital не блокирует замену зоны");
            Check(adapter.Execute(capitalFixture, bank, actor, command with { ZoneReplacementPreviewId = capitalPreview.PreviewId }).Code == "zone-capital-building-present",
                "Execute обошёл capital gate preview");
            // Trusted demolition fixture проверяет пустое подтверждение отдельно от native can_demolish.
            var empty = initial.DemolishBuilding("empty-fixture", 1, initial.Revision, factory.SlotId, factory.DefinitionId).State;
            var emptyPreview = Preview(empty).Preview!;
            Check(emptyPreview.RemovedBuildings.Count == 0 && emptyPreview.CanEnqueue, "Пустой removal preview требует чужой lifecycle");
            Check(adapter.Execute(empty, bank, actor, command with { ExpectedColonyRevision = empty.Revision, ZoneReplacementPreviewId = emptyPreview.PreviewId, ConfirmZoneRemoval = false }).Accepted,
                "Пустой список требует подтверждения удаления");
            var accepted = adapter.Execute(initial, bank, actor, command);
            Check(accepted.Accepted && accepted.ResourceDeltas.Single().Amount == -850m, "Подтверждённый replacement не принят: " + accepted.Code);
            var paid = new ConstructionBankSnapshot(bank.Accounts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, decimal>)pair.Value
                .ToDictionary(value => value.Key, value => value.Value + accepted.ResourceDeltas.Where(delta => delta.PayerId == pair.Key && delta.ResourceId == value.Key).Sum(delta => delta.Amount))), storage);
            var pending = accepted.State;
            Check(pending.Installed[factory.SlotId] == factory && pending.Installed[zone.SlotId] == zone, "Ранняя смена graph");
            Check(adapter.Execute(pending, paid, actor, command).Code == "duplicate", "ACK loss/replay повторно оплатил replacement");
            var pendingView = adapter.Project(pending, 1, bank: paid).Queue.Single(item => item.OrderId == "zone-order");
            Check(pendingView.ZoneReplacement?.RemovedBuildings.Single().SlotId == factory.SlotId, "Queue DTO потерял warning target");
            var cancel = pending.Cancel("cancel", 1, pending.Revision, "zone-order", paid);
            Check(cancel.Accepted && cancel.ResourceDeltas.Single().Amount == 850m && cancel.State.Installed[factory.SlotId] == factory, "Cancel изменил старую зону/здание");
            var before = initial.Installed.Values.Where(item => item.Kind == ConstructionKind.Building && item.SlotId != factory.SlotId).ToArray();
            for (var day = 1; day <= 360; day++)
            {
                var step = adapter.AdvanceDay(pending, paid, "day-" + day);
                Check(step.Accepted, "Replacement daily: " + step.Code);
                pending = step.State;
                if (day < 360) Check(pending.Installed[zone.SlotId].DefinitionId == "zone_industrial" && pending.Installed.ContainsKey(factory.SlotId), "Ранний removal");
            }
            Check(pending.Installed[zone.SlotId].DefinitionId == "zone_foundry" && !pending.Installed.ContainsKey(factory.SlotId)
                && before.All(item => pending.Installed[item.SlotId] == item), "Completion затронул лишние buildings");
            Check(pending.Slots.Values.Where(slot => slot.Kind == ConstructionKind.Building && slot.ZoneInstanceId == instance)
                .All(slot => slot.ZoneDefinitionId == "zone_foundry"), "Неатомарный child binding");
            var final = adapter.Project(pending, 1, bank: paid);
            Check(final.StartupZones!.Single(item => item.InstanceId == instance).ZoneId == "zone_foundry"
                && final.StartupBuildings!.All(item => item.SlotInstanceId != factory.SlotId), "Current DTO показывает старую зону/factory");
            var world = profile.CreateWorld(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, preset, home.Plan);
            var jobs = profile.RefreshInstalledGraph(world, planet, startup, pending, preset);
            var repeated = profile.RefreshInstalledGraph(world, planet, startup, pending, preset);
            Check(jobs.OrderBy(pair => pair.Key).SequenceEqual(repeated.OrderBy(pair => pair.Key)), "Повтор completion economy удвоил jobs");
            // Trusted malformed consent fixture: действующий список удаления пуст, хотя factory несовместима.
            var trustedQuote = accepted.State.Orders["zone-order"].Quote;
            var wrong = initial.Enqueue("unapproved", 1, initial.Revision, "unapproved-order", zone.SlotId, trustedQuote, bank,
                zoneReplacement: new("zone_industrial", instance, new Dictionary<string, string>()));
            Check(wrong.Accepted, "Не создана test-only outdated-consent fixture");
            var recheck = adapter.AdvanceDay(wrong.State, paid, "unapproved-day");
            Check(recheck.Accepted && recheck.State.Orders["unapproved-order"].CancellationReason == ConstructionCancellationReason.ConditionLost
                && recheck.State.Installed.ContainsKey(factory.SlotId) && recheck.State.Installed[zone.SlotId].DefinitionId == "zone_industrial", "Неподтверждённое дополнительное удаление прошло completion");
        }
        Console.WriteLine("PASS C03 source3presets: preview/confirm/stale/no pay/native speed gate, fixed quote/live completion/cancel, exact removal/current DTO/economy, unapproved loss auto-cancel");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
