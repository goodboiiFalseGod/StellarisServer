using System.Collections.Frozen;

namespace Stellaris.Core.Galaxy;

public enum GalaxyConsumerEntityKind { Country, Species, Colony, EconomyPlanet, District, Building, Zone, Fleet, Ship }
/// <summary>Проекция actual consumer state, предоставляемая host после staged startup transaction.</summary>
public sealed record GalaxyConsumerEntity(string Id, GalaxyConsumerEntityKind Kind, string? CountryId = null,
    string? SpeciesId = null, string? ColonyId = null, string? EconomyPlanetId = null, string? ParentId = null,
    string? FleetId = null, string? SystemId = null, string? BodyId = null, string? DefinitionId = null,
    int ParticipantId = 0, decimal Amount = 0, string? PlanetClass = null, int InitialSize = 0, int FinalSize = 0);
public sealed record GalaxyConsumerBinding(int ParticipantId, string CountryId, string SpeciesId, string ColonyId, string EconomyPlanetId);
public enum GalaxyDeferredDispositionKind { Executed, AcceptedApproximation, Unsupported }
public sealed record GalaxyDeferredDisposition(string RequestId, GalaxyDeferredDispositionKind Kind, string PolicyId, string Evidence);
public sealed class GalaxyConsumerSnapshot
{
    public string WorldFingerprint { get; }
    public string SettingsFingerprint { get; }
    public string ContentFingerprint { get; }
    public string ActorPolicyId { get; }
    public IReadOnlyList<GalaxyConsumerEntity> Entities { get; }
    public IReadOnlyList<GalaxyConsumerBinding> Bindings { get; }
    public IReadOnlyList<GalaxyDeferredDisposition> Dispositions { get; }
    public IReadOnlySet<string> Definitions { get; }
    public GalaxyConsumerSnapshot(string worldFingerprint, string settingsFingerprint, string contentFingerprint,
        IEnumerable<GalaxyConsumerEntity> entities, IEnumerable<GalaxyConsumerBinding> bindings,
        IEnumerable<GalaxyDeferredDisposition> dispositions, IEnumerable<string> definitions, string actorPolicyId)
    {
        WorldFingerprint = worldFingerprint; SettingsFingerprint = settingsFingerprint; ContentFingerprint = contentFingerprint; ActorPolicyId = actorPolicyId;
        Entities = GalaxyWorldSnapshot.Freeze(entities); Bindings = GalaxyWorldSnapshot.Freeze(bindings);
        Dispositions = GalaxyWorldSnapshot.Freeze(dispositions); Definitions = definitions.ToFrozenSet(StringComparer.Ordinal);
    }
}
public sealed record GalaxyPlayValidation(bool Accepted, string Readiness, IReadOnlyList<string> Errors);

