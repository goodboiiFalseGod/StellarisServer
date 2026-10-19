using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;

namespace Stellaris.Session;

public sealed record ClientCommand(string CommandId, string Type, int? PlanetId = null, string? Job = null, decimal? Count = null, bool? Ready = null, string[]? Traits = null, string[]? Buildings = null, int? Speed = null, bool? Paused = null, int? TargetParticipantId = null);
public sealed record JoinGrant(int ParticipantId, string Token, string SessionId);
public sealed record CommandResult(string CommandId, bool Accepted, string Message);
public sealed record ParticipantView(int Id, string Name, string Role, bool IsHost, bool Connected, bool Afk, bool Ready);
public sealed record CatalogItem(string Id, string Name, string Source);
public sealed record CatalogView(IReadOnlyList<CatalogItem> Jobs, IReadOnlyList<CatalogItem> Buildings, IReadOnlyList<CatalogItem> Traits);
public sealed record PlanetView(int Id, int OwnerId, string Name, decimal Population, decimal Workforce, IReadOnlyDictionary<string, decimal> Jobs, IReadOnlyDictionary<string, decimal> BonusMultipliers, IReadOnlyList<string> Traits, IReadOnlyList<string> Buildings, IReadOnlyDictionary<string, decimal> Balances, PlanetReport? Report, int? ReportMonth);
public sealed record SessionView(int Protocol, string SessionId, long Sequence, string Phase, int Month, bool Paused, int Speed, int You, IReadOnlyList<ParticipantView> Participants, IReadOnlyList<PlanetView> Planets, CatalogView Catalog, string? LastError, double LastTickMilliseconds);

/// <summary>Сериализованная сессия лаборатории; внешняя сеть не получает доступа к мировому состоянию.</summary>
public sealed class GameSession
{
    private static readonly string[] TRAIT_IDS = ["trait_agrarian", "trait_industrious", "trait_ingenious"];
    private static readonly string[] JOB_IDS = ["technician", "miner", "farmer"];
    private static readonly string[] BUILDING_IDS = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    private readonly object _gate = new();
    private readonly EconomyProjection _content;
    private readonly Dictionary<string, TraitWorkforceProjection> _traitCatalog;
    private readonly Dictionary<string, BuildingDefinition> _buildingCatalog;
    private readonly EconomyWorld _world;
    private readonly List<Participant> _participants = [];
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly CatalogView _catalog;
    private long _sequence;
    private string _phase = "lobby";
    private bool _paused = true;
    private int _speed = 1;
    private int? _host;
    private DateTimeOffset? _nextTick;
    private string? _lastError;
    private double _lastTickMilliseconds;

