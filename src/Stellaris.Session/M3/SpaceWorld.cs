namespace Stellaris.Session.M3;

// Неизменяемый staged-мир M3; публикацией и экономической транзакцией владеет сессия.
public sealed partial class SpaceWorld
{
    public const double SPEED_UNITS_PER_DAY = 7.2;
    public const double CHARGE_DAYS = 15;
    public const double TRANSIT_DAYS = 5;
    // Историческая геометрия fixture и нижняя граница размеров generated-систем.
    public const double EXIT_RADIUS = 140;
    public const double EXIT_INSET = 10;
    private const double EPSILON = 1e-6;
    private readonly Dictionary<string, SystemDefinition> _systems;
    private readonly Dictionary<string, string[]> _neighbors;
    public IReadOnlyList<SystemDefinition> Systems { get; }
    public IReadOnlyList<HyperlaneDefinition> Hyperlanes { get; }
    public IReadOnlyList<BodyDefinition> Bodies { get; }
    public IReadOnlyList<ColonyDefinition> Colonies { get; }
    public IReadOnlyList<FleetState> Fleets { get; }
    public int ElapsedDay { get; }

    private SpaceWorld(SystemDefinition[] systems, HyperlaneDefinition[] lanes, BodyDefinition[] bodies, ColonyDefinition[] colonies, FleetState[] fleets, int elapsedDay)
    {
        _systems = systems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _neighbors = systems.ToDictionary(item => item.Id, _ => Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var system in systems)
        {
            _neighbors[system.Id] = lanes.Where(lane => lane.FromSystemId == system.Id || lane.ToSystemId == system.Id).Select(lane => lane.FromSystemId == system.Id ? lane.ToSystemId : lane.FromSystemId).Order(StringComparer.Ordinal).ToArray();
        }
        Systems = Array.AsReadOnly(systems);
        Hyperlanes = Array.AsReadOnly(lanes);
        Bodies = Array.AsReadOnly(bodies);
        Colonies = Array.AsReadOnly(colonies);
        Fleets = Array.AsReadOnly(fleets.Select(CopyFleet).ToArray());
        ElapsedDay = elapsedDay;
        Validate();
    }

    private SpaceWorld(SpaceWorld previous, FleetState[] fleets, int elapsedDay)
    {
        _systems = previous._systems;
        _neighbors = previous._neighbors;
        Systems = previous.Systems;
        Hyperlanes = previous.Hyperlanes;
        Bodies = previous.Bodies;
        Colonies = previous.Colonies;
        Fleets = Array.AsReadOnly(fleets);
        ElapsedDay = elapsedDay;
    }

    public static SpaceWorld CreateFixture(params int[] participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);
        if (participantIds.Length == 0) { throw new ArgumentException("Нужен хотя бы один participantId.", nameof(participantIds)); }
        if (participantIds.Any(id => id <= 0) || participantIds.Distinct().Count() != participantIds.Length)
        { throw new ArgumentException("Все participantId должны быть положительными и различными.", nameof(participantIds)); }

