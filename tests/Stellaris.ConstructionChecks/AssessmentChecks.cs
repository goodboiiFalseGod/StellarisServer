using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;
using Stellaris.Session.M3;

static class AssessmentChecks
{
    private static readonly ConstructionOrigin _origin = new("checks/source-shape", 1, 1);
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task NominalDuration()
    {
        var projection = Projection(baseBuildtime: 3600);
        var context = Context(false, false);
        var assessment = ConstructionAssessor.Assess(projection, context);
        var raw = ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment);
        ConstructionNominalQuoteBasisResult Evaluate(decimal? fraction) => ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment, new(projection, context, fraction));
        void Rejected(ConstructionNominalQuoteBasisResult result, string code) => Check(result.Nominal is null && result.Code == code, "Nominal refusal: expected " + code + ", got " + result.Code);
        foreach (var (fraction, factor) in new[] { (1m, 2m), (10m, 11m), (.5m, 1.5m), (0m, 1m), (-.5m, .5m), (-.9999999999999999999999999999m, .0000000000000000000000000001m), (.0000000000000000000000000001m, 1.0000000000000000000000000001m), (decimal.MaxValue - 1, decimal.MaxValue) })
        {
            var result = Evaluate(fraction);
            var nominal = result.Nominal;
            Check(result.Code == "nominal-ratio-only" && nominal is not null && nominal.NumeratorDays == 3600 && nominal.Denominator == factor, "Exact nominal operands changed.");
            Check(ReferenceEquals(nominal!.Source, projection) && ReferenceEquals(nominal.Context, context), "Nominal snapshot identity lost.");
            Check(nominal.Basis.RawBuildtime == raw.Basis!.RawBuildtime && nominal.Basis.RawCosts.SequenceEqual(raw.Basis.RawCosts) && nominal.Basis.RawInfrastructureUpkeep.SequenceEqual(raw.Basis.RawInfrastructureUpkeep) && nominal.Basis.ActiveJobContributions.SequenceEqual(raw.Basis.ActiveJobContributions), "Nominal arithmetic changed the raw basis.");
        }
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment, null), "nominal-speed-unavailable");
        Rejected(Evaluate(null), "nominal-speed-unavailable");
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment, new(Projection(baseBuildtime: 3600), context, 0)), "speed-source-snapshot-mismatch");
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment, new(projection, Context(false, false), 0)), "speed-context-snapshot-mismatch");
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(Projection(baseBuildtime: 3600), assessment, new(projection, context, 0)), "source-snapshot-mismatch");
        foreach (var fraction in new[] { -1m, -2m, decimal.MinValue }) { Rejected(Evaluate(fraction), "nonpositive-speed-factor"); }
        // Ordinary decimal addition can round these factors; nominal evaluation must refuse instead.
        foreach (var fraction in new[] { decimal.MaxValue, 7.9228162514264337593543950335m, 7922816251426433759354395033.5m }) { Rejected(Evaluate(fraction), "speed-factor-representation-unavailable"); }
        foreach (var baseTime in new[] { decimal.MaxValue, .0000000000000000000000000001m })
        {
            var source = Projection(baseBuildtime: baseTime);
            var assessed = ConstructionAssessor.Assess(source, context);
            var nominal = ConstructionQuoteBasisEvaluator.Evaluate(source, assessed, new(source, context, 10)).Nominal;
            Check(nominal?.NumeratorDays == baseTime && nominal.Denominator == 11, "Extreme base duration divided, rounded or overflowed.");
        }
        foreach (var baseTime in new[] { 0m, -1m })
        {
            var source = Projection(baseBuildtime: baseTime);
            Rejected(ConstructionQuoteBasisEvaluator.Evaluate(source, ConstructionAssessor.Assess(source, context), new(source, context, 0)), "invalid-raw-quote-basis");
        }
        var unknownContext = new ConstructionContext();
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(projection, ConstructionAssessor.Assess(projection, unknownContext), new(projection, unknownContext, 0)), "quote-basis-context-unresolved");
        var falseSource = Projection(extraPotential: F("always", "no"));
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(falseSource, ConstructionAssessor.Assess(falseSource, context), new(falseSource, context, 0)), "not-eligible");
        var unknownSource = Projection(extraPotential: F("unsupported_native_test", "yes"));
        Rejected(ConstructionQuoteBasisEvaluator.Evaluate(unknownSource, ConstructionAssessor.Assess(unknownSource, context), new(unknownSource, context, 0)), "quote-basis-context-unresolved");

        // Synthetic existing ledger fixture: evaluating fresh speed must not reprice or progress an order.
        var quote = new ConstructionQuote("checks", "checks", projection.Definition.Id, ConstructionKind.Building, 3600, raw.Basis!.RawCosts, new Dictionary<string, decimal> { ["energy"] = 2 }, _empty, []);
        var ledger = new ConstructionLedger(new("colony", ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal> { ["minerals"] = 1000 } }, ["slot"]);
        var enqueue = ledger.Enqueue("enqueue", 1, 0, "order", "slot", quote);
        Check(enqueue.Accepted, "Synthetic order failed enqueue.");
        var progress = enqueue.State.Progress("progress", 1, 1, "checks", [new("order", 100)]);
        Check(progress.Accepted, "Synthetic order failed progress.");
        var before = Json(progress.State);
        Check(Evaluate(1).Nominal!.Denominator == 2 && Evaluate(10).Nominal!.Denominator == 11, "Explicit current fractions were cached.");
        Check(Json(progress.State) == before && progress.State.Orders["order"].CompletedWork == 100 && progress.State.Orders["order"].Quote.RequiredWork == 3600 && progress.State.Orders["order"].Quote.Costs["minerals"] == 400 && progress.State.Accounts[1]["minerals"] == 600, "Nominal evaluation changed paid price/progress.");
        Check(ConstructionQuoteBasisEvaluator.Evaluate(projection, assessment).Code == "raw-basis-only", "Legacy evaluator changed after nominal refusals.");
        return Task.CompletedTask;
    }

    public static Task OwnerFlags()
    {
        var source = new List<string> { "fallen_empire_1", "yes" };
        var facts = new ConstructionSourceFacts(ownerFlags: source);
        source.Clear();
        ConstructionContext Context(IEnumerable<string>? flags, bool? exists = true) => new(ownerExists: exists, carrierFlags: ["fallen_empire_1"], sourceFacts: new ConstructionSourceFacts(ownerFlags: flags));
        ConstructionCondition Evaluate(ConstructionContext context, ConstructionField field, ConstructionAssessor.Scope scope = ConstructionAssessor.Scope.Planet) => new ConstructionAssessor.Evaluator(context).Condition([field], scope);
        var flag = Owner("has_country_flag", "fallen_empire_1");
        Check(Evaluate(new(ownerExists: true, sourceFacts: facts), flag).Truth == ConstructionTruth.True && facts.OwnerFlags!.Contains("yes"), "Owner flags were mutable or boolean-like identifiers lost.");
        Check(Evaluate(Context(["fallen_empire_1"]), flag).Truth == ConstructionTruth.True && Evaluate(Context(["FALLEN_EMPIRE_1"]), flag).Truth == ConstructionTruth.False, "Flag ID membership is not ordinal.");
        Check(Evaluate(Context([]), flag).Truth == ConstructionTruth.False && Evaluate(Context(null), flag).Truth == ConstructionTruth.Unknown, "Empty and unavailable owner flags conflated or carrier flags substituted.");
        Check(Evaluate(Context([]), F("NOT", children: [flag])).Truth == ConstructionTruth.True, "Inverted missing flag was not true.");
        foreach (var exists in new bool?[] { null, false })
        {
            Check(Evaluate(Context(["fallen_empire_1"], exists), flag).Truth == ConstructionTruth.Unknown, "Missing owner scope consumed supplied flags.");
        }
        var guarded = Evaluate(Context(null, false), F("AND", children: [F("exists", "owner"), flag]));
        Check(guarded.Truth == ConstructionTruth.False && guarded.Gates.Count != 0 && guarded.BlockingGates.Count == 0, "Explicit absent owner did not decisively deactivate guarded branch.");
        foreach (var malformed in new[] { F("has_country_flag", operation: "!="), F("has_country_flag", children: [F("value", "fallen_empire_1")]), F("has_country_flag", ""), F("has_country_flag", new string('x', 161)), F("has_country_flag", "bad\nflag"), F("has_country_flag", "$FLAG$"), F("has_country_flag", "fallen_empire_1", children: [F("always", "yes")]) })
        {
            Check(Evaluate(Context([]), F("owner", children: [malformed])).Truth == ConstructionTruth.Unknown, "Malformed owner flag became false/true.");
        }
        foreach (var scope in new[] { ConstructionAssessor.Scope.Planet, ConstructionAssessor.Scope.From })
        {
            Check(Evaluate(Context(["fallen_empire_1"]), F("has_country_flag", "fallen_empire_1"), scope).Truth == ConstructionTruth.Unknown, "Flag evaluated in wrong scope.");
        }
        foreach (var invalid in new IEnumerable<string>[] { Enumerable.Range(0, 257).Select(index => "flag_" + index), [""], [new string('x', 161)], ["bad\nflag"] })
        {
            try { _ = new ConstructionSourceFacts(ownerFlags: invalid); throw new InvalidOperationException("Invalid owner flag context accepted."); }
            catch (ArgumentException) { }
        }
        return Task.CompletedTask;
    }

    public static Task Conditions()
    {
        var projection = Projection();
        var unknown = ConstructionAssessor.Assess(projection, new());
        Check(unknown.Potential.Truth == ConstructionTruth.Unknown && unknown.Costs.All(cost => cost.Condition.Truth == ConstructionTruth.Unknown), "Unknown scopes became regular/false.");
        var regular = ConstructionAssessor.Assess(projection, Context(false, false));
        Check(ConstructionQuoteBasisEvaluator.Evaluate(Projection(), regular).Code == "source-snapshot-mismatch", "Different same-ID source snapshot mixed into quote basis.");
        Check(regular.Potential.Truth == ConstructionTruth.True && regular.Destroy.Truth == ConstructionTruth.False && regular.Prerequisites.Truth == ConstructionTruth.True, "Known conditions mis-evaluated.");
        Check(regular.Costs[0].Condition.Truth == ConstructionTruth.True && regular.Costs[1].Condition.Truth == ConstructionTruth.False && regular.Costs[1].RawMultiplier == .1m, "Cost branches/multiplier conflated.");
        // Both source-shaped blocks deliberately match; first-match evaluation must not discard either.
        Check(regular.Jobs.Count == 3 && regular.Jobs[0].Condition.Truth == ConstructionTruth.True && regular.Jobs[2].Condition.Truth == ConstructionTruth.True, "Independent job blocks were first-matched.");
        Check(regular.Jobs[0].RawCapacity.Single().Resource == "farmer" && regular.Jobs[1].RawCapacity.Single().Resource == "agri_drone", "Job IDs lost.");
        foreach (var mode in new[] { ConstructionAuthorityMode.Hive, ConstructionAuthorityMode.MachineIntelligence })
        {
            var gestalt = ConstructionAssessor.Assess(projection, Context(true, false));
            Check(gestalt.Jobs[0].Condition.Truth == ConstructionTruth.False && gestalt.Jobs[1].Condition.Truth == ConstructionTruth.True, "Gestalt branch lost: " + mode);
        }
        var nomadic = ConstructionAssessor.Assess(projection, Context(false, true));
        Check(nomadic.Costs[0].Condition.Truth == ConstructionTruth.False && nomadic.Costs[1].Condition.Truth == ConstructionTruth.True, "Separate from binding ignored.");
        var basis = ConstructionQuoteBasisEvaluator.Evaluate(projection, nomadic);
        Check(basis.Code == "raw-basis-only" && basis.Basis!.RawCosts["volatile_motes"] == 40 && basis.Basis.RawBuildtime == 360, "Explicit raw multiplier basis lost.");
        var noFrom = ConstructionAssessor.Assess(projection, Context(false, null));
        Check(noFrom.Costs.All(cost => cost.Condition.Truth == ConstructionTruth.Unknown), "Owner substituted for absent from.");
        Check(ConstructionQuoteBasisEvaluator.Evaluate(projection, noFrom).Basis is null, "Unknown cost binding became a quote basis.");
        var orProjection = Projection(extraPotential: F("OR", children: [F("exists", "owner"), F("unsupported_native_test", "yes")]));
        var decisiveOr = ConstructionAssessor.Assess(orProjection, Context(false, false));
        Check(decisiveOr.Potential.Truth == ConstructionTruth.True && decisiveOr.Potential.Gates.Count != 0 && decisiveOr.Potential.BlockingGates.Count == 0 && ConstructionQuoteBasisEvaluator.Evaluate(orProjection, decisiveOr).Basis is not null, "Irrelevant OR unknown blocked known truth.");
        var andProjection = Projection(extraPotential: F("AND", children: [Owner("is_ai", "yes"), F("unsupported_native_test", "yes")]));
        var decisiveAnd = ConstructionAssessor.Assess(andProjection, Context(false, false));
        Check(decisiveAnd.Potential.Truth == ConstructionTruth.False && decisiveAnd.Potential.BlockingGates.Count == 0 && ConstructionQuoteBasisEvaluator.Evaluate(andProjection, decisiveAnd).Code == "not-eligible", "Known false AND was unresolved.");
        Check(ConstructionAssessor.Assess(projection, new(technologies: [])).Prerequisites.Truth == ConstructionTruth.False, "Known missing technology not distinguished.");
        var unsupported = ConstructionAssessor.Assess(Projection(extraPotential: F("unsupported_native_test", "yes")), Context(false, false));
        Check(unsupported.Potential.Truth == ConstructionTruth.Unknown && unsupported.Potential.Gates.Count != 0, "Unsupported operator disappeared.");
        var wrongOperator = ConstructionAssessor.Assess(Projection(extraPotential: F("has_modifier", "resort_colony", operation: "!=")), Context(false, false));
        Check(wrongOperator.Potential.Truth == ConstructionTruth.Unknown, "Unsupported scalar operator guessed.");
        var badParent = ConstructionAssessor.Assess(Projection(jobOperation: "!="), Context(false, false));
        Check(badParent.Jobs[0].Condition.Truth == ConstructionTruth.Unknown, "Unsupported parent modifier operator guessed.");
        var duplicate = ConstructionAssessor.Assess(Projection(duplicateJob: true), Context(false, false));
        Check(duplicate.Jobs[0].Condition.Truth == ConstructionTruth.Unknown, "Duplicate job capacity semantics guessed.");
        var inactiveProjection = Projection(invalidInactiveJob: true);
        var inactive = ConstructionAssessor.Assess(inactiveProjection, Context(false, false));
        Check(inactive.Jobs[1].Condition.Truth == ConstructionTruth.False && inactive.Jobs[1].Condition.Gates.Count != 0 && inactive.Jobs[1].Condition.BlockingGates.Count == 0 && ConstructionQuoteBasisEvaluator.Evaluate(inactiveProjection, inactive).Basis is not null, "Inactive unsupported effect blocked independent basis.");
        var facts = new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Gestalt] = false };
        var context = new ConstructionContext(ownerFacts: facts, modifiers: new[] { "x" });
        facts[ConstructionOwnerPredicate.Gestalt] = true;
        Check(context.OwnerFacts[ConstructionOwnerPredicate.Gestalt] == false && context.Modifiers!.Contains("x"), "Context input not copied.");
        return Task.CompletedTask;
    }

    public static void Real(ConstructionProjection projection)
    {
        foreach (var gestalt in new[] { false, true })
        {
            var result = ConstructionAssessor.Assess(projection, Context(gestalt, false));
            Check(result.Potential.Truth == ConstructionTruth.True && result.Destroy.Truth == ConstructionTruth.False, "Real hydroponics conditions failed.");
            Check(result.Jobs.Count == 3 && result.Jobs.Count(job => job.Condition.Truth == ConstructionTruth.True) == 1, "Real farmers_add independent coverage failed.");
            Check(result.Jobs.Single(job => job.Condition.Truth == ConstructionTruth.True).RawCapacity.Single().Resource == (gestalt ? "agri_drone" : "farmer"), "Real job branch wrong.");
            Check(result.Costs[0].Condition.Truth == ConstructionTruth.True && result.Costs[1].Condition.Truth == ConstructionTruth.False && result.Gates.Count != 0, "Real raw quote gates lost.");
            Check(ConstructionQuoteBasisEvaluator.Evaluate(projection, result).Basis!.RawCosts["minerals"] == 400, "Real regular raw basis wrong.");
        }
    }

    public static Task Session()
    {
        var manifest = new M3AssetManifest([]);
        var session = NewSession(manifest, Projection());
        var host = session.Join("host", "player", null, TimeSpan.Zero);
        var second = session.Join("second", "player", null, TimeSpan.Zero);
        var observer = session.Join("observer", "observer", null, TimeSpan.Zero);
        Check(session.View(observer.ParticipantId).Development!.Colonies.Count == 0, "Pending observer development leak.");
        Execute(session, host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId), true, "OK");
        foreach (var player in new[] { host, second })
        {
            Execute(session, player, new("import", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), true, "OK");
            Execute(session, player, new("ready", "ready", Ready: true), true, "OK");
        }
        Execute(session, host, new("start", "start"), true, "OK");
        var own = session.View(host.ParticipantId).Development!;
        var other = session.View(second.ParticipantId).Development!;
        Check(own.Colonies.Count == 1 && other.Colonies.Count == 1 && own.Colonies[0].ColonyId != other.Colonies[0].ColonyId, "Player projection not filtered.");
        Check(own.Colonies[0].AuthorityMode == "unresolved" && own.Colonies[0].Catalog.Single().Quote is null && own.Colonies[0].Gates.Count != 0, "Invented live authority/quote.");
        Check(session.View(observer.ParticipantId).Development!.Colonies.Count == 0, "General observer received development private context.");
        Execute(session, observer, new("import", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), true, "OK");
        Execute(session, observer, new("pov", "observerView", TargetParticipantId: host.ParticipantId), true, "OK");
        Check(Json(session.View(observer.ParticipantId).Development!) == Json(own), "Matching observer POV differs.");
        var colony = own.Colonies.Single().ColonyId;
        var command = new M3ClientCommand("build", "enqueueConstruction", ColonyId: colony, ExpectedColonyRevision: 0, DefinitionId: "building_hydroponics_farm", SlotId: "unresolved-slot", OrderId: "order");
        Execute(session, observer, command, false, "OUT_OF_SCOPE");
        Execute(session, second, command, false, "OUT_OF_SCOPE");
        var before = Json(session.View(host.ParticipantId));
        var refusal = Execute(session, host, command, false, "DEPENDENCY_UNRESOLVED");
        Check(Execute(session, host, command, false, "DEPENDENCY_UNRESOLVED") == refusal && Json(session.View(host.ParticipantId)) == before, "Refused retry changed state.");
        Execute(session, host, command with { DefinitionId = "other" }, false, "ID_CONFLICT");
        Execute(session, host, command with { CommandId = "unknown", DefinitionId = "other" }, false, "UNKNOWN_DEFINITION");
        Execute(session, host, command with { CommandId = "stale", ExpectedColonyRevision = 1 }, false, "STALE_REVISION");
        Execute(session, host, command with { CommandId = "negative", ExpectedColonyRevision = -1 }, false, "INVALID_COMMAND");
        Execute(session, host, command with { CommandId = "large", OrderId = new string('x', 161) }, false, "INVALID_COMMAND");
        Execute(session, host, command with { CommandId = "numeric", Count = 1 }, false, "INVALID_COMMAND");
        Execute(session, host, new("cancel", "cancelConstruction", ColonyId: colony, ExpectedColonyRevision: 0, OrderId: "missing"), false, "DEPENDENCY_UNRESOLVED");
        Execute(session, host, command with { CommandId = "clear", Type = "clearBlocker" }, false, "UNKNOWN_DEFINITION");
        session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
        var returned = session.Join("host", "player", host.Token, TimeSpan.Zero);
        Check(Execute(session, returned, command, false, "DEPENDENCY_UNRESOLVED") == refusal, "Reconnect lost rejected ACK cache.");
        Execute(session, returned, command with { CommandId = "notReady" }, false, "ASSETS_NOT_READY");
        var unavailable = NewSession(manifest, null);
        var joined = unavailable.Join("host", "player", null, TimeSpan.Zero);
        Check(unavailable.View(joined.ParticipantId).Development!.Gates.Any(gate => gate.DependencyId == "construction-profile-unavailable"), "Import failure was hidden.");
        return Task.CompletedTask;
    }

    public static Task TemplateSelection()
    {
        var manifest = new M3AssetManifest([]);
        var session = NewSession(manifest, Projection());
        var host = session.Join("host", "player", null, TimeSpan.Zero);
        var second = session.Join("second", "player", null, TimeSpan.Zero);
        var observer = session.Join("observer", "observer", null, TimeSpan.Zero);
        var choose = new M3ClientCommand("select", "selectEmpireTemplate", TemplateId: "military");
        Check(session.View(host.ParticipantId).EmpireSetup!.SelectedTemplateId is null && session.View(second.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "Implicit default template assigned.");
        Check(session.View(host.ParticipantId).EmpireSetup!.Catalog.Select(template => template.TemplateId).SequenceEqual(new[] { "economic", "scientific", "military" }), "Catalog mismatch.");
        Check(session.View(host.ParticipantId).EmpireSetup!.Catalog.All(template => template.Traits.Contains("trait_pc_continental_preference") && template.Traits.Count(trait => trait.EndsWith("_preference", StringComparison.Ordinal)) == 1), "Lobby catalog lost or mixed the accepted founder preference.");
        Execute(session, host, choose with { CommandId = "notReady" }, false, "ASSETS_NOT_READY");
        foreach (var grant in new[] { host, second, observer })
        {
            Execute(session, grant, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), true, "OK");
        }
        Execute(session, observer, choose with { CommandId = "pending" }, false, "OUT_OF_SCOPE");
        Execute(session, host, new("ready", "ready", Ready: true), true, "OK");
        var economy = Json(session.View(host.ParticipantId).Planets);
        var selected = Execute(session, host, choose, true, "OK");
        Check(!session.View(host.ParticipantId).Participants.Single(participant => participant.Id == host.ParticipantId).Ready, "Selection did not clear readiness.");
        Check(Json(session.View(host.ParticipantId).Planets) == economy, "Template selection applied M2 economy effects.");
        Execute(session, host, new("readyAgain", "ready", Ready: true), true, "OK");
        var before = Json(session.View(host.ParticipantId));
        Check(Execute(session, host, choose, true, "OK") == selected && Json(session.View(host.ParticipantId)) == before, "Selection retry reset readiness or mutated state.");
        Execute(session, host, choose with { TemplateId = "economic" }, false, "ID_CONFLICT");
        foreach (var id in new[] { null, "", "hive", "voidfarers", "Military", "military\n", new string('x', 161) })
        {
            Execute(session, host, choose with { CommandId = "invalid" + (id?.Length.ToString() ?? "null") + (id is null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)))[..8]), TemplateId = id }, false, "INVALID_COMMAND");
            Check(Json(session.View(host.ParticipantId)) == before, "Invalid selection mutated state.");
        }
        Execute(session, host, choose with { CommandId = "foreign", TargetParticipantId = second.ParticipantId }, false, "INVALID_COMMAND");
        Check(session.View(second.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "Foreign selection leaked or applied.");
        Execute(session, host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId), true, "OK");
        Execute(session, observer, choose with { CommandId = "observer" }, false, "OUT_OF_SCOPE");
        Check(session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "General observer selected template leak.");
        Execute(session, observer, new("pov", "observerView", TargetParticipantId: host.ParticipantId), true, "OK");
        Check(session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId == "military", "Matching observer POV lost selection.");
        Execute(session, observer, new("secondPov", "observerView", TargetParticipantId: second.ParticipantId), true, "OK");
        Check(session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "Observer POV retained previous player's selection.");
        session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
        var returned = session.Join("host", "player", host.Token, TimeSpan.Zero);
        Check(session.View(returned.ParticipantId).EmpireSetup!.SelectedTemplateId == "military" && Execute(session, returned, choose, true, "OK") == selected, "Reconnect lost committed choice/retry.");
        Execute(session, returned, new("assetsAgain", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), true, "OK");
        Execute(session, returned, new("readyReturned", "ready", Ready: true), true, "OK");
        Execute(session, second, new("ready", "ready", Ready: true), true, "OK");
        Execute(session, returned, new("start", "start"), true, "OK");
        var own = session.View(host.ParticipantId).Development!.Colonies.Single();
        var other = session.View(second.ParticipantId).Development!.Colonies.Single();
        Check(own.AuthorityMode == "individualist" && !own.Gates.Any(gate => gate.DependencyId == "technology-context") && !own.Gates.Any(gate => gate.DependencyId == "empire-template-selection"), "Chosen template prerequisites not bound to own colony.");
        Check(other.AuthorityMode == "unresolved" && other.Gates.Any(gate => gate.DependencyId == "technology-context") && other.Gates.Any(gate => gate.DependencyId == "empire-template-selection"), "Shared assessment leaked selected context to unselected player.");
        Check(own.Catalog.Single().Quote is null && own.Gates.Any(gate => gate.DependencyId == "from-scope") && own.Slots.Count == 0, "Selection invented final quote/from/layout.");
        var running = Json(session.View(host.ParticipantId));
        Execute(session, returned, choose with { CommandId = "running", TemplateId = "economic" }, false, "INVALID_PHASE");
        Check(Json(session.View(host.ParticipantId)) == running, "Running selection mutated frozen identity.");
        Execute(session, second, choose with { CommandId = "secondRunning", TemplateId = "scientific" }, false, "INVALID_PHASE");
        var distinct = NewSession(manifest, Projection());
        var firstChoice = distinct.Join("first", "player", null, TimeSpan.Zero);
        var secondChoice = distinct.Join("second", "player", null, TimeSpan.Zero);
        var watcher = distinct.Join("watcher", "observer", null, TimeSpan.Zero);
        foreach (var grant in new[] { firstChoice, secondChoice, watcher })
        {
            Execute(distinct, grant, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), true, "OK");
        }
        Execute(distinct, firstChoice, choose with { TemplateId = "economic" }, true, "OK");
        Execute(distinct, secondChoice, choose with { TemplateId = "scientific" }, true, "OK");
        Check(distinct.View(firstChoice.ParticipantId).EmpireSetup!.SelectedTemplateId == "economic" && distinct.View(secondChoice.ParticipantId).EmpireSetup!.SelectedTemplateId == "scientific", "Different player choices were shared.");
        Execute(distinct, firstChoice, new("approve", "approveObserver", TargetParticipantId: watcher.ParticipantId), true, "OK");
        foreach (var (grant, expected) in new[] { (firstChoice, "economic"), (secondChoice, "scientific") })
        {
            Execute(distinct, watcher, new("pov" + grant.ParticipantId, "observerView", TargetParticipantId: grant.ParticipantId), true, "OK");
            Check(distinct.View(watcher.ParticipantId).EmpireSetup!.SelectedTemplateId == expected, "Different selected observer POV mixed choices.");
        }
        return Task.CompletedTask;
    }

    public static void ImageBinding(ConstructionProjection projection, M3AssetManifest manifest)
    {
        const string ROOT = "common/buildings/03_resource_buildings.txt";
        const string IMAGE = "gfx/interface/icons/buildings/building_hydroponics_farm.dds";
        Check(manifest.ProfileId == "m3-static-452-v11" && manifest.ImporterVersion == 8 && manifest.MaterialProfileVersion == 4 && manifest.SchemaVersion == 1, "Construction image check uses an unsupported asset profile.");
        M3GameSession Started(M3AssetManifest assets, out M3JoinGrant player, out M3JoinGrant observer)
        {
            var session = NewSession(assets, projection);
            player = session.Join("host", "player", null, TimeSpan.Zero);
            var second = session.Join("second", "player", null, TimeSpan.Zero);
            observer = session.Join("watcher", "observer", null, TimeSpan.Zero);
            Execute(session, player, new("old-assets", "assetReady", Ready: true, ProfileId: "m3-static-452-v6", ProfileDigest: assets.ProfileDigest), false, "PROFILE_MISMATCH");
            Execute(session, player, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId), true, "OK");
            foreach (var grant in new[] { player, second, observer })
            {
                Execute(session, grant, new("assets", "assetReady", Ready: true, ProfileId: assets.ProfileId, ProfileDigest: assets.ProfileDigest), true, "OK");
            }
            Execute(session, player, new("ready", "ready", Ready: true), true, "OK");
            Execute(session, second, new("ready", "ready", Ready: true), true, "OK");
            Execute(session, player, new("start", "start"), true, "OK");
            return session;
        }
        var valid = Started(manifest, out var host, out var watcher);
        var own = valid.View(host.ParticipantId).Development!;
        var entry = own.Colonies.Single().Catalog.Single();
        Check(entry.ImageDefinitionKey == "building.building_hydroponics_farm" && !own.Gates.Any(gate => gate.DependencyId == "construction-image-binding") && !entry.Gates.Any(gate => gate.DependencyId == "construction-image-binding"), "Verified real image binding remains unavailable.");
        Check(entry.Quote is null && entry.Eligibility == "unresolved" && entry.Gates.Count > 0, "Image binding activated numerical construction.");
        Check(valid.View(watcher.ParticipantId).Development!.Colonies.Count == 0, "Image binding leaked general observer colony.");
        Execute(valid, watcher, new("pov", "observerView", TargetParticipantId: host.ParticipantId), true, "OK");
        Check(Json(valid.View(watcher.ParticipantId).Development!) == Json(own), "Image binding differs for matching POV.");
        var json = Json(own);
        Check(!json.Contains(ROOT, StringComparison.Ordinal) && !json.Contains(IMAGE, StringComparison.Ordinal) && !projection.Files.Any(file => json.Contains(file.Sha256, StringComparison.Ordinal)), "Source metadata leaked into development JSON.");
        var transitive = projection.Files.First(file => file.RelativePath != ROOT && !manifest.Files.Any(asset => asset.RelativePath == file.RelativePath));
        var transitiveMismatch = manifest.Files.Append(new AssetManifestFile(transitive.RelativePath, transitive.ByteLength, new string('0', 64), Path.GetExtension(transitive.RelativePath)[1..]));
        foreach (var invalid in new[]
        {
            new M3AssetManifest(manifest.Files.Where(file => file.RelativePath != ROOT)),
            new M3AssetManifest(manifest.Files.Where(file => file.RelativePath != IMAGE)),
            new M3AssetManifest(manifest.Files.Select(file => file.RelativePath == ROOT ? new AssetManifestFile(file.RelativePath, file.ByteLength, new string('0', 64), file.Kind) : file)),
            new M3AssetManifest(manifest.Files.Select(file => file.RelativePath == ROOT ? new AssetManifestFile(file.RelativePath, file.ByteLength + 1, file.Sha256, file.Kind) : file)),
            new M3AssetManifest(transitiveMismatch)
        })
        {
            var rejected = Started(invalid, out var owner, out _).View(owner.ParticipantId).Development!;
            Check(rejected.Colonies.Single().Catalog.Single().ImageDefinitionKey is null && rejected.Gates.Any(gate => gate.DependencyId == "construction-image-binding"), "Missing or changed source snapshot advertised an image.");
        }
    }

    private static M3GameSession NewSession(M3AssetManifest manifest, ConstructionProjection? construction)
    {
        var metadata = new ContentManifest("4.5.2", "synthetic", [], []);
        var resources = new[] { new ResourceDefinition("energy", 1000000), new ResourceDefinition("minerals", 1000000), new ResourceDefinition("food", 1000000) };
        var jobs = new[] { "technician", "miner", "farmer" }.Select((id, index) => new JobDefinition(id, "worker", 100, new Dictionary<string, decimal> { [resources[index].Id] = 1 }, _empty)).ToArray();
        var buildings = new[] { "building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility" }.Select(id => new BuildingDefinition(id, _empty, [])).ToArray();
        var content = new EconomyProjection(metadata, resources, jobs, [new("worker", null)], buildings, [], 100);
        var traits = new[] { "trait_agrarian", "trait_industrious", "trait_ingenious" }.ToDictionary(id => id, id => new TraitWorkforceProjection(metadata, [id], jobs.ToDictionary(job => job.Id, _ => 0m), []));
        return new(content, traits, manifest, construction, constructionImportAttempted: true);
    }

    private static ConstructionContext Context(bool gestalt, bool? from) => new(true, new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.UsesFood] = true, [ConstructionOwnerPredicate.Ai] = false, [ConstructionOwnerPredicate.Wilderness] = false, [ConstructionOwnerPredicate.Regular] = !gestalt, [ConstructionOwnerPredicate.SpiritualistFallenEmpire] = false, [ConstructionOwnerPredicate.Gestalt] = gestalt }, from, "pc_continental", [], [], ["tech_hydroponics"], new Dictionary<string, decimal> { ["building_hydroponics_farm"] = 0 });
    private static ConstructionField F(string key, string? scalar = null, string operation = "=", params ConstructionField[] children) => new(key, operation, scalar, children, _origin, []);
    private static ConstructionField Owner(string key, string value) => F("owner", children: [F(key, value)]);
    private static ConstructionProjection Projection(ConstructionField? extraPotential = null, string jobOperation = "=", bool duplicateJob = false, bool invalidInactiveJob = false, decimal baseBuildtime = 360)
    {
        var potential = new List<ConstructionField> { F("exists", "owner"), Owner("is_ai", "no") };
        if (extraPotential is not null) { potential.Add(extraPotential); }
        ConstructionField Jobs(string key, string value, string job) => F("triggered_planet_modifier", operation: jobOperation, children: [F("potential", children: [Owner(key, value)]), F("modifier", children: duplicateJob || invalidInactiveJob && key == "is_gestalt" ? [F(job, "200"), F(job, "200")] : [F(job, "200")])]);
        var costs = new[] { new ConstructionCost([new("minerals", 400, _origin)], F("trigger", children: [F("from?", children: [F("is_nomadic", "no")])]), null), new ConstructionCost([new("volatile_motes", 400, _origin)], F("trigger", children: [F("from?", children: [F("is_nomadic", "yes")])]), .1m) };
        var definition = new ConstructionDefinition("building_hydroponics_farm", _origin, baseBuildtime, "resource", "planet_buildings", ["hydroponics"], ["tech_hydroponics"], [new("energy", 2, _origin)], costs,
            [F("potential", children: potential.ToArray()), F("destroy_trigger", children: [Owner("is_wilderness_empire", "yes")]), Jobs("is_regular_empire", "yes", "job_farmer_add"), Jobs("is_gestalt", "yes", "job_agri_drone_add"), Jobs("is_ai", "no", "job_farmer_add")]);
        return new(definition, [], []);
    }
    private static string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static M3CommandResult Execute(M3GameSession session, M3JoinGrant grant, M3ClientCommand command, bool accepted, string code)
    {
        var result = session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero);
        Check(result.Accepted == accepted && result.Code == code, command.Type + ": expected " + code + ", got " + result.Code);
        return result;
    }
    private static void Check(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
