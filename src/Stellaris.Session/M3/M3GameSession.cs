using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Session.M3.Construction;
using Stellaris.Core.Research;

namespace Stellaris.Session.M3;

/// <summary>Read-only runtime observation; no participant data or diagnostic state.</summary>
public readonly record struct M3TimingObservation(string Phase, bool Paused, int SpeedDaysPerSecond, int ElapsedDay, long CommandSequence);

/// <summary>Изолированная дневная сессия M3; все обращения сериализованы.</summary>
public sealed partial class M3GameSession
{
    private static readonly string[] TRAIT_IDS = ["trait_agrarian", "trait_industrious", "trait_ingenious"];
    private static readonly string[] JOB_IDS = ["technician", "miner", "farmer"];
    private static readonly string[] BUILDING_IDS = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    private readonly object _gate = new();
    private readonly EconomyProjection _content;
    private readonly Dictionary<string, TraitWorkforceProjection> _traitCatalog;
    private readonly Dictionary<string, BuildingDefinition> _buildingCatalog;
    private EconomyWorld _world;
    private M3AssetManifest _manifest;
    private readonly M3GalaxyRuntime? _galaxy;
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<string>, M3AssetManifest>? _galaxyManifest;
    private readonly Action<M3GalaxyStart, M3AssetManifest>? _galaxyValidate;
    private M3GalaxySettings? _galaxySettings;
    private M3GalaxyStart? _galaxyCandidate;
    private long _lobbyRevision = 1;
    private bool _startPending;
    private readonly List<Participant> _participants = [];
    private int _nextParticipantId = 1;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly CatalogView _catalog;
    private long _sequence;
    private long _commandSequence;
    private long _tick;
    private string _phase = "lobby";
    private bool _paused = true;
    private int _speed = 1;
    private int? _host;
    private TimeSpan _lastNow;
    private TimeSpan? _nextTick;
    private SpaceWorld? _space;
    private IReadOnlySet<string> _systems = new HashSet<string>(StringComparer.Ordinal);
    private string? _lastError;
    private double _lastTickMilliseconds;
    private readonly ConstructionProjection? _construction;
    private readonly bool _constructionImportAttempted;
    private readonly M3StartupCatalog? _startup;
    private readonly M3BaselineEconomyProfile? _sourceEconomy;
    private readonly M3RareDepositResearchPolicy? _rareDepositResearchPolicy;
    private readonly M3ConstructionContent? _constructionContent;
    private readonly TechnologyCatalog? _researchCatalog;
    private readonly IReadOnlySet<string> _technologyKeys;
    private readonly Func<int, IReadOnlySet<string>>? _researchedTechnologies;
    private IReadOnlyDictionary<string, M3StartupColony> _startupColonies = new Dictionary<string, M3StartupColony>(StringComparer.Ordinal);
    private readonly Dictionary<string, ConstructionLiveRuntime> _constructionRuntimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, M3ConstructionAdapter> _constructionAdapters = new(StringComparer.Ordinal);

    public M3GameSession(EconomyProjection content, IReadOnlyDictionary<string, TraitWorkforceProjection> traitCatalog, M3AssetManifest manifest, ConstructionProjection? construction = null, bool constructionImportAttempted = false, M3StartupCatalog? startup = null, M3BaselineEconomyProfile? sourceEconomy = null, M3ConstructionContent? constructionContent = null, TechnologyCatalog? researchCatalog = null, IReadOnlySet<string>? technologyKeys = null, Func<int, IReadOnlySet<string>>? researchedTechnologies = null, M3GalaxyRuntime? galaxy = null, Func<IReadOnlyList<string>, IReadOnlyList<string>, M3AssetManifest>? galaxyManifest = null, Action<M3GalaxyStart, M3AssetManifest>? galaxyValidate = null, M3RareDepositResearchPolicy? rareDepositResearchPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _manifest = manifest;
        _rareDepositResearchPolicy = rareDepositResearchPolicy;
        if (rareDepositResearchPolicy is not null)
        {
            var files = rareDepositResearchPolicy.Manifest.Files.Select(file => (Path: file.Path, file.Sha256))
                .Concat(sourceEconomy?.Catalog.Sources.Select(file => (Path: file.RelativePath, file.Sha256)) ?? [])
                .Concat(researchCatalog?.Manifest?.Files.Select(file => (Path: file.Path, file.Sha256)) ?? []);
            if (files.GroupBy(file => file.Path, StringComparer.Ordinal).Any(group => group.Select(file => file.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1))
            { throw new InvalidDataException("Rare research policy source snapshot отличается от economy/research."); }
        }
        _galaxy = galaxy;
        _galaxyManifest = galaxyManifest;
        _galaxyValidate = galaxyValidate;
        _galaxySettings = galaxy?.DefaultSettings;
        _construction = construction;
        _startup = startup;
        _sourceEconomy = sourceEconomy;
        _constructionContent = constructionContent;
        _researchCatalog = researchCatalog;
        _technologyKeys = (technologyKeys ?? new HashSet<string>()).ToFrozenSet(StringComparer.Ordinal);
        _researchedTechnologies = researchedTechnologies;
        _constructionImportAttempted = constructionImportAttempted || construction is not null;
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(traitCatalog);
        if (!content.Jobs.Select(job => job.Id).Order(StringComparer.Ordinal).SequenceEqual(JOB_IDS.Order(StringComparer.Ordinal)) || content.Jobs.Any(job => job.WorkforcePerJob != 100))
        {
            throw new ArgumentException("M3 требует три профессии M1 с WorkforcePerJob=100.", nameof(content));
        }
        if (!content.Buildings.Select(building => building.Id).Order(StringComparer.Ordinal).SequenceEqual(BUILDING_IDS.Order(StringComparer.Ordinal)))
        {
            throw new ArgumentException("M3 требует три здания M1.", nameof(content));
        }
        if (!traitCatalog.Keys.Order(StringComparer.Ordinal).SequenceEqual(TRAIT_IDS))
        {
            throw new ArgumentException("Каталог M3 требует только trait_agrarian, trait_industrious, trait_ingenious.", nameof(traitCatalog));
        }
        if (content.Resources.Any(resource => resource.Capacity < 1000))
        {
            throw new ArgumentException("Ёмкость ресурсов M3 должна допускать начальные запасы 1000.", nameof(content));
        }
        _content = content with { Resources = Array.AsReadOnly(content.Resources.ToArray()), Jobs = Array.AsReadOnly(content.Jobs.ToArray()), Categories = Array.AsReadOnly(content.Categories.ToArray()) };
        _traitCatalog = traitCatalog.ToDictionary(pair => pair.Key, pair => pair.Value with { JobMultipliers = Copy(pair.Value.JobMultipliers) }, StringComparer.Ordinal);
        _buildingCatalog = content.Buildings.ToDictionary(building => building.Id, building => building with { Upkeep = Copy(building.Upkeep), Modifiers = Array.AsReadOnly(building.Modifiers.ToArray()) }, StringComparer.Ordinal);
        foreach (var building in _buildingCatalog.Values)
        {
            if (building.Upkeep.Any(amount => amount.Value < 0 || !content.Resources.Any(resource => resource.Id == amount.Key)) ||
                building.Modifiers.Any(modifier => !content.Resources.Any(resource => resource.Id == modifier.Resource) || !content.Categories.Any(category => category.Id == modifier.Category) || modifier.Operation is not "produces" and not "upkeep"))
            {
                throw new ArgumentException("Здание M3 содержит недопустимые расходы или модификаторы.", nameof(content));
            }
        }
        foreach (var item in _traitCatalog)
        {
            if (item.Value.JobMultipliers.Keys.Any(id => !JOB_IDS.Contains(id)) || item.Value.JobMultipliers.Values.Any(value => value < 0))
            {
                throw new ArgumentException("Каталог traits содержит неподдерживаемые доли Workforce.", nameof(traitCatalog));
            }
        }
        _world = sourceEconomy?.CreateWorld() ?? new EconomyWorld(content.Resources, content.Jobs, content.Categories);
        _catalog = new CatalogView(
            Array.AsReadOnly(new[] { new CatalogItem("technician", "Техники", "common/pop_jobs/03_worker_jobs.txt"), new CatalogItem("miner", "Шахтёры", "common/pop_jobs/03_worker_jobs.txt"), new CatalogItem("farmer", "Фермеры", "common/pop_jobs/03_worker_jobs.txt") }),
            Array.AsReadOnly(new[] { new CatalogItem("building_energy_grid", "Энергосеть", "common/buildings/03_resource_buildings.txt"), new CatalogItem("building_mineral_purification_plant", "Очистка минералов", "common/buildings/03_resource_buildings.txt"), new CatalogItem("building_food_processing_facility", "Переработка пищи", "common/buildings/03_resource_buildings.txt") }),
            Array.AsReadOnly(new[] { new CatalogItem("trait_agrarian", "Аграрии", "common/traits/04_species_traits.txt"), new CatalogItem("trait_industrious", "Трудолюбивые", "common/traits/04_species_traits.txt"), new CatalogItem("trait_ingenious", "Изобретательные", "common/traits/04_species_traits.txt") }));
    }

    public M3JoinGrant Join(string name, string role, string? token, TimeSpan now)
    {
        lock (_gate)
        {
            CheckNow(now);
            if (role is not "player" and not "observer")
            {
                throw new ArgumentException("Неизвестная роль.", nameof(role));
            }
            var sequence = checked(_sequence + 1);
            var connectionId = NewSecret();
            if (token != null)
            {
                var returning = _participants.SingleOrDefault(participant => participant.Token == token);
                if (returning == null || returning.Connected || (returning.Role == "player") != (role == "player"))
                {
                    throw new InvalidOperationException("Возврат невозможен: токен недействителен или уже используется.");
                }
                var nextLobbyRevision = returning.Role == "player" && _phase == "lobby" ? checked(_lobbyRevision + 1) : (long?)null;
                returning.ConnectionId = connectionId;
                returning.Connected = true;
                returning.DisconnectedAt = null;
                returning.Afk = false;
                returning.AssetReady = false;
                returning.Ready = false;
                returning.InspectedSystemId = null;
                if (nextLobbyRevision is not null) InvalidateGalaxy(nextLobbyRevision);
                _sequence = sequence;
                return Grant(returning);
            }
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 32 || name.Any(char.IsControl))
            {
                throw new ArgumentException("Имя должно содержать 1–32 печатных символа.", nameof(name));
            }
            if (role == "player" && _phase != "lobby")
            {
                throw new InvalidOperationException("Вход игрока недоступен.");
            }
            if (role == "observer" && (_phase != "lobby" || _participants.Any(participant => participant.Role != "player")))
            {
                throw new InvalidOperationException("Место наблюдателя недоступно; разрешение выдаётся в лобби.");
            }
            var participantId = _nextParticipantId;
            var nextParticipantId = checked(_nextParticipantId + 1);
            var participant = new Participant(participantId, name.Trim(), role == "player" ? role : "pendingObserver", NewSecret(), connectionId);
            var joinedLobbyRevision = role == "player" && _phase == "lobby" ? checked(_lobbyRevision + 1) : (long?)null;
            if (role == "player")
            {
                if (_sourceEconomy is null)
                {
                    foreach (var job in _content.Jobs)
                    {
                        participant.Jobs.Add(job.Id, 2);
                        participant.BonusMultipliers.Add(job.Id, 0);
                    }
                }
                else
                {
                    foreach (var job in _sourceEconomy.Jobs.Keys)
                    {
                        participant.Jobs.Add(job, 0);
                        participant.BonusMultipliers.Add(job, 0);
                    }
                }
            }
            var lobbyWorld = role == "player" && _phase == "lobby" ? BuildLobbyWorld(_participants.Append(participant)) : null;
            _participants.Add(participant);
            if (lobbyWorld is not null)
            {
                _world = lobbyWorld.World;
                ApplyLobbyWorldBindings(lobbyWorld.Bindings);
            }
            if (role == "player") _host ??= participant.Id;
            _nextParticipantId = nextParticipantId;
            if (joinedLobbyRevision is not null) InvalidateGalaxy(joinedLobbyRevision);
            _sequence = sequence;
            return Grant(participant);
        }
    }