/// <summary>Не исполняет startup: проверяет реальные сущности/ссылки consumer projection. Host-owned, не клиентские receipts.</summary>
public static class GalaxyBootstrapValidation
{
    public const string PrototypeOmissionPolicy = "PrototypeContentOmissionsV1";
    public const string NavigationActorPolicy = "PrototypeNavigationActorsV1";
    public static GalaxyPlayValidation ValidateForPlay(GalaxyWorldSnapshot world, GalaxyConsumerSnapshot consumer)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(consumer);
        var errors = new List<string>();
        void Require(bool condition, string error) { if (!condition) errors.Add(error); }
        Require(world.WorldFingerprint == consumer.WorldFingerprint && world.Request.SettingsFingerprint == consumer.SettingsFingerprint &&
            world.Request.ContentFingerprint == consumer.ContentFingerprint, "fingerprint-mismatch");
        Require(consumer.ActorPolicyId == NavigationActorPolicy, "unsupported-actor-policy");
        var entities = new Dictionary<string, GalaxyConsumerEntity>(StringComparer.Ordinal);
        foreach (var entity in consumer.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Id) || !entities.TryAdd(entity.Id, entity)) errors.Add("duplicate-or-empty-entity");
            if (!Enum.IsDefined(entity.Kind)) errors.Add("invalid-entity-kind");
        }
        bool Is(string? id, GalaxyConsumerEntityKind kind) => id is not null && entities.TryGetValue(id, out var e) && e.Kind == kind;
        var systemIds = world.Systems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var bodyIds = world.Bodies.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var bodySystems = world.Bodies.ToDictionary(b => b.Id, b => b.SystemId, StringComparer.Ordinal);
        foreach (var entity in entities.Values)
        {
            if (entity.Kind != GalaxyConsumerEntityKind.Country) Require(Is(entity.CountryId, GalaxyConsumerEntityKind.Country), "missing-owner:" + entity.Id);
            if (entity.CountryId is not null) Require(Is(entity.CountryId, GalaxyConsumerEntityKind.Country), "dangling-country:" + entity.Id);
            if (entity.SpeciesId is not null) Require(Is(entity.SpeciesId, GalaxyConsumerEntityKind.Species), "dangling-species:" + entity.Id);
            if (entity.ColonyId is not null) Require(Is(entity.ColonyId, GalaxyConsumerEntityKind.Colony), "dangling-colony:" + entity.Id);
            if (entity.EconomyPlanetId is not null) Require(Is(entity.EconomyPlanetId, GalaxyConsumerEntityKind.EconomyPlanet), "dangling-economy:" + entity.Id);
            if (entity.FleetId is not null) Require(Is(entity.FleetId, GalaxyConsumerEntityKind.Fleet), "dangling-fleet:" + entity.Id);
            if (entity.ParentId is not null) Require(entities.ContainsKey(entity.ParentId), "dangling-parent:" + entity.Id);
            if (entity.SystemId is not null) Require(systemIds.Contains(entity.SystemId), "dangling-system:" + entity.Id);
            if (entity.BodyId is not null) Require(bodyIds.Contains(entity.BodyId), "dangling-body:" + entity.Id);
            if (entity.BodyId is not null && entity.SystemId is not null && bodySystems.TryGetValue(entity.BodyId, out var bodySystem))
                Require(entity.SystemId == bodySystem, "body-system-mismatch:" + entity.Id);
            if (entity.Kind is GalaxyConsumerEntityKind.District or GalaxyConsumerEntityKind.Building or GalaxyConsumerEntityKind.Zone or GalaxyConsumerEntityKind.Ship)
                Require(entity.DefinitionId is not null && consumer.Definitions.Contains(entity.DefinitionId), "missing-definition:" + entity.Id);
            if (entity.Kind is GalaxyConsumerEntityKind.District or GalaxyConsumerEntityKind.Building)
                Require(Is(entity.ParentId, GalaxyConsumerEntityKind.EconomyPlanet), "invalid-economic-parent:" + entity.Id);
            if (entity.Kind == GalaxyConsumerEntityKind.Zone) Require(Is(entity.ParentId, GalaxyConsumerEntityKind.District), "invalid-zone-parent:" + entity.Id);
            if (entity.ParentId is not null && entities.TryGetValue(entity.ParentId, out var parent))
                Require(parent.CountryId == entity.CountryId, "parent-owner-mismatch:" + entity.Id);
            if (entity.Kind is GalaxyConsumerEntityKind.Fleet or GalaxyConsumerEntityKind.Ship)
                Require(entity.SystemId is not null && systemIds.Contains(entity.SystemId), "missing-actor-system:" + entity.Id);
            if (entity.Kind == GalaxyConsumerEntityKind.District) Require(entity.Amount > 0, "invalid-district-level:" + entity.Id);
        }
        Require(consumer.Bindings.Count == world.Homes.Count && consumer.Bindings.Select(b => b.ParticipantId).Distinct().Count() == world.Homes.Count &&
            consumer.Bindings.Select(b => b.CountryId).Distinct(StringComparer.Ordinal).Count() == world.Homes.Count &&
            consumer.Bindings.Select(b => b.SpeciesId).Distinct(StringComparer.Ordinal).Count() == world.Homes.Count &&
            consumer.Bindings.Select(b => b.ColonyId).Distinct(StringComparer.Ordinal).Count() == world.Homes.Count &&
            consumer.Bindings.Select(b => b.EconomyPlanetId).Distinct(StringComparer.Ordinal).Count() == world.Homes.Count, "start-bijection");
        Require(entities.Values.Count(e => e.Kind == GalaxyConsumerEntityKind.Country) == world.Homes.Count, "country-count");
        foreach (var home in world.Homes)
        {
            var bindings = consumer.Bindings.Where(b => b.ParticipantId == home.ParticipantId).ToArray();
            if (bindings.Length != 1) { errors.Add("missing-player-binding"); continue; }
            var binding = bindings[0];
            if (!Is(binding.CountryId, GalaxyConsumerEntityKind.Country) || !Is(binding.SpeciesId, GalaxyConsumerEntityKind.Species) ||
                !Is(binding.ColonyId, GalaxyConsumerEntityKind.Colony) || !Is(binding.EconomyPlanetId, GalaxyConsumerEntityKind.EconomyPlanet))
            { errors.Add("missing-start-entity"); continue; }
            var country = entities[binding.CountryId]; var species = entities[binding.SpeciesId];
            var colony = entities[binding.ColonyId]; var economy = entities[binding.EconomyPlanetId];
            Require(country.ParticipantId == home.ParticipantId && country.SpeciesId == species.Id && country.ColonyId == colony.Id &&
                country.DefinitionId == home.StartupProfileId, "country-start-links");
            Require(species.CountryId == country.Id && colony.CountryId == country.Id && colony.SpeciesId == species.Id &&
                colony.EconomyPlanetId == economy.Id && economy.CountryId == country.Id && economy.ColonyId == colony.Id, "ownership-start-links");
            Require(colony.BodyId == home.BodyId && colony.SystemId == home.SystemId && colony.PlanetClass == home.ClassId &&
                colony.InitialSize == home.Size && colony.FinalSize > 0 && economy.Amount > 0, "home-economy-state");
            var districts = entities.Values.Where(e => e.Kind == GalaxyConsumerEntityKind.District && e.ParentId == economy.Id && e.CountryId == country.Id && e.Amount > 0).ToArray();
            Require(districts.Length > 0, "missing-start-districts");
            Require(entities.Values.Any(e => e.Kind == GalaxyConsumerEntityKind.Building && e.ParentId == economy.Id && e.CountryId == country.Id), "missing-start-buildings");
            Require(entities.Values.Any(e => e.Kind == GalaxyConsumerEntityKind.Zone && e.CountryId == country.Id && districts.Any(d => d.Id == e.ParentId)), "missing-start-zones");
            var fleets = entities.Values.Where(e => e.Kind == GalaxyConsumerEntityKind.Fleet && e.CountryId == country.Id && e.SystemId == home.SystemId).ToArray();
            Require(fleets.Any(f => entities.Values.Any(s => s.Kind == GalaxyConsumerEntityKind.Ship && s.FleetId == f.Id && s.CountryId == country.Id && s.SystemId == home.SystemId && s.Amount > 0)), "missing-playable-actor");
        }
        foreach (var ship in entities.Values.Where(e => e.Kind == GalaxyConsumerEntityKind.Ship))
        {
            Require(Is(ship.FleetId, GalaxyConsumerEntityKind.Fleet), "ship-without-fleet");
            if (Is(ship.FleetId, GalaxyConsumerEntityKind.Fleet))
            { var fleet = entities[ship.FleetId!]; Require(fleet.CountryId == ship.CountryId && fleet.SystemId == ship.SystemId && ship.Amount > 0, "ship-fleet-mismatch"); }
        }
        var requests = world.Deferred.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        Require(consumer.Dispositions.Count == requests.Count && consumer.Dispositions.Select(d => d.RequestId).Distinct(StringComparer.Ordinal).Count() == requests.Count &&
            consumer.Dispositions.All(d => requests.Contains(d.RequestId)), "deferred-disposition-bijection");
        foreach (var disposition in consumer.Dispositions)
            Require(!string.IsNullOrWhiteSpace(disposition.Evidence) && !string.IsNullOrWhiteSpace(disposition.PolicyId) &&
                (disposition.Kind == GalaxyDeferredDispositionKind.Executed || disposition.Kind == GalaxyDeferredDispositionKind.AcceptedApproximation &&
                    world.Request.Settings.Profile == GalaxyContentProfile.PrototypeOrdinaryV1 && disposition.PolicyId == PrototypeOmissionPolicy), "unsupported-deferred:" + disposition.RequestId);
        return new(errors.Count == 0, errors.Count == 0 ? "PrototypePlayableConsumerValidated" : "ReadyForBootstrap", GalaxyWorldSnapshot.Freeze(errors.Distinct(StringComparer.Ordinal)));
    }
}