    public GameSession(EconomyProjection content, IReadOnlyDictionary<string, TraitWorkforceProjection> traitCatalog)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(traitCatalog);
        if (!content.Jobs.Select(job => job.Id).Order(StringComparer.Ordinal).SequenceEqual(JOB_IDS.Order(StringComparer.Ordinal)) || content.Jobs.Any(job => job.WorkforcePerJob != 100))
        {
            throw new ArgumentException("M2 требует три профессии M1 с WorkforcePerJob=100.", nameof(content));
        }
        if (!content.Buildings.Select(building => building.Id).Order(StringComparer.Ordinal).SequenceEqual(BUILDING_IDS.Order(StringComparer.Ordinal)))
        {
            throw new ArgumentException("M2 требует три здания M1.", nameof(content));
        }
        if (!traitCatalog.Keys.Order(StringComparer.Ordinal).SequenceEqual(TRAIT_IDS))
        {
            throw new ArgumentException("Каталог M2 требует только trait_agrarian, trait_industrious, trait_ingenious.", nameof(traitCatalog));
        }
        if (content.Resources.Any(resource => resource.Capacity < 1000))
        {
            throw new ArgumentException("Ёмкость ресурсов M2 должна допускать начальные запасы 1000.", nameof(content));
        }
        _content = content with { Resources = Array.AsReadOnly(content.Resources.ToArray()), Jobs = Array.AsReadOnly(content.Jobs.ToArray()), Categories = Array.AsReadOnly(content.Categories.ToArray()) };
        _traitCatalog = traitCatalog.ToDictionary(pair => pair.Key, pair => pair.Value with { JobMultipliers = Copy(pair.Value.JobMultipliers) }, StringComparer.Ordinal);
        _buildingCatalog = content.Buildings.ToDictionary(building => building.Id, building => building with { Upkeep = Copy(building.Upkeep), Modifiers = Array.AsReadOnly(building.Modifiers.ToArray()) }, StringComparer.Ordinal);
        foreach (var building in _buildingCatalog.Values)
        {
            if (building.Upkeep.Any(amount => amount.Value < 0 || !content.Resources.Any(resource => resource.Id == amount.Key)) ||
                building.Modifiers.Any(modifier => !content.Resources.Any(resource => resource.Id == modifier.Resource) || !content.Categories.Any(category => category.Id == modifier.Category) || modifier.Operation is not "produces" and not "upkeep"))
            {
                throw new ArgumentException("Здание M2 содержит недопустимые расходы или модификаторы.", nameof(content));
            }
        }
        foreach (var item in _traitCatalog)
        {
            if (item.Value.JobMultipliers.Keys.Any(id => !JOB_IDS.Contains(id)) || item.Value.JobMultipliers.Values.Any(value => value < 0))
            {
                throw new ArgumentException("Каталог traits содержит неподдерживаемые доли Workforce.", nameof(traitCatalog));
            }
        }
        _world = new EconomyWorld(content.Resources, content.Jobs, content.Categories);
        _catalog = new CatalogView(
            Array.AsReadOnly(new[] { new CatalogItem("technician", "Техники", "common/pop_jobs/03_worker_jobs.txt"), new CatalogItem("miner", "Шахтёры", "common/pop_jobs/03_worker_jobs.txt"), new CatalogItem("farmer", "Фермеры", "common/pop_jobs/03_worker_jobs.txt") }),
            Array.AsReadOnly(new[] { new CatalogItem("building_energy_grid", "Энергосеть", "common/buildings/03_resource_buildings.txt"), new CatalogItem("building_mineral_purification_plant", "Очистка минералов", "common/buildings/03_resource_buildings.txt"), new CatalogItem("building_food_processing_facility", "Переработка пищи", "common/buildings/03_resource_buildings.txt") }),
            Array.AsReadOnly(new[] { new CatalogItem("trait_agrarian", "Аграрии", "common/traits/04_species_traits.txt"), new CatalogItem("trait_industrious", "Трудолюбивые", "common/traits/04_species_traits.txt"), new CatalogItem("trait_ingenious", "Изобретательные", "common/traits/04_species_traits.txt") }));
    }

    public JoinGrant Join(string name, string role, string? token, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (role is not "player" and not "observer")
            {
                throw new ArgumentException("Неизвестная роль.", nameof(role));
            }
            if (token != null)
            {
                var returning = _participants.SingleOrDefault(participant => participant.Token == token);
                if (returning == null || returning.Connected || (returning.Role == "player") != (role == "player"))
                {
                    throw new InvalidOperationException("Возврат невозможен: токен недействителен или уже используется.");
                }
                returning.Connected = true;
                returning.DisconnectedAt = null;
                returning.Afk = false;
                _sequence++;
                return new JoinGrant(returning.Id, returning.Token, _sessionId);
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
            var participant = new Participant(_participants.Count + 1, name.Trim(), role == "player" ? role : "pendingObserver", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            if (role == "player")
            {
                participant.Country = _world.CreateCountry();
                participant.Planet = _world.CreatePlanet(participant.Country.Value);
                _world.AddGroup(participant.Planet.Value, 600, 1);
                foreach (var resource in _content.Resources)
                {
                    _world.SetBalance(participant.Country.Value, resource.Id, 1000);
                }
                foreach (var job in _content.Jobs)
                {
                    _world.SetJobs(participant.Planet.Value, job.Id, 2);
                    participant.Jobs.Add(job.Id, 2);
                    participant.BonusMultipliers.Add(job.Id, 0);
                }
                _host ??= participant.Id;
            }
            _participants.Add(participant);
            _sequence++;
            return new JoinGrant(participant.Id, participant.Token, _sessionId);
        }
    }

    public void Disconnect(int id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var participant = Find(id);
            if (!participant.Connected)
            {
                return;
            }
            participant.Connected = false;
            participant.DisconnectedAt = now;
            _sequence++;
        }
    }

    public CommandResult Execute(int id, ClientCommand command, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            var participant = Find(id);
            if (!participant.Connected)
            {
                return new CommandResult(command.CommandId, false, "Соединение неактивно.");
            }
            if (string.IsNullOrWhiteSpace(command.CommandId) || command.CommandId.Length > 64)
            {
                return new CommandResult(command.CommandId ?? "", false, "ID команды должен содержать 1–64 символа.");
            }
            var fingerprint = JsonSerializer.Serialize(command);
            if (participant.Commands.TryGetValue(command.CommandId, out var previous))
            {
                return previous.Fingerprint == fingerprint ? previous.Result : new CommandResult(command.CommandId, false, "ID уже использован для другого содержимого.");
            }
            if (participant.Commands.Count >= 10000)
            {
                return new CommandResult(command.CommandId, false, "Лимит 10000 команд достигнут; новые команды недоступны до перезапуска сессии.");
            }
            CommandResult result;
            try
            {
                Apply(participant, command, now);
                result = new CommandResult(command.CommandId, true, "Принято.");
            }
            catch (CommandRejected exception)
            {
                result = new CommandResult(command.CommandId, false, exception.Message);
            }
            participant.Commands.Add(command.CommandId, new CachedCommand(fingerprint, result));
            return result;
        }
    }

    public void Tick(DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var participant in _participants)
            {
                if (!participant.Connected && !participant.Afk && participant.DisconnectedAt is { } disconnected && now - disconnected >= TimeSpan.FromSeconds(60))
                {
                    participant.Afk = true;
                    _sequence++;
                }
            }
            if (_phase != "running" || _paused)
            {
                return;
            }
            _nextTick ??= now + Interval();
            if (now >= _nextTick.Value)
            {
                CalculateMonth();
                _nextTick = now + Interval();
            }
        }
    }

    public SessionView View(int id)
    {
        lock (_gate)
        {
            var viewer = Find(id);
            var participants = _participants.Select(participant => new ParticipantView(participant.Id, participant.Name, participant.Role, _host == participant.Id, participant.Connected, participant.Afk, participant.Ready)).ToArray();
            var visible = _participants.Where(participant => participant.Planet != null && (viewer.Role == "observer" || participant.Id == id && viewer.Role == "player"));
            var planets = visible.Select(participant => new PlanetView(participant.Planet!.Value.Index, participant.Id, $"Планета {participant.Name}", 600, 600,
                Copy(participant.Jobs), Copy(participant.BonusMultipliers), Array.AsReadOnly(participant.Traits.ToArray()), Array.AsReadOnly(participant.Buildings.ToArray()),
                Copy(_content.Resources.ToDictionary(resource => resource.Id, resource => _world.GetBalance(participant.Country!.Value, resource.Id))),
                _world.Month == 0 ? null : _world.GetPlanetReport(participant.Planet.Value), _world.Month == 0 ? null : _world.Month)).ToArray();
            return new SessionView(1, _sessionId, _sequence, _phase, _world.Month, _paused, _speed, id, Array.AsReadOnly(participants), Array.AsReadOnly(planets), _catalog, _lastError, _lastTickMilliseconds);
        }
    }

    private void Apply(Participant participant, ClientCommand command, DateTimeOffset now)
    {
        switch (command.Type)
        {
            case "configure":
                RequirePlayer(participant);
                RequireLobby();
                Configure(participant, command);
                break;
            case "ready":
                RequirePlayer(participant);
                RequireLobby();
                participant.Ready = command.Ready ?? throw Reject("Не задан ready.");
                break;
            case "start":
                RequireHost(participant);
                RequireLobby();
                var players = _participants.Where(player => player.Role == "player").ToArray();
                if (players.Length == 0 || players.Any(player => !player.Connected || !player.Ready))
                {
                    throw Reject("Для запуска нужен хотя бы один игрок; все игроки должны быть подключены и готовы.");
                }
                _phase = "running";
                _paused = true;
                _nextTick = null;
                break;
            case "setJobs":
                RequirePlayer(participant);
                RequireRunning();
                if (command.PlanetId != participant.Planet!.Value.Index)
                {
                    throw Reject("Планета недоступна.");
                }
                if (command.Job == null || !participant.Jobs.ContainsKey(command.Job) || command.Count is not { } count || count < 0 || count > 6 || count % 0.5m != 0)
                {
                    throw Reject("Профессия или число вакансий недопустимы; допустимо 0–6 с шагом 0.5.");
                }
                _world.SetJobs(participant.Planet.Value, command.Job, count);
                participant.Jobs[command.Job] = count;
                break;
            case "pause":
                RequireHost(participant);
                RequireRunning();
                _paused = command.Paused ?? throw Reject("Не задан paused.");
                _nextTick = _paused ? null : now + Interval();
                break;
            case "speed":
                RequireHost(participant);
                RequireRunning();
                if (command.Speed is not (1 or 2 or 4))
                {
                    throw Reject("Допустимые скорости: 1, 2, 4 месяца/сек.");
                }
                _speed = command.Speed.Value;
                _nextTick = _paused ? null : now + Interval();
                break;
            case "step":
                RequireHost(participant);
                RequireRunning();
                if (!_paused)
                {
                    throw Reject("Шаг доступен только на паузе.");
                }
                if (!CalculateMonth())
                {
                    throw Reject(_lastError!);
                }
                return;
            case "transferHost":
                RequireHost(participant);
                var target = _participants.SingleOrDefault(player => player.Id == command.TargetParticipantId && player.Role == "player" && player.Connected && player.Id != participant.Id);
                if (target == null)
                {
                    throw Reject("Передать права можно другому подключённому игроку.");
                }
                _host = target.Id;
                break;
            case "approveObserver":
                RequireHost(participant);
                RequireLobby();
                var observer = _participants.SingleOrDefault(player => player.Id == command.TargetParticipantId && player.Role == "pendingObserver");
                if (observer == null)
                {
                    throw Reject("Ожидающий наблюдатель не найден.");
                }
                observer.Role = "observer";
                break;
            default:
                throw Reject("Неизвестная команда.");
        }
        _sequence++;
    }

    private void Configure(Participant participant, ClientCommand command)
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

    private bool CalculateMonth()
    {
        var timer = Stopwatch.StartNew();
        try
        {
            _world.AdvanceMonth();
            _lastError = null;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or OverflowException or AggregateException)
        {
            _paused = true;
            _nextTick = null;
            _lastError = "Месячный расчёт отклонён; состояние сохранено. Проверьте расходы и параметры лаборатории.";
            return false;
        }
        finally
        {
            _lastTickMilliseconds = timer.Elapsed.TotalMilliseconds;
            _sequence++;
        }
    }

    private Participant Find(int id)
    {
        return _participants.SingleOrDefault(participant => participant.Id == id) ?? throw new InvalidOperationException("Участник недоступен.");
    }

    private void RequireHost(Participant participant)
    {
        if (participant.Role != "player" || _host != participant.Id)
        {
            throw Reject("Команда доступна только хосту.");
        }
    }

    private static void RequirePlayer(Participant participant)
    {
        if (participant.Role != "player")
        {
            throw Reject("Команда доступна только игроку.");
        }
    }

    private void RequireLobby()
    {
        if (_phase != "lobby")
        {
            throw Reject("Команда доступна только в лобби.");
        }
    }

    private void RequireRunning()
    {
        if (_phase != "running")
        {
            throw Reject("Сессия ещё не запущена.");
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
        return new CommandRejected(message);
    }

    private sealed class Participant(int id, string name, string role, string token)
    {
        public readonly int Id = id;
        public readonly string Name = name;
        public readonly string Token = token;
        public readonly Dictionary<string, CachedCommand> Commands = new(StringComparer.Ordinal);
        public string Role = role;
        public bool Connected = true;
        public bool Afk;
        public bool Ready;
        public DateTimeOffset? DisconnectedAt;
        public EntityId? Country;
        public EntityId? Planet;
        public Dictionary<string, decimal> Jobs = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> BonusMultipliers = new(StringComparer.Ordinal);
        public string[] Traits = [];
        public string[] Buildings = [];
    }

    private sealed record CachedCommand(string Fingerprint, CommandResult Result);
    private sealed class CommandRejected(string message) : Exception(message);
}