    public void Disconnect(int id, string connectionId, TimeSpan now)
    {
        lock (_gate)
        {
            CheckNow(now);
            var participant = _participants.SingleOrDefault(item => item.Id == id);
            if (participant is null || !participant.Connected || participant.ConnectionId != connectionId)
            {
                return;
            }
            var sequence = checked(_sequence + 1);
            if (_phase == "lobby")
            {
                var wasPlayer = participant.Role == "player";
                var lobbyWorld = wasPlayer ? BuildLobbyWorld(_participants.Where(player => player.Id != participant.Id)) : null;
                foreach (var remaining in _participants.Where(player => player.Id != participant.Id))
                {
                    if (remaining.ObserverViewParticipantId == participant.Id) remaining.ObserverViewParticipantId = null;
                }
                _participants.Remove(participant);
                if (wasPlayer)
                {
                    _world = lobbyWorld!.World;
                    ApplyLobbyWorldBindings(lobbyWorld.Bindings);
                    InvalidateGalaxy(checked(_lobbyRevision + 1));
                    if (_host == participant.Id || _host is null || !_participants.Any(player => player.Id == _host && player.Role == "player" && player.Connected))
                    {
                        _host = _participants.Where(player => player.Role == "player" && player.Connected).Select(player => (int?)player.Id).Min();
                    }
                }
                _sequence = sequence;
                return;
            }
            participant.Connected = false;
            participant.AssetReady = false;
            participant.Ready = false;
            participant.DisconnectedAt = now;
            _sequence = sequence;
        }
    }