        var side = Math.Max(4, (int)Math.Ceiling(Math.Sqrt(participantIds.Length)));
        int systemCount;
        try { systemCount = checked(side * side); }
        catch (OverflowException exception) { throw new ArgumentException("Слишком много игроков для размера тестовой сетки.", nameof(participantIds), exception); }
        var systems = Enumerable.Range(0, systemCount)
            .Select(index => new SystemDefinition($"S{index + 1:00}", $"Система {index + 1}", new GalaxyPoint(index % side * 100, 0, index / side * 100), 150)).ToArray();
        var lanes = new List<HyperlaneDefinition>();
        for (var index = 0; index < systems.Length; index++)
        {
            foreach (var next in new[]
            {
                index % side < side - 1 ? index + 1 : -1,
                index / side < side - 1 ? index + side : -1
            })
            {
                if (next < 0) { continue; }
                var fromSystemId = systems[index].Id;
                var toSystemId = systems[next].Id;
                if (StringComparer.Ordinal.Compare(fromSystemId, toSystemId) > 0) { (fromSystemId, toSystemId) = (toSystemId, fromSystemId); }
                lanes.Add(new HyperlaneDefinition(fromSystemId + "-" + toSystemId, fromSystemId, toSystemId));
            }
        }
        var bodies = systems.SelectMany(system => new[]
        {
            new BodyDefinition("star:" + system.Id, system.Id, "star", "Звезда", new LocalPoint(0, 0), "star.g", 1),
            new BodyDefinition("planet:" + system.Id, system.Id, "planet", "Планета", new LocalPoint(50, 0), "planet.continental", 1)
        }).ToList();
        var colonies = new List<ColonyDefinition>();
        var fleets = new List<FleetState>();
        var homeSystemIndexes = Enumerable.Range(0, systems.Length)
            .Where(index => index != 0 && index != 15)
            .Prepend(15)
            .Prepend(0)
            .Take(participantIds.Length)
            .ToArray();
        for (var slot = 0; slot < participantIds.Length; slot++)
        {
            var participant = participantIds[slot];
            var systemId = systems[homeSystemIndexes[slot]].Id;
            bodies.Add(new BodyDefinition("station:" + systemId, systemId, "station", "Станция", new LocalPoint(30, 0), "station.outpost.mammalian", 1, participant));
            var economySlot = slot + 1;
            colonies.Add(new ColonyDefinition("colony:" + economySlot, "planet:" + systemId, participant, economySlot));
            fleets.Add(new FleetState("fleet:" + economySlot, participant, "Флот " + economySlot, "ship.corvette.mammalian", 3, FleetPhase.Idle, new SystemLocation(systemId, new LocalPoint(0, -100)), new FleetMovement(0, null, Array.Empty<string>(), 0, null, null)));
        }
        return new SpaceWorld(systems, lanes.ToArray(), bodies.ToArray(), colonies.ToArray(), fleets.ToArray(), 0);
    }

    public IReadOnlyList<string> FindRoute(string fromSystemId, string targetSystemId, IReadOnlySet<string> permittedSystems)
    {
        var allowed = Allowed(permittedSystems);
        RequireVisible(fromSystemId, allowed);
        RequireVisible(targetSystemId, allowed);
        return Array.AsReadOnly(Route(fromSystemId, targetSystemId, allowed));
    }

    private void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in Systems.Select(item => item.Id).Concat(Hyperlanes.Select(item => item.Id)).Concat(Bodies.Select(item => item.Id)).Concat(Colonies.Select(item => item.Id)).Concat(Fleets.Select(item => item.Id)))
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { throw new InvalidOperationException("Повторный/пустой spatial ID."); }
        }
        foreach (var system in Systems)
        {
            if (!double.IsFinite(system.GalaxyPosition.X) || !double.IsFinite(system.GalaxyPosition.Y) || !double.IsFinite(system.GalaxyPosition.Z) || !double.IsFinite(system.Radius) || system.Radius < EXIT_RADIUS) { throw new InvalidOperationException("Неверная система fixture."); }
        }
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lane in Hyperlanes)
        {
            if (!_systems.ContainsKey(lane.FromSystemId) || !_systems.ContainsKey(lane.ToSystemId) || StringComparer.Ordinal.Compare(lane.FromSystemId, lane.ToSystemId) >= 0 || !edges.Add(lane.FromSystemId + "\0" + lane.ToSystemId)) { throw new InvalidOperationException("Неверная гиперлиния fixture."); }
            _ = Exit(lane.FromSystemId, lane.ToSystemId);
        }
        foreach (var body in Bodies)
        {
            if (!_systems.TryGetValue(body.SystemId, out var system) || !Inside(body.Position, system.Radius) || !double.IsFinite(body.Scale) || body.Scale <= 0) { throw new InvalidOperationException("Неверное тело fixture."); }
        }
        foreach (var colony in Colonies)
        {
            if (colony.OwnerParticipantId <= 0 || colony.EconomyPlanetId <= 0 || !Bodies.Any(body => body.Id == colony.BodyId && body.Kind == "planet")) { throw new InvalidOperationException("Неверная колония fixture."); }
        }
        foreach (var fleet in Fleets)
        {
            if (fleet.OwnerParticipantId <= 0 || fleet.ShipCount != 3 || fleet.Location is not SystemLocation location || !_systems.TryGetValue(location.SystemId, out var system) || !Inside(location.Position, system.Radius)) { throw new InvalidOperationException("Неверный флот fixture."); }
        }
    }

    private static FleetState CopyFleet(FleetState fleet)
    {
        return fleet with { Movement = fleet.Movement with { RouteSystemIds = Array.AsReadOnly(fleet.Movement.RouteSystemIds.ToArray()) } };
    }

    private static HashSet<string> Allowed(IReadOnlySet<string> permittedSystems)
    {
        ArgumentNullException.ThrowIfNull(permittedSystems);
        return new HashSet<string>(permittedSystems, StringComparer.Ordinal);
    }

    private void RequireVisible(string id, HashSet<string> allowed)
    {
        if (id == null || !allowed.Contains(id) || !_systems.ContainsKey(id)) { throw Reject("OUT_OF_SCOPE"); }
    }

    private FleetState OwnedFleet(int participantId, string fleetId, HashSet<string> allowed)
    {
        var fleet = Fleets.SingleOrDefault(item => item.Id == fleetId && item.OwnerParticipantId == participantId);
        if (fleet == null || fleet.Location is SystemLocation location && !allowed.Contains(location.SystemId) || fleet.Location is TransitLocation transit && (!allowed.Contains(transit.FromSystemId) || !allowed.Contains(transit.ToSystemId))) { throw Reject("OUT_OF_SCOPE"); }
        return fleet;
    }

    private string[] Route(string from, string target, HashSet<string> allowed)
    {
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal) { [from] = null };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.TryDequeue(out var current))
        {
            if (current == target)
            {
                var result = new List<string>();
                for (string? id = target; id != null; id = parents[id]) { result.Add(id); }
                result.Reverse();
                return result.ToArray();
            }
            foreach (var neighbor in _neighbors[current])
            {
                if (!allowed.Contains(neighbor) || parents.ContainsKey(neighbor)) { continue; }
                parents.Add(neighbor, current);
                queue.Enqueue(neighbor);
            }
        }
        throw Reject("NO_ROUTE");
    }

    public LocalPoint GetExitPosition(string from, string to)
    {
        if (from is null || to is null || !_neighbors.TryGetValue(from, out var neighbors) || !neighbors.Contains(to, StringComparer.Ordinal))
        {
            throw new ArgumentException("An exit requires an existing adjacent system pair.");
        }
        return Exit(from, to);
    }

    private LocalPoint Exit(string from, string to)
    {
        var a = _systems[from].GalaxyPosition;
        var b = _systems[to].GalaxyPosition;
        var x = b.X - a.X;
        var z = b.Z - a.Z;
        var distance = Math.Sqrt(x * x + z * z);
        if (!double.IsFinite(distance) || distance == 0) { throw new InvalidOperationException("Совпадающие/неверные центры гиперлинии."); }
        var radius = _systems[from].Radius - EXIT_INSET;
        if (!double.IsFinite(radius) || radius <= 0) { throw new InvalidOperationException("Радиус выхода вне системы."); }
        return new LocalPoint(x / distance * radius, z / distance * radius);
    }

    private static bool Inside(LocalPoint point, double radius)
    {
        return double.IsFinite(point.X) && double.IsFinite(point.Z) && point.X * point.X + point.Z * point.Z <= radius * radius;
    }

    private SpaceWorld Replace(FleetState fleet)
    {
        return new SpaceWorld(this, Fleets.Select(item => item.Id == fleet.Id ? CopyFleet(fleet) : item).ToArray(), ElapsedDay);
    }

    private static SpatialRejected Reject(string code)
    {
        return new SpatialRejected(code, code switch { "OUT_OF_SCOPE" => "Объект недоступен.", "INVALID_TARGET" => "Недопустимая цель.", "INVALID_PHASE" => "Команда недоступна в текущей фазе.", "NO_ROUTE" => "Допустимый маршрут отсутствует.", _ => "Пространственный расчёт отклонён." });
    }
}