    public M3CommandResult Execute(int id, string connectionId, M3ClientCommand command, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            CheckNow(now);
            var participant = _participants.SingleOrDefault(item => item.Id == id);
            if (participant is null || !participant.Connected || participant.ConnectionId != connectionId)
            {
                return new(command.CommandId ?? "", false, "OUT_OF_SCOPE", "Соединение неактивно.");
            }
            if (string.IsNullOrWhiteSpace(command.CommandId) || command.CommandId.Length > 64)
            {
                return new(command.CommandId ?? "", false, "INVALID_COMMAND", "ID команды должен содержать 1–64 символа.");
            }
            var fingerprint = JsonSerializer.Serialize(command, new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals });
            if (participant.Commands.TryGetValue(command.CommandId, out var previous))
            {
                return previous.Fingerprint == fingerprint ? previous.Result : new(command.CommandId, false, "ID_CONFLICT", "ID уже использован для другого содержимого.");
            }
            if (participant.Commands.Count >= 10000)
            {
                return new(command.CommandId, false, "RATE_LIMITED", "Лимит 10000 команд достигнут.");
            }
            M3CommandResult result;
            try
            {
                ValidateFields(command);
                var commandSequence = checked(_commandSequence + 1);
                var sequence = checked(_sequence + 1);
                var refundLosses = Apply(participant, command, now, sequence);
                _commandSequence = commandSequence;
                _sequence = sequence;
                result = new(command.CommandId, true, "OK", "Принято.", commandSequence, _tick, sequence, refundLosses);
            }
            catch (CommandRejected exception)
            {
                result = new(command.CommandId, false, exception.Code, exception.Message);
            }
            catch (SpatialRejected exception)
            {
                result = new(command.CommandId, false, exception.Code, exception.Message);
            }
            catch (OverflowException)
            {
                result = new(command.CommandId, false, "INVALID_TARGET", "Численные параметры превышают допустимый диапазон.");
            }
            catch (Exception exception) when (_galaxy is not null && command.Type is ("prepareGalaxy" or "start") &&
                exception is (InvalidOperationException or ArgumentException or IOException or NotSupportedException or AssetManifestException or GeneratedWorldVisualException))
            {
                result = new(command.CommandId, false, "DEPENDENCY_UNRESOLVED", "Не удалось атомарно подготовить или запустить текущую галактику.");
            }
            participant.Commands.Add(command.CommandId, new CachedCommand(fingerprint, result));
            return result;
        }
    }

    public M3TimingObservation ObserveTiming()
    {
        lock (_gate)
        {
            return new(_phase, _paused, _speed, _space?.ElapsedDay ?? 0, _commandSequence);
        }
    }

    public void Tick(TimeSpan now)
    {
        lock (_gate)
        {
            CheckNow(now);
            foreach (var participant in _participants)
            {
                if (!participant.Connected && !participant.Afk && participant.DisconnectedAt is { } disconnected && now - disconnected >= TimeSpan.FromSeconds(60))
                {
                    var sequence = checked(_sequence + 1);
                    participant.Afk = true;
                    _sequence = sequence;
                }
            }
            if (_phase == "running" && !_paused && _nextTick is { } deadline && now >= deadline)
            {
                AdvanceDay(now, checked(_sequence + 1));
            }
        }
    }

    public long SnapshotSequence
    {
        get { lock (_gate) { return _sequence; } }
    }

    public bool IsCurrentConnection(int id, string connectionId)
    {
        lock (_gate)
        {
            return _participants.Any(participant => participant.Id == id && participant.Connected && participant.ConnectionId == connectionId);
        }
    }

    private LobbyWorldState BuildLobbyWorld(IEnumerable<Participant> members)
    {
        var world = _sourceEconomy?.CreateWorld() ?? new EconomyWorld(_content.Resources, _content.Jobs, _content.Categories);
        var bindings = new Dictionary<int, (EntityId Country, EntityId Planet)>();
        foreach (var member in members.Where(player => player.Role == "player"))
        {
            var country = world.CreateCountry();
            var planet = world.CreatePlanet(country);
            if (_sourceEconomy is null)
            {
                world.AddGroup(planet, 600, 1);
                foreach (var resource in _content.Resources) world.SetBalance(country, resource.Id, 1000);
                foreach (var job in member.Jobs) world.SetJobs(planet, job.Key, job.Value);
            }
            foreach (var bonus in member.BonusMultipliers) world.SetJobBonusWorkforceMultiplier(planet, bonus.Key, bonus.Value);
            var upkeep = _content.Resources.ToDictionary(resource => resource.Id, _ => 0m, StringComparer.Ordinal);
            var modifiers = new List<Modifier>();
            foreach (var buildingId in member.Buildings)
            {
                var building = _buildingCatalog[buildingId];
                modifiers.AddRange(building.Modifiers);
                foreach (var amount in building.Upkeep) upkeep[amount.Key] = checked(upkeep[amount.Key] + amount.Value);
            }
            world.SetModifiers(planet, modifiers);
            world.SetBuildingUpkeep(planet, upkeep);
            bindings.Add(member.Id, (country, planet));
        }
        return new LobbyWorldState(world, bindings);
    }

    private void ApplyLobbyWorldBindings(IReadOnlyDictionary<int, (EntityId Country, EntityId Planet)> bindings)
    {
        foreach (var member in _participants.Where(player => player.Role == "player"))
        {
            var binding = bindings[member.Id];
            member.Country = binding.Country;
            member.Planet = binding.Planet;
        }
    }

    private sealed record LobbyWorldState(EconomyWorld World, Dictionary<int, (EntityId Country, EntityId Planet)> Bindings);

    public M3SessionView View(int id)
    {
        lock (_gate)
        {
            var viewer = Find(id);
            var perspectiveId = viewer.Role == "observer" ? viewer.ObserverViewParticipantId : id;
            var generalObserver = viewer.Role == "observer" && perspectiveId == null;
            var viewContext = viewer.Role == "pendingObserver" ? new M3ViewContext("pending", null) : generalObserver ? new M3ViewContext("general", null) : new M3ViewContext("player", perspectiveId);
            var participants = _participants.Select(participant => new M3ParticipantView(participant.Id, participant.Name, participant.Role, _host == participant.Id, participant.Connected, participant.Afk, participant.Ready, participant.AssetReady)).ToArray();
            var visible = _participants.Where(participant => participant.Planet != null && (generalObserver || viewer.Role != "pendingObserver" && participant.Id == perspectiveId));
            var planets = visible.Select(participant => new PlanetView(participant.Planet!.Value.Index, participant.Id, $"Планета {participant.Name}",
                _sourceEconomy?.InitialPopulation ?? 600, _sourceEconomy?.InitialPopulation ?? 600,
                Copy(participant.Jobs), Copy(participant.BonusMultipliers), Array.AsReadOnly(participant.Traits.ToArray()), Array.AsReadOnly(participant.Buildings.ToArray()),
                Copy((_sourceEconomy?.Resources.Keys ?? _content.Resources.Select(resource => resource.Id)).ToDictionary(resource => resource, resource => _world.GetBalance(participant.Country!.Value, resource))),
                _world.Month == 0 ? null : _world.GetPlanetReport(participant.Planet.Value), _world.Month == 0 ? null : _world.Month)).ToArray();
            var spatial = _space == null || viewer.Role == "pendingObserver" ? M3Projection.Empty : ProjectSpatial(viewer, perspectiveId ?? id, generalObserver);
            var day = _space?.ElapsedDay ?? 0;
            var date = GameCalendar.FromElapsedDay(day);
            var templates = _participants.Where(participant => participant.Template is not null).ToDictionary(participant => participant.Id, participant => participant.Template!);
            IReadOnlyDictionary<string, M3DevelopmentEconomyView>? economyViews = null;
            IReadOnlyDictionary<string, long>? colonyRevisions = null;
            IReadOnlyDictionary<string, M3DevelopmentColonyView>? constructionViews = null;
            if (_sourceEconomy is not null && _space is not null)
            {
                var snapshots = _space.Colonies.Select(colony => (colony.Id, Participant: _participants.Single(player => player.Id == colony.OwnerParticipantId)))
                    .ToArray();
                economyViews = snapshots.ToDictionary(item => item.Id, item => ProjectSourceEconomy(item.Participant), StringComparer.Ordinal);
                colonyRevisions = snapshots.ToDictionary(item => item.Id, item => item.Participant.ColonyRevision, StringComparer.Ordinal);
                if (_constructionContent is not null)
                {
                    var bank = CreateConstructionBank(_world);
                    constructionViews = _constructionRuntimes.ToDictionary(pair => pair.Key,
                        pair => _constructionAdapters[pair.Key].Project(pair.Value, _space.Colonies.Single(colony => colony.Id == pair.Key).EconomyPlanetId,
                            ConstructionEvaluationFor(_participants.Single(player => player.Id == pair.Value.OwnerId), _startupColonies[pair.Key], pair.Value), bank: bank), StringComparer.Ordinal);
                }
            }
            var development = _constructionImportAttempted ? M3DevelopmentProjection.Project(_construction, spatial, viewContext, templates, _manifest, _startupColonies,
                economyViews, _sourceEconomy?.ProfileId, colonyRevisions, constructionViews) : null;
            if (_galaxy is not null && development is not null) development = M3DevelopmentProjection.CompactCatalog(development);
            var selectedTemplate = viewContext.Mode == "player" ? _participants.SingleOrDefault(participant => participant.Id == viewContext.ParticipantId && participant.Role == "player")?.Template?.Id : null;
            var galaxyView = _galaxy?.Project(_phase == "running" ? "running" : _galaxyCandidate is null ? "unprepared" : "awaitingAssets", _galaxySettings!, _phase == "lobby" && !_startPending && CanPrepareGalaxy(), _galaxyCandidate);
            if (galaxyView is not null) galaxyView = galaxyView with { LobbyRevision = _lobbyRevision, StartPending = _startPending };
            return new(2, _sessionId, _sequence, _phase, _tick, day, new(date.Year, date.Month, date.Day), _world.Month, _paused, _speed, id, Array.AsReadOnly(participants), Array.AsReadOnly(planets), _catalog, new(_manifest.ProfileId, _manifest.ProfileDigest), spatial, _lastError, _lastTickMilliseconds, viewContext, development, new(M3EmpireSetupProjection.Catalog, selectedTemplate), Research: ProjectResearch(visible), Galaxy: galaxyView);
        }
    }

    public M3AssetManifest CurrentManifest
    {
        get { lock (_gate) { return _manifest; } }
    }

    private M3SpatialView ProjectSpatial(Participant viewer, int perspectiveId, bool generalObserver)
    {
        var bodies = _space!.Bodies.Select(body => body.Id).ToHashSet(StringComparer.Ordinal);
        var economyIds = _participants.Where(player => player.Planet is not null).ToDictionary(player => player.Id, player => player.Planet!.Value.Index);
        return _galaxyCandidate is null
            ? M3Projection.Project(_space, perspectiveId, generalObserver, _systems, bodies, economyIds)
            : M3GalaxyProjection.Project(_galaxyCandidate, viewer.InspectedSystemId, perspectiveId, generalObserver, _systems, bodies, economyIds, _space);
    }

    private bool CanPrepareGalaxy()
    {
        var players = _participants.Where(player => player.Role == "player").ToArray();
        return players.Length > 0 && players.All(player => player.Connected && player.Template is not null && player.Planet is not null && player.Ready && player.ReadyLobbyRevision == _lobbyRevision);
    }

    private void InvalidateGalaxy(long? revision = null)
    {
        _lobbyRevision = revision ?? checked(_lobbyRevision + 1);
        _galaxyCandidate = null;
        _startPending = false;
        ResetGalaxyReadiness();
    }

    private void ResetGalaxyReadiness()
    {
        foreach (var player in _participants)
        {
            player.AssetReady = false;
            player.Ready = false;
            player.ReadyLobbyRevision = null;
            player.InspectedSystemId = null;
        }
    }

    private void PrepareGalaxy()
    {
        if (_galaxy is null) throw Reject("OUT_OF_SCOPE", "Генерация не подключена к этой fixture-сессии.");
        if (!CanPrepareGalaxy()) throw Reject("INVALID_PHASE", "Все игроки с пресетами должны быть подключены и подтвердить текущую ревизию лобби.");
        if (_startup is null || _galaxyManifest is null) throw Reject("DEPENDENCY_UNRESOLVED", "Generated startup/asset profile пока недоступны.");
        var players = _participants.Where(player => player.Role == "player").Select(player =>
            new M3GalaxyPlayer(player.Id, player.Planet!.Value.Index, player.Template!.Id)).ToArray();
        var candidate = _galaxy.Prepare(_galaxySettings!, players, (template, home) => _startup.Exact(template, home.ClassId, home.Size));
        var manifest = _galaxyManifest(candidate.ClassIds, candidate.ObjectIds) ?? throw new InvalidOperationException("Generated manifest отсутствует.");
        _galaxyValidate?.Invoke(candidate, manifest);
        // Публикуем обе зависимости только после успеха; старый профиль не fallback.
        _galaxyCandidate = candidate;
        _manifest = manifest;
        foreach (var player in _participants) { player.AssetReady = false; player.InspectedSystemId = null; }
        _startPending = true;
    }

    private void RequireLobbyRevision(M3ClientCommand command)
    {
        if (command.ExpectedLobbyRevision != _lobbyRevision)
            throw Reject("STALE_LOBBY_REVISION", "Подтвердите текущие настройки и состав лобби.");
    }

    private void RequireGalaxyCandidate(M3ClientCommand command, bool requireProfile)
    {
        if (_galaxyCandidate is null || command.GalaxyWorldFingerprint != _galaxyCandidate.Generated.WorldFingerprint ||
            requireProfile && (command.ProfileId != _manifest.ProfileId || command.ProfileDigest != _manifest.ProfileDigest))
            throw Reject("STALE_GALAXY_CANDIDATE", "Подтвердите текущие мир и профиль ассетов.");
    }

    private M3StartupColony LegacyStartup(Participant player, string colonyId)
    {
        var selected = _startup!.Select(player.Template!.Id, RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue));
        return M3StartupColony.Create(colonyId, player.Id, selected.Homeworld, selected.Plan);
    }

    private M3DevelopmentEconomyView ProjectSourceEconomy(Participant participant)
    {
        var planet = participant.Planet!.Value;
        var country = participant.Country!.Value;
        var report = _world.Month == 0 ? null : _world.GetPlanetReport(planet);
        var preview = _world.PreviewPlanetAllocations(planet);
        var jobs = _sourceEconomy!.Jobs.Values.OrderBy(job => job.Id, StringComparer.Ordinal).Select(job =>
        {
            var capacity = participant.SourceJobCapacities.GetValueOrDefault(job.Id);
            var allocation = preview.GetValueOrDefault(job.Id);
            var flow = report?.JobFlows?.GetValueOrDefault(job.Id);
            return new M3DevelopmentEconomyJobView(job.Id, job.EconomicCategory ?? job.JobStratum,
                _sourceEconomy.GetJobImageDefinitionKey(job.Id, _manifest), capacity,
                participant.SourceWorkforceLimits.GetValueOrDefault(job.Id), allocation?.Workforce,
                allocation?.BonusWorkforce, allocation?.EffectiveWorkforce, job.JobStratum,
                M3AssetManifestBuilder.ResolvePlanetImageKey(_manifest, "socialClass", job.JobStratum), job.NameKey, job.DescriptionKey, job.PluralKey, flow?.Produced, flow?.Upkeep);
        }).ToArray();
        var resources = _sourceEconomy.Resources.Values.OrderBy(resource => resource.Id, StringComparer.Ordinal).Select(resource =>
            new M3DevelopmentEconomyResourceView(resource.Id, resource.StoragePolicy, resource.SourceMaximum,
                _world.GetBalance(country, resource.Id), report?.Produced.GetValueOrDefault(resource.Id),
                report?.JobUpkeep.GetValueOrDefault(resource.Id), report?.BuildingUpkeep.GetValueOrDefault(resource.Id),
                report?.DistrictUpkeep.GetValueOrDefault(resource.Id), report?.ZoneUpkeep.GetValueOrDefault(resource.Id),
                report?.Net.GetValueOrDefault(resource.Id), PlanetLocalizationBindings.Get("resource", resource.Id).NameKey,
                PlanetLocalizationBindings.Get("resource", resource.Id).DescriptionKey,
                resource.StoragePolicy == "source-capped" ? _world.GetEffectiveResourceCapacity(country, resource.Id) : null)).ToArray();
        return new M3DevelopmentEconomyView(_sourceEconomy.ProfileId, "prototype-definition-order", _sourceEconomy.InitialPopulation,
            _sourceEconomy.InitialPopulation, report is null ? null : _world.Month, Array.AsReadOnly(jobs), Array.AsReadOnly(resources),
            Array.AsReadOnly(_sourceEconomy.CoverageOmissions.Select(item => new M3DevelopmentCoverageOmission(
                item.DefinitionId, item.TargetId, item.TermKind, item.Reason)).ToArray()),
            Array.AsReadOnly(_sourceEconomy.SourceJobs.Values.OrderBy(job => job.Id, StringComparer.Ordinal).Select(job => new M3DevelopmentJobDefinitionView(
                job.Id, job.EconomicCategory, job.JobStratum, job.NameKey, job.DescriptionKey, job.PluralKey, _sourceEconomy.GetJobImageDefinitionKey(job.Id, _manifest),
                _sourceEconomy.Jobs.ContainsKey(job.Id) ? [] : [new("dependency-unresolved", "job-source-recipe-unresolved:" + job.Id)])).ToArray()));
    }

    private IReadOnlyDictionary<string, decimal>? Apply(Participant participant, M3ClientCommand command, TimeSpan now, long sequence)
    {
        if (_galaxy is not null && command.Type == "assetReady") RequireGalaxyCandidate(command, requireProfile: true);
        var beforeAssets = command.Type is "assetReady" or "pause" or "transferHost" or "approveObserver" or "kickParticipant" ||
            _galaxy is not null && command.Type is ("selectEmpireTemplate" or "setGalaxySettings" or "prepareGalaxy" or "ready" or "start");
        if (!beforeAssets && !participant.AssetReady)
        {
            throw Reject("ASSETS_NOT_READY", "Сначала подтвердите текущий профиль ассетов.");
        }
        switch (command.Type)
        {
            case "setGalaxySettings":
                RequireHost(participant);
                RequireLobby();
                if (_galaxy is null) throw Reject("OUT_OF_SCOPE", "Генерация не подключена к этой fixture-сессии.");
                M3GalaxySettings settings;
                try { settings = _galaxy.ResolveSettings(new(command.GalaxySeed!, command.GalaxyScenarioId!, command.GalaxyShapeId!, command.GalaxyDensity!.Value, command.GalaxyNebulae, command.GalaxyGuaranteedColonies, command.GalaxyGatewayFrequency, command.GalaxyHabitableMultiplier, command.GalaxyWormholePairs)); }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
                { throw Reject("INVALID_GALAXY_SETTINGS", "Настройки галактики не поддерживаются текущим source catalog."); }
                InvalidateGalaxy();
                _galaxySettings = settings;
                break;
            case "prepareGalaxy":
                RequireHost(participant);
                RequireLobby();
                throw Reject("INVALID_COMMAND", "Для генерации все игроки подтверждают Ready, затем хост нажимает Start.");
            case "inspectGalaxySystem":
                RequireRunning();
                RequireGalaxyCandidate(command, requireProfile: false);
                if (participant.Role is not ("player" or "observer") || command.SystemId != "" && !_systems.Contains(command.SystemId!))
                    throw Reject("OUT_OF_SCOPE", "Система недоступна текущей perspective.");
                participant.InspectedSystemId = command.SystemId == "" ? null : command.SystemId;
                break;
            case "selectResearch":
                SelectResearch(participant, command);
                break;
            case "selectEmpireTemplate":
                RequirePlayer(participant);
                RequireLobby();
                ConstructionOrdinaryTemplate template;
                try { template = ConstructionOrdinaryTemplate.Get(command.TemplateId!); }
                catch (ArgumentException) { throw Reject("INVALID_COMMAND", "Unknown accepted ordinary empire template."); }
                if (_galaxy is not null) InvalidateGalaxy();
                participant.Template = template;
                participant.Ready = false;
                break;
            case "enqueueConstruction":
            case "enqueueUpgrade":
            case "enqueueZoneReplacement":
            case "setBuildingEnabled":
            case "demolishBuilding":
            case "clearBlocker":
            case "cancelConstruction":
            case "reorderConstruction":
                return ExecuteConstruction(participant, command);
            case "assetReady":
                if (command.Ready == true && (command.ProfileId != _manifest.ProfileId || command.ProfileDigest != _manifest.ProfileDigest))
                {
                    throw Reject("PROFILE_MISMATCH", "Профиль ассетов не совпадает.");
                }
                participant.AssetReady = command.Ready!.Value;
                if (!participant.AssetReady && _galaxy is null)
                {
                    participant.Ready = false;
                }
                break;
            case "configure":
                RequirePlayer(participant);
                RequireLobby();
                Configure(participant, command);
                break;
            case "observerView":
                if (participant.Role != "observer")
                {
                    throw Reject("OUT_OF_SCOPE", "Only an approved observer can select a perspective.");
                }
                if (command.TargetParticipantId != 0 && !_participants.Any(player => player.Id == command.TargetParticipantId && player.Role == "player"))
                {
                    throw Reject("OUT_OF_SCOPE", "The requested player perspective is unavailable.");
                }
                participant.ObserverViewParticipantId = command.TargetParticipantId == 0 ? null : command.TargetParticipantId;
                participant.InspectedSystemId = null;
                break;
            case "ready":
                RequirePlayer(participant);
                RequireLobby();
                if (_galaxy is not null)
                {
                    RequireLobbyRevision(command);
                    if (participant.Template is null) throw Reject("INVALID_PHASE", "Сначала выберите поддержанный пресет.");
                    if (_startPending && command.Ready == false) { InvalidateGalaxy(); break; }
                    if (_startPending) throw Reject("INVALID_PHASE", "Согласие уже принято для запуска текущего мира.");
                    participant.ReadyLobbyRevision = command.Ready == true ? _lobbyRevision : null;
                }
                participant.Ready = command.Ready!.Value;
                break;
            case "start":
                RequireHost(participant);
                RequireLobby();
                if (_galaxy is not null)
                {
                    RequireLobbyRevision(command);
                    if (!_startPending)
                    {
                        if (command.ProfileId is not null || command.ProfileDigest is not null || command.GalaxyWorldFingerprint is not null)
                            throw Reject("STALE_GALAXY_CANDIDATE", "Первый Start подтверждает только текущую ревизию лобби.");
                        PrepareGalaxy();
                        break;
                    }
                    if (command.ProfileId is null && command.ProfileDigest is null && command.GalaxyWorldFingerprint is null)
                        throw Reject("INVALID_PHASE", "Генерация уже выполнена; ожидаются ассеты текущего мира.");
                    RequireGalaxyCandidate(command, requireProfile: true);
                    if (!CanPrepareGalaxy()) throw Reject("INVALID_PHASE", "Согласие игроков на текущую ревизию утрачено.");
                }
                var players = _participants.Where(player => player.Role == "player").ToArray();
                if (players.Length == 0 || players.Any(player => !player.Connected || !player.Ready || !player.AssetReady))
                {
                    throw Reject("INVALID_PHASE", "Для запуска нужен хотя бы один игрок; все игроки должны быть подключены, готовы и подтвердить импорт.");
                }
                if (_sourceEconomy is not null && (players.Any(player => player.Template is null) || _startup is null))
                {
                    throw Reject("INVALID_PHASE", "Source runtime требует выбранный поддержанный preset и startup catalog для каждого игрока.");
                }
                var space = _galaxy is null ? SpaceWorld.CreateFixture(players.Select(player => player.Id).ToArray()) : _galaxyCandidate!.Space;
                var economyCandidate = _sourceEconomy is null ? _world : _world.Fork();
                Dictionary<string, M3StartupColony>? generatedStartup = null;
                var generatedConstruction = new Dictionary<string, (ConstructionLiveRuntime Runtime, M3ConstructionAdapter Adapter)>(StringComparer.Ordinal);
                var pendingSourceJobs = new Dictionary<int, (Dictionary<string, decimal> Capacity, Dictionary<string, decimal> Limits, Dictionary<string, decimal> Jobs)>();
                if (_startup is not null)
                {
                    var generated = new Dictionary<string, M3StartupColony>(StringComparer.Ordinal);
                    foreach (var player in players.Where(item => item.Template is not null))
                    {
                        var colony = space.Colonies.Single(item => item.OwnerParticipantId == player.Id);
                        var startupColony = _galaxyCandidate is not null ? _galaxyCandidate.Startup[colony.Id] : LegacyStartup(player, colony.Id);
                        var plan = startupColony.Plan;
                        generated.Add(colony.Id, startupColony);
                        if (_sourceEconomy is not null)
                        {
                            _sourceEconomy.InitializeColony(economyCandidate, player.Country!.Value, player.Planet!.Value, player.Template!.Id, plan);
                            IReadOnlyDictionary<string, decimal> currentCapacity = economyCandidate.GetInstalledJobCapacity(player.Planet.Value) ?? new Dictionary<string, decimal>();
                            if (_constructionContent is not null)
                            {
                                var runtime = _constructionContent.Create(startupColony, _sourceEconomy.CreateConstructionContributions(startupColony));
                                var fromBinding = M3ConstructionContent.FromBinding.ActingCountry(plan.Context.IsNomadic);
                                var adapter = new M3ConstructionAdapter(_constructionContent, startupColony, player.Template, fromBinding: fromBinding,
                                    researchedTechnologies: () => player.Research?.Researched ?? _researchedTechnologies?.Invoke(player.Id) ?? player.Template.StartingTechnologies, sapientPopulation: _sourceEconomy.InitialPopulation);
                                currentCapacity = _sourceEconomy.RefreshInstalledGraph(economyCandidate, player.Planet.Value, startupColony, runtime, player.Template.Id,
                                    _constructionContent.Context(startupColony, player.Template, runtime, fromBinding,
                                        player.Research?.Researched ?? player.Template.StartingTechnologies, _sourceEconomy.InitialPopulation,
                                        ownerSpecies: economyCandidate.GetOwnedSpeciesSnapshot(player.Country.Value),
                                        ownerFounderSpeciesId: economyCandidate.GetFounderSpecies(player.Country.Value)?.Id,
                                        ownerCountryEntityId: player.Country.Value,
                                        foreignOwnership: economyCandidate.GetPlanetForeignOwnership(player.Planet.Value),
                                        planetEntityId: player.Planet.Value), _constructionContent.Content.Conditions);
                                generatedConstruction.Add(colony.Id, (runtime, adapter));
                            }
                            var capacities = new Dictionary<string, decimal>(currentCapacity, StringComparer.Ordinal);
                            var limits = capacities.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                            pendingSourceJobs.Add(player.Id, (capacities, limits, limits.ToDictionary(pair => pair.Key, pair => pair.Value / 100m, StringComparer.Ordinal)));
                        }
                    }
                    generatedStartup = generated;
                }
                var systems = space.Systems.Select(system => system.Id).ToHashSet(StringComparer.Ordinal);
                var initialRuntimes = generatedConstruction.ToDictionary(pair => pair.Key, pair => pair.Value.Runtime, StringComparer.Ordinal);
                var researchCandidates = _researchCatalog is null ? new Dictionary<int, ResearchCountry>() : players.Where(player => player.Template is not null)
                    .ToDictionary(player => player.Id, player => CreateResearch(player.Template!, ResearchFactsFor(player.Template!, player.Id, space.ElapsedDay, generatedStartup, initialRuntimes)));
                if (_sourceEconomy is not null)
                {
                    foreach (var player in players.Where(player => researchCandidates.ContainsKey(player.Id)))
                    {
                        var effects = ResearchEffectsFor(player, researchCandidates[player.Id], economyCandidate);
                        foreach (var planet in ResearchPlanets(player, space))
                        { _sourceEconomy.ApplyResearchEffects(economyCandidate, planet, player.Template!.Id, effects); }
                    }
                }
                _space = space;
                foreach (var item in researchCandidates) { players.Single(player => player.Id == item.Key).Research = item.Value; }
                if (_sourceEconomy is not null) { _world = economyCandidate; }
                if (generatedStartup is not null) { _startupColonies = new ReadOnlyDictionary<string, M3StartupColony>(generatedStartup); }
                _constructionRuntimes.Clear();
                _constructionAdapters.Clear();
                foreach (var item in generatedConstruction)
                {
                    _constructionRuntimes.Add(item.Key, item.Value.Runtime);
                    _constructionAdapters.Add(item.Key, item.Value.Adapter);
                }
                foreach (var item in pendingSourceJobs)
                {
                    var player = players.Single(value => value.Id == item.Key);
                    player.SourceJobCapacities = item.Value.Capacity;
                    player.SourceWorkforceLimits = item.Value.Limits;
                    player.Jobs = item.Value.Jobs;
                }
                _systems = systems;
                _phase = "running";
                _startPending = false;
                _paused = true;
                _nextTick = null;
                break;
            case "setJobs":
                RequirePlayer(participant);
                RequireRunning();
                if (_sourceEconomy is not null) { throw Reject("OUT_OF_SCOPE", "Source runtime изменяет WorkforceLimit, а не лабораторные вакансии."); }
                if (command.PlanetId != participant.Planet!.Value.Index)
                {
                    throw Reject("OUT_OF_SCOPE", "Планета недоступна.");
                }
                if (!participant.Jobs.ContainsKey(command.Job!) || command.Count is not { } count || count < 0 || count > 6 || count % 0.5m != 0)
                {
                    throw Reject("INVALID_TARGET", "Допустимо 0–6 вакансий с шагом 0.5.");
                }
                _world.SetJobs(participant.Planet.Value, command.Job!, count);
                participant.Jobs[command.Job!] = count;
                break;
            case "adjustWorkforceLimit":
            case "setWorkforceLimitMin":
            case "setWorkforceLimitMax":
            case "setWorkforceLimit":
                SetSourceWorkforceLimit(participant, command);
                break;
            case "pause":
                RequireHost(participant);
                RequireRunning();
                var pause = command.Paused!.Value;
                var pauseDeadline = pause ? (TimeSpan?)null : now + Interval();
                _paused = pause;
                _nextTick = pauseDeadline;
                break;
            case "speed":
                RequireHost(participant);
                RequireRunning();
                if (command.Speed is not (1 or 2 or 4))
                {
                    throw Reject("INVALID_TARGET", "Допустимые скорости: 1, 2, 4 дня/сек.");
                }
                var speedDeadline = _paused ? (TimeSpan?)null : now + TimeSpan.FromSeconds(1d / command.Speed.Value);
                _speed = command.Speed.Value;
                _nextTick = speedDeadline;
                break;
            case "stepDay":
                RequireHost(participant);
                RequireRunning();
                if (!_paused)
                {
                    throw Reject("INVALID_PHASE", "Шаг доступен только на паузе.");
                }
                if (!AdvanceDay(now, sequence))
                {
                    throw Reject("INVALID_PHASE", _lastError!);
                }
                break;
            case "moveFleetToPoint":
                RequirePlayer(participant);
                RequireRunning();
                _space = _space!.MoveToPoint(participant.Id, command.FleetId!, command.SystemId!, new(command.X!.Value, command.Z!.Value), _systems);
                break;
            case "moveFleetToSystem":
                RequirePlayer(participant);
                RequireRunning();
                _space = _space!.MoveToSystem(participant.Id, command.FleetId!, command.TargetSystemId!, _systems);
                break;
            case "stopFleet":
                RequirePlayer(participant);
                RequireRunning();
                _space = _space!.Stop(participant.Id, command.FleetId!, _systems);
                break;
            case "transferHost":
                RequireHost(participant);
                var target = _participants.SingleOrDefault(player => player.Id == command.TargetParticipantId && player.Role == "player" && player.Connected && player.Id != participant.Id);
                if (target == null)
                {
                    throw Reject("OUT_OF_SCOPE", "Передать права можно подключённому второму игроку.");
                }
                _host = target.Id;
                break;
            case "kickParticipant":
                RequireHost(participant);
                RequireLobby();
                var removal = _participants.SingleOrDefault(player => player.Id == command.TargetParticipantId && player.Connected && player.Id != participant.Id);
                if (removal is null)
                {
                    throw Reject("INVALID_TARGET", "Исключить можно другого подключённого участника лобби.");
                }
                var wasPlayer = removal.Role == "player";
                var lobbyWorld = wasPlayer ? BuildLobbyWorld(_participants.Where(player => player.Id != removal.Id)) : null;
                foreach (var remaining in _participants.Where(player => player.Id != removal.Id))
                {
                    if (remaining.ObserverViewParticipantId == removal.Id) remaining.ObserverViewParticipantId = null;
                }
                _participants.Remove(removal);
                if (wasPlayer)
                {
                    _world = lobbyWorld!.World;
                    ApplyLobbyWorldBindings(lobbyWorld.Bindings);
                    InvalidateGalaxy(checked(_lobbyRevision + 1));
                    if (_host == removal.Id || _host is null || !_participants.Any(player => player.Id == _host && player.Role == "player" && player.Connected))
                    {
                        _host = _participants.Where(player => player.Role == "player" && player.Connected).Select(player => (int?)player.Id).Min();
                    }
                }
                break;
            case "approveObserver":
                RequireHost(participant);
                RequireLobby();
                var observer = _participants.SingleOrDefault(player => player.Id == command.TargetParticipantId && player.Role == "pendingObserver");
                if (observer == null)
                {
                    throw Reject("OUT_OF_SCOPE", "Ожидающий наблюдатель недоступен.");
                }
                observer.Role = "observer";
                break;
        }
        return null;
    }

    private IReadOnlyDictionary<string, decimal>? ExecuteConstruction(Participant participant, M3ClientCommand command)
    {
        RequirePlayer(participant);
        RequireRunning();
        if (_sourceEconomy is null || _constructionContent is null)
        {
            RejectUnresolvedConstruction(participant, command);
            return null;
        }
        if (_space is null || participant.Planet is null || participant.Country is null)
        {
            throw Reject("OUT_OF_SCOPE", "Live construction runtime недоступен.");
        }
        if (command.ColonyId is null || !_constructionRuntimes.TryGetValue(command.ColonyId, out var original)
            || !_constructionAdapters.TryGetValue(command.ColonyId, out var adapter)
            || !_startupColonies.TryGetValue(command.ColonyId, out var startup))
        {
            throw Reject("OUT_OF_SCOPE", "Колония недоступна этому участнику.");
        }
        if (original.OwnerId != participant.Id) { throw Reject("OUT_OF_SCOPE", "Колония недоступна этому участнику."); }
        if (command.ExpectedColonyRevision is < 0) { throw Reject("INVALID_COMMAND", "Revision must be nonnegative."); }
        if (command.ExpectedColonyRevision != participant.ColonyRevision) { throw Reject(command.Type == "enqueueZoneReplacement" ? "ZONE_REPLACEMENT_PREVIEW_CHANGED" : command.Type == "cancelConstruction" && command.RefundPreviewId is not null ? "REFUND_PREVIEW_CHANGED" : "STALE_REVISION", "Revision колонии устарела."); }

        try
        {
            var runtime = original.SynchronizeRevision(participant.ColonyRevision);
            var bank = CreateConstructionBank(_world);
            var actor = new M3ConstructionActor(participant.Id, participant.Role == "player", participant.AssetReady, _phase == "running");
            var transition = adapter.Execute(runtime, bank, actor, command, ConstructionEvaluationFor(participant, startup, runtime));
            if (!transition.Accepted) { throw Reject(transition.Code, "Construction command отклонена: " + transition.Code + "."); }
            var staged = ConstructionLiveTransaction.Stage(transition, bank);
            if (!staged.Accepted || staged.Runtime is null || staged.Bank is null)
            {
                throw Reject(staged.Code, "Construction transaction отклонена: " + staged.Code + ".");
            }

            if (command.Type is "setBuildingEnabled" or "demolishBuilding")
            {
                var refreshed = adapter.RefreshPendingConditions(staged.Runtime, staged.Bank, ConstructionEvaluationFor(participant, startup, staged.Runtime));
                staged = ConstructionLiveTransaction.Stage(refreshed, staged.Bank);
                if (!staged.Accepted || staged.Runtime is null || staged.Bank is null)
                    throw Reject(staged.Code, "Пересчёт условий строительства отклонён; инфраструктура сохранена.");
            }

            var economyCandidate = _world.Fork();
            ApplyConstructionBank(economyCandidate, staged.Bank);
            var capacities = _sourceEconomy.RefreshInstalledGraph(economyCandidate, participant.Planet.Value, startup, staged.Runtime, participant.Template!.Id,
                ConstructionEvaluationFor(participant, startup, staged.Runtime).Context, _constructionContent!.Content.Conditions);
            if (participant.Research is not null)
            { _sourceEconomy.ApplyResearchEffects(economyCandidate, participant.Planet.Value, participant.Template.Id, ResearchEffectsFor(participant, world: economyCandidate)); }
            var limits = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var jobs = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var job in _sourceEconomy.Jobs.Keys)
            {
                var maximum = capacities.GetValueOrDefault(job);
                var previousMaximum = participant.SourceJobCapacities.GetValueOrDefault(job);
                var previousLimit = participant.SourceWorkforceLimits.GetValueOrDefault(job);
                var limit = previousLimit == previousMaximum ? maximum : Math.Min(previousLimit, maximum);
                limits[job] = limit;
                jobs[job] = limit / 100m;
                economyCandidate.SetJobs(participant.Planet.Value, job, jobs[job]);
            }
            var revision = checked(participant.ColonyRevision + 1);
            var synchronized = staged.Runtime.SynchronizeRevision(revision);
            var losses = command.Type == "cancelConstruction" && transition.Code != "duplicate"
                ? M3ConstructionAdapter.RefundPreview(runtime, runtime.Orders[command.OrderId!], bank).Losses : null;

            _world = economyCandidate;
            _constructionRuntimes[command.ColonyId] = synchronized;
            participant.SourceJobCapacities = capacities.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            participant.SourceWorkforceLimits = limits;
            participant.Jobs = jobs;
            participant.ColonyRevision = revision;
            return losses?.Count > 0 ? losses : null;
        }
        catch (CommandRejected) { throw; }
        catch (ResourceStorageUnresolvedException exception)
        {
            throw Reject("resource-storage-unresolved:" + exception.ResourceId, "Предел хранения ресурса не установлен; состояние колонии сохранено.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException or KeyNotFoundException)
        {
            throw Reject("INVALID_TARGET", "Construction candidate отклонён; состояние колонии не изменено.");
        }
    }

    private ConstructionBankSnapshot CreateConstructionBank(EconomyWorld world)
    {
        var accounts = _participants.Where(item => item.Role == "player" && item.Country is not null)
            .ToDictionary(item => item.Id, item => world.GetBalances(item.Country!.Value), EqualityComparer<int>.Default);
        var storage = _sourceEconomy!.Resources.Values.ToDictionary(resource => resource.Id,
            resource => new ConstructionStorage(resource.StoragePolicy == "unlimited-reserve", resource.SourceMaximum), StringComparer.Ordinal);
        var payerStorage = _participants.Where(item => item.Role == "player" && item.Country is not null)
            .ToDictionary(item => item.Id, item => (IReadOnlyDictionary<string, ConstructionStorage>)_sourceEconomy.Resources.Values
                .Where(resource => resource.StoragePolicy == "source-capped").ToDictionary(resource => resource.Id,
                    resource => new ConstructionStorage(false, world.GetEffectiveResourceCapacity(item.Country!.Value, resource.Id)), StringComparer.Ordinal));
        return new(accounts, storage, payerStorage: payerStorage);
    }

    private void ApplyConstructionBank(EconomyWorld world, ConstructionBankSnapshot bank)
    {
        foreach (var participant in _participants.Where(item => item.Role == "player" && item.Country is not null))
        {
            if (!bank.Accounts.TryGetValue(participant.Id, out var balances)) { throw new InvalidDataException("Construction bank account is missing."); }
            foreach (var balance in balances) { world.SetBalance(participant.Country!.Value, balance.Key, balance.Value); }
        }
    }

    private void SetSourceWorkforceLimit(Participant participant, M3ClientCommand command)
    {
        RequirePlayer(participant);
        RequireRunning();
        if (_sourceEconomy is null || _space is null || participant.Planet is null || participant.Country is null)
        {
            throw Reject("OUT_OF_SCOPE", "Source WorkforceLimit runtime недоступен.");
        }
        var colony = _space.Colonies.SingleOrDefault(item => item.Id == command.ColonyId && item.OwnerParticipantId == participant.Id);
        if (colony is null) { throw Reject("OUT_OF_SCOPE", "Колония недоступна этому участнику."); }
        if (command.ExpectedColonyRevision is < 0) { throw Reject("INVALID_COMMAND", "Revision must be nonnegative."); }
        if (command.ExpectedColonyRevision != participant.ColonyRevision) { throw Reject("STALE_REVISION", "Revision колонии устарела."); }
        if (command.Job is null || !participant.SourceJobCapacities.TryGetValue(command.Job, out var maximum))
        {
            throw Reject("INVALID_TARGET", "Неизвестная source job для колонии.");
        }
        decimal target = command.Type switch
        {
            "adjustWorkforceLimit" when command.Count is -1 or 1 => checked(participant.SourceWorkforceLimits[command.Job] + maximum * 0.05m * command.Count.Value),
            "adjustWorkforceLimit" => throw Reject("INVALID_TARGET", "Шаг должен быть -1 или +1."),
            "setWorkforceLimitMin" => 0,
            "setWorkforceLimitMax" => maximum,
            "setWorkforceLimit" when command.Count is { } absolute => absolute,
            _ => throw Reject("INVALID_TARGET", "Не задано допустимое значение WorkforceLimit.")
        };
        target = Math.Clamp(target, 0, maximum);
        if (command.Type == "setWorkforceLimit" && command.Count != target)
        {
            throw Reject("INVALID_TARGET", "Абсолютный WorkforceLimit должен лежать в пределах 0..capacity.");
        }
        var nextRevision = checked(participant.ColonyRevision + 1);
        var constructionCandidate = _constructionRuntimes.TryGetValue(colony.Id, out var runtime)
            ? runtime.SynchronizeRevision(nextRevision) : null;
        _world.SetJobs(participant.Planet.Value, command.Job, target / 100m);
        participant.SourceWorkforceLimits[command.Job] = target;
        participant.Jobs[command.Job] = target / 100m;
        if (constructionCandidate is not null) { _constructionRuntimes[colony.Id] = constructionCandidate; }
        participant.ColonyRevision = nextRevision;
    }

    private bool AdvanceDay(TimeSpan now, long sequence)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            // Всё потенциально ошибочное готовим до commit экономики.
            var tick = checked(_tick + 1);
            var candidate = _space!.AdvanceDay(_space.ElapsedDay, _ => _systems);
            _ = GameCalendar.FromElapsedDay(candidate.ElapsedDay);
            var deadline = _paused ? (TimeSpan?)null : now + Interval();
            var economyCandidate = _world.Fork();
            var constructionCandidates = new Dictionary<string, ConstructionLiveRuntime>(_constructionRuntimes, StringComparer.Ordinal);
            var sourceJobCandidates = new Dictionary<int, (Dictionary<string, decimal> Capacity, Dictionary<string, decimal> Limits, Dictionary<string, decimal> Jobs)>();
            var researchCandidates = new Dictionary<int, ResearchCountry>();
            if (_constructionContent is not null && _sourceEconomy is not null && constructionCandidates.Count > 0)
            {
                var bank = CreateConstructionBank(_world);
                foreach (var colony in candidate.Colonies.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    if (!constructionCandidates.TryGetValue(colony.Id, out var runtime) || !_constructionAdapters.TryGetValue(colony.Id, out var adapter)
                        || !_startupColonies.TryGetValue(colony.Id, out var startup)) { continue; }
                    var participant = _participants.Single(item => item.Id == colony.OwnerParticipantId);
                    runtime = runtime.SynchronizeRevision(participant.ColonyRevision);
                    if (runtime.PendingOrderIds.Count == 0)
                    {
                        // Пустая очередь не создаёт строительных effects/refunds и daily receipt.
                        // Общий day commit ниже синхронизирует actual participant revision.
                        constructionCandidates[colony.Id] = runtime;
                        continue;
                    }
                    var transition = adapter.AdvanceDay(runtime, bank, "m3-day-" + candidate.ElapsedDay.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        evaluation: ConstructionEvaluationFor(participant, startup, runtime, systemRuntimes: constructionCandidates));
                    if (!transition.Accepted) { throw new InvalidOperationException("Construction day rejected: " + transition.Code); }
                    var staged = ConstructionLiveTransaction.Stage(transition, bank);
                    if (!staged.Accepted || staged.Runtime is null || staged.Bank is null) { throw new InvalidOperationException("Construction bank day rejected: " + staged.Code); }
                    var refreshed = adapter.RefreshPendingConditions(staged.Runtime, staged.Bank, ConstructionEvaluationFor(participant, startup, staged.Runtime, systemRuntimes: constructionCandidates));
                    staged = ConstructionLiveTransaction.Stage(refreshed, staged.Bank);
                    if (!staged.Accepted || staged.Runtime is null || staged.Bank is null) { throw new InvalidOperationException("Construction condition day rejected: " + staged.Code); }
                    bank = staged.Bank;
                    constructionCandidates[colony.Id] = staged.Runtime;
                }
                ApplyConstructionBank(economyCandidate, bank);
                foreach (var colony in candidate.Colonies)
                {
                    if (!constructionCandidates.TryGetValue(colony.Id, out var runtime) || !_startupColonies.TryGetValue(colony.Id, out var startup)) { continue; }
                    var participant = _participants.Single(item => item.Id == colony.OwnerParticipantId);
                    var planet = EconomyPlanetFor(colony);
                    var capacities = _sourceEconomy.RefreshInstalledGraph(economyCandidate, planet, startup, runtime, participant.Template!.Id,
                        ConstructionEvaluationFor(participant, startup, runtime, systemRuntimes: constructionCandidates).Context, _constructionContent!.Content.Conditions);
                    if (participant.Research is not null)
                    { _sourceEconomy.ApplyResearchEffects(economyCandidate, planet, participant.Template.Id, ResearchEffectsFor(participant, world: economyCandidate)); }
                    sourceJobCandidates[participant.Id] = PreserveSourceLimits(economyCandidate, participant, capacities, planet);
                }
            }
            if (candidate.ElapsedDay % 30 == 0)
            {
                var oldReserves = _participants.Where(player => player.Research is not null && player.Country is not null)
                    .ToDictionary(player => player.Id, player => ResearchCountry.AreaIds.ToDictionary(area => area, area => economyCandidate.GetBalance(player.Country!.Value, area + "_research"), StringComparer.Ordinal));
                economyCandidate.AdvanceMonth();
                foreach (var player in _participants.Where(player => player.Research is not null && player.Country is not null && player.Planet is not null))
                {
                    var reports = ResearchPlanets(player, candidate).Select(economyCandidate.GetPlanetReport).ToArray();
                    var production = ResearchCountry.AreaIds.ToDictionary(area => area, area => reports.Sum(report => report.Net.GetValueOrDefault(area + "_research")), StringComparer.Ordinal);
                    var multipliers = _sourceEconomy is null ? ResearchCountry.AreaIds.ToDictionary(area => area, _ => 1 + player.Research!.Catalog.NoLeaderPenalty, StringComparer.Ordinal)
                        : ResearchEffectsFor(player, world: economyCandidate).AreaResearchMultipliers;
                    var research = player.Research!.WithFacts(ResearchFactsFor(player.Template!, player.Id, candidate.ElapsedDay, _startupColonies, constructionCandidates))
                        .AdvanceMonth(production, oldReserves[player.Id], multipliers);
                    foreach (var area in ResearchCountry.AreaIds) { economyCandidate.SetBalance(player.Country!.Value, area + "_research", research.Reserves[area]); }
                    if (_sourceEconomy is not null)
                    {
                        var effects = ResearchEffectsFor(player, research.Country, economyCandidate);
                        foreach (var planet in ResearchPlanets(player, candidate))
                        { _sourceEconomy.ApplyResearchEffects(economyCandidate, planet, player.Template!.Id, effects); }
                    }
                    researchCandidates.Add(player.Id, research.Country);
                    if (_sourceEconomy is not null)
                    {
                        foreach (var colony in candidate.Colonies.Where(colony => colony.OwnerParticipantId == player.Id))
                        {
                            if (!constructionCandidates.TryGetValue(colony.Id, out var runtime)) continue;
                            _sourceEconomy.RefreshInstalledCountryResourceCapacities(economyCandidate, EconomyPlanetFor(colony), runtime,
                                ConstructionEvaluationFor(player, _startupColonies[colony.Id], runtime, research.Country, constructionCandidates).Context,
                                _constructionContent!.Content.Conditions);
                        }
                    }
                }
            }

            var revisionCandidates = _participants.Where(item => item.Role == "player")
                .ToDictionary(item => item.Id, item => checked(item.ColonyRevision + 1));
            foreach (var colony in candidate.Colonies)
            {
                if (!constructionCandidates.TryGetValue(colony.Id, out var runtime)) { continue; }
                constructionCandidates[colony.Id] = runtime.SynchronizeRevision(revisionCandidates[colony.OwnerParticipantId]);
            }
            _world = economyCandidate;
            foreach (var item in researchCandidates) { _participants.Single(player => player.Id == item.Key).Research = item.Value; }
            foreach (var item in constructionCandidates) { _constructionRuntimes[item.Key] = item.Value; }
            foreach (var item in sourceJobCandidates)
            {
                var participant = _participants.Single(player => player.Id == item.Key);
                participant.SourceJobCapacities = item.Value.Capacity;
                participant.SourceWorkforceLimits = item.Value.Limits;
                participant.Jobs = item.Value.Jobs;
            }
            foreach (var item in revisionCandidates) { _participants.Single(participant => participant.Id == item.Key).ColonyRevision = item.Value; }
            _space = candidate;
            _tick = tick;
            _nextTick = deadline;
            _lastError = null;
            _sequence = sequence;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException or AggregateException or SpatialRejected)
        {
            _paused = true;
            _nextTick = null;
            _lastError = "Дневной расчёт отклонён; состояние сохранено. " + exception.Message;
            _sequence = sequence;
            return false;
        }
        finally
        {
            _lastTickMilliseconds = timer.Elapsed.TotalMilliseconds;
        }
    }

    private (Dictionary<string, decimal> Capacity, Dictionary<string, decimal> Limits, Dictionary<string, decimal> Jobs) PreserveSourceLimits(EconomyWorld world, Participant participant, IReadOnlyDictionary<string, decimal> capacities, EntityId planet)
    {
        var limits = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var jobs = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var job in _sourceEconomy!.Jobs.Keys)
        {
            var maximum = capacities.GetValueOrDefault(job);
            var previousMaximum = participant.SourceJobCapacities.GetValueOrDefault(job);
            var previousLimit = participant.SourceWorkforceLimits.GetValueOrDefault(job);
            var limit = previousLimit == previousMaximum ? maximum : Math.Min(previousLimit, maximum);
            limits[job] = limit;
            jobs[job] = limit / 100m;
            world.SetJobs(planet, job, jobs[job]);
        }
        return (capacities.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), limits, jobs);
    }

    private void ValidateFields(M3ClientCommand command)
    {
        if (_galaxy is not null && command.Type == "configure")
            throw Reject("PRESET_CONFIGURATION_ONLY", "В этом прототипе доступны только параметры выбранного пресета.");
        string[] allowed = command.Type switch
        {
            "assetReady" => _galaxy is null ? ["Ready", "ProfileId", "ProfileDigest"] : ["Ready", "ProfileId", "ProfileDigest", "GalaxyWorldFingerprint"],
            "configure" => ["Traits", "Buildings"],
            "setGalaxySettings" => ["GalaxySeed", "GalaxyScenarioId", "GalaxyShapeId", "GalaxyDensity", "GalaxyNebulae", "GalaxyGuaranteedColonies", "GalaxyGatewayFrequency", "GalaxyHabitableMultiplier", "GalaxyWormholePairs"],
            "prepareGalaxy" => [],
            "inspectGalaxySystem" => ["SystemId", "GalaxyWorldFingerprint"],
            "selectEmpireTemplate" => ["TemplateId"],
            "selectResearch" => ["Area", "TechnologyId", "ExpectedResearchRevision"],
            "ready" => _galaxy is null ? ["Ready"] : ["Ready", "ExpectedLobbyRevision"],
            "start" => _galaxy is null ? [] : ["ExpectedLobbyRevision", "ProfileId", "ProfileDigest", "GalaxyWorldFingerprint"],
            "stepDay" => [],
            "setJobs" => ["PlanetId", "Job", "Count"],
            "adjustWorkforceLimit" => ["ColonyId", "ExpectedColonyRevision", "Job", "Count"],
            "setWorkforceLimitMin" or "setWorkforceLimitMax" => ["ColonyId", "ExpectedColonyRevision", "Job"],
            "setWorkforceLimit" => ["ColonyId", "ExpectedColonyRevision", "Job", "Count"],
            "pause" => ["Paused"],
            "speed" => ["Speed"],
            "transferHost" or "approveObserver" or "observerView" or "kickParticipant" => ["TargetParticipantId"],
            "moveFleetToPoint" => ["FleetId", "SystemId", "X", "Z"],
            "moveFleetToSystem" => ["FleetId", "TargetSystemId"],
            "stopFleet" => ["FleetId"],
            "enqueueConstruction" or "clearBlocker" => ["ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "OrderId"],
            "enqueueUpgrade" => ["ColonyId", "ExpectedColonyRevision", "SourceDefinitionId", "DefinitionId", "SlotId", "ZoneInstanceId", "OrderId"],
            "enqueueZoneReplacement" => ["ColonyId", "ExpectedColonyRevision", "SourceDefinitionId", "DefinitionId", "SlotId", "ZoneInstanceId", "OrderId", "ZoneReplacementPreviewId", "ConfirmZoneRemoval"],
            "setBuildingEnabled" => ["ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "Enabled"],
            "demolishBuilding" => ["ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "ConfirmDemolition"],
            "cancelConstruction" => ["ColonyId", "ExpectedColonyRevision", "OrderId", "RefundPreviewId", "ConfirmRefundLoss"],
            "reorderConstruction" => ["ColonyId", "ExpectedColonyRevision", "OrderIds"],
            "step" => throw Reject("INVALID_COMMAND", "В M3 используйте stepDay."),
            _ => throw Reject("INVALID_COMMAND", "Неизвестная команда.")
        };
        foreach (var property in typeof(M3ClientCommand).GetProperties())
        {
            if (property.Name is "CommandId" or "Type")
            {
                continue;
            }
            var value = property.GetValue(command);
            if (value != null && !allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Reject("INVALID_COMMAND", "Посторонний аргумент команды.");
            }
            if (allowed.Contains(property.Name, StringComparer.Ordinal) && value == null)
            {
                if (command.Type == "setGalaxySettings" && property.Name is ("GalaxyNebulae" or "GalaxyGuaranteedColonies" or "GalaxyGatewayFrequency" or "GalaxyHabitableMultiplier" or "GalaxyWormholePairs")) continue;
                if (command.Type == "cancelConstruction" && property.Name is ("RefundPreviewId" or "ConfirmRefundLoss")) continue;
                if (_galaxy is not null && property.Name is ("ProfileId" or "ProfileDigest" or "GalaxyWorldFingerprint")) continue;
                throw Reject("INVALID_COMMAND", "Отсутствует обязательный аргумент команды.");
            }
        }
    }

    private void RejectUnresolvedConstruction(Participant participant, M3ClientCommand command)
    {
        RequirePlayer(participant);
        RequireRunning();
        if (_space is null || !_space.Colonies.Any(colony => colony.Id == command.ColonyId && colony.OwnerParticipantId == participant.Id))
        {
            throw Reject("OUT_OF_SCOPE", "The requested colony is unavailable.");
        }
        var ids = new[] { command.ColonyId, command.OrderId, command.DefinitionId, command.SlotId }.Where(id => id is not null);
        if (command.ExpectedColonyRevision < 0 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.Any(char.IsControl)))
        {
            throw Reject("INVALID_COMMAND", "Invalid construction identifier or revision.");
        }
        if (command.ExpectedColonyRevision != 0)
        {
            throw Reject("STALE_REVISION", "The colony construction revision has changed.");
        }
        if (_construction is not null && command.Type != "cancelConstruction" && (command.Type == "clearBlocker" || command.DefinitionId != _construction.Definition.Id))
        {
            throw Reject("UNKNOWN_DEFINITION", "The construction definition is not in the supported catalog.");
        }
        throw Reject("DEPENDENCY_UNRESOLVED", "Construction is unavailable until source context, quote, layout and native scheduling policies are resolved.");
    }

    private void CheckNow(TimeSpan now)
    {
        if (now < _lastNow)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Монотонное время не может идти назад.");
        }
        _lastNow = now;
    }

    private M3JoinGrant Grant(Participant participant)
    {
        return new(participant.Id, participant.Token, _sessionId, participant.ConnectionId, _manifest.ProfileId, _manifest.ProfileDigest);
    }

    private static string NewSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private void Configure(Participant participant, M3ClientCommand command)
    {
        if (command.Traits == null || command.Buildings == null)
        {
            throw Reject("Требуются массивы traits и buildings.");
        }
        var traits = ValidateSelection(command.Traits, _traitCatalog.Keys, "traits");
        var buildings = ValidateSelection(command.Buildings, _buildingCatalog.Keys, "buildings");
        var rates = JOB_IDS.ToDictionary(job => job, _ => 0m, StringComparer.Ordinal);
        var upkeep = _content.Resources.ToDictionary(resource => resource.Id, _ => 0m, StringComparer.Ordinal);
        var modifiers = new List<Modifier>();
        try
        {
            foreach (var trait in traits)
            {
                foreach (var job in JOB_IDS)
                {
                    rates[job] = checked(rates[job] + _traitCatalog[trait].JobMultipliers.GetValueOrDefault(job));
                }
            }
            foreach (var building in buildings.Select(id => _buildingCatalog[id]))
            {
                modifiers.AddRange(building.Modifiers);
                foreach (var amount in building.Upkeep)
                {
                    upkeep[amount.Key] = checked(upkeep[amount.Key] + amount.Value);
                }
            }
        }
        catch (OverflowException)
        {
            throw Reject("Численные настройки превышают допустимый диапазон.");
        }
        _world.SetModifiers(participant.Planet!.Value, modifiers);
        _world.SetBuildingUpkeep(participant.Planet.Value, upkeep);
        foreach (var job in rates)
        {
            _world.SetJobBonusWorkforceMultiplier(participant.Planet.Value, job.Key, job.Value);
        }
        participant.Traits = traits;
        participant.Buildings = buildings;
        participant.BonusMultipliers = rates;
        participant.Ready = false;
    }

    private Participant Find(int id)
    {
        return _participants.SingleOrDefault(participant => participant.Id == id) ?? throw new InvalidOperationException("Участник недоступен.");
    }

    private void RequireHost(Participant participant)
    {
        if (participant.Role != "player" || _host != participant.Id)
        {
            throw Reject("OUT_OF_SCOPE", "Команда доступна только хосту.");
        }
    }

    private static void RequirePlayer(Participant participant)
    {
        if (participant.Role != "player")
        {
            throw Reject("OUT_OF_SCOPE", "Команда доступна только игроку.");
        }
    }

    private void RequireLobby()
    {
        if (_phase != "lobby")
        {
            throw Reject("INVALID_PHASE", "Команда доступна только в лобби.");
        }
    }

    private void RequireRunning()
    {
        if (_phase != "running")
        {
            throw Reject("INVALID_PHASE", "Сессия ещё не запущена.");
        }
    }

    private TimeSpan Interval()
    {
        return TimeSpan.FromSeconds(1d / _speed);
    }

    private static string[] ValidateSelection(string[] selection, IEnumerable<string> allowed, string name)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in selection)
        {
            if (id == null || !allowed.Contains(id, StringComparer.Ordinal) || !set.Add(id))
            {
                throw Reject($"Недопустимый или повторный ID в {name}.");
            }
        }
        return set.Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, decimal> Copy(IReadOnlyDictionary<string, decimal> values)
    {
        return new ReadOnlyDictionary<string, decimal>(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static CommandRejected Reject(string message)
    {
        return new CommandRejected("INVALID_COMMAND", message);
    }

    private static CommandRejected Reject(string code, string message)
    {
        return new CommandRejected(code, message);
    }

    private sealed class Participant(int id, string name, string role, string token, string connectionId)
    {
        public readonly int Id = id;
        public readonly string Name = name;
        public readonly string Token = token;
        public readonly Dictionary<string, CachedCommand> Commands = new(StringComparer.Ordinal);
        public string ConnectionId = connectionId;
        public string Role = role;
        public bool Connected = true;
        public bool Afk;
        public bool Ready;
        public long? ReadyLobbyRevision;
        public bool AssetReady;
        public int? ObserverViewParticipantId;
        public string? InspectedSystemId;
        public TimeSpan? DisconnectedAt;
        public EntityId? Country;
        public EntityId? Planet;
        public Dictionary<string, decimal> Jobs = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> BonusMultipliers = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> SourceJobCapacities = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> SourceWorkforceLimits = new(StringComparer.Ordinal);
        public long ColonyRevision;
        public string[] Traits = [];
        public string[] Buildings = [];
        public ConstructionOrdinaryTemplate? Template;
        public ResearchCountry? Research;
    }

    private sealed record CachedCommand(string Fingerprint, M3CommandResult Result);
    private sealed class CommandRejected(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
