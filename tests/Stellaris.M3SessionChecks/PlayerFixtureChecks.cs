using Stellaris.Session.M3;

internal static class PlayerFixtureChecks
{
    public static void Run()
    {
        VerifyTwoPlayerCompatibility();
        VerifyFlexiblePlayerCounts();
        VerifyInvalidParticipants();
    }

    private static void VerifyTwoPlayerCompatibility()
    {
        var world = SpaceWorld.CreateFixture(7, 42);
        Require(world.Systems.Count == 16 && world.Hyperlanes.Count == 24, "Двухигровой fixture должен сохранить топологию 4×4.");
        Require(world.Systems[0].Id == "S01" && world.Systems[15].Id == "S16", "Изменились крайние ID систем.");
        Require(world.Colonies.Select(item => item.Id).Order().SequenceEqual(new[] { "colony:1", "colony:2" }), "Изменились colony IDs.");
        Require(world.Colonies.Single(item => item.OwnerParticipantId == 7).BodyId == "planet:S01" &&
                world.Colonies.Single(item => item.OwnerParticipantId == 42).BodyId == "planet:S16", "Изменилось размещение стартовых колоний.");
        Require(world.Colonies.Select(item => item.EconomyPlanetId).Order().SequenceEqual(new[] { 1, 2 }), "Изменились economy slots.");
        Require(world.Bodies.Where(item => item.Kind == "station").Select(item => item.Id).Order().SequenceEqual(new[] { "station:S01", "station:S16" }), "Изменились station IDs.");
        Require(world.Fleets.Select(item => item.Id).Order().SequenceEqual(new[] { "fleet:1", "fleet:2" }), "Изменились fleet IDs.");
        Require(world.Fleets.Single(item => item.OwnerParticipantId == 7).Location is SystemLocation { SystemId: "S01" } &&
                world.Fleets.Single(item => item.OwnerParticipantId == 42).Location is SystemLocation { SystemId: "S16" }, "Изменилось размещение стартовых флотов.");
    }

    private static void VerifyFlexiblePlayerCounts()
    {
        var single = SpaceWorld.CreateFixture(91);
        Require(single.Systems.Count == 16 && single.Colonies.Single().BodyId == "planet:S01", "Один игрок должен получить S01 в базовой сетке.");

        var players = Enumerable.Range(1, 17).Select(index => 1000 + index).ToArray();
        var world = SpaceWorld.CreateFixture(players);
        Require(world.Systems.Count == 25 && world.Hyperlanes.Count == 40, "17 игроков должны расширить fixture до сетки 5×5.");
        var expectedHomes = new[] { "S01", "S16" }
            .Concat(Enumerable.Range(2, 24).Where(index => index != 16).Select(index => $"S{index:00}"))
            .Take(17).ToArray();
        Require(world.Colonies.Count == players.Length && world.Fleets.Count == players.Length, "У каждого игрока должна быть колония и флот.");
        for (var slot = 0; slot < players.Length; slot++)
        {
            var colony = world.Colonies.Single(item => item.OwnerParticipantId == players[slot]);
            var fleet = world.Fleets.Single(item => item.OwnerParticipantId == players[slot]);
            var station = world.Bodies.Single(item => item.Kind == "station" && item.OwnerParticipantId == players[slot]);
            Require(colony.Id == $"colony:{slot + 1}" && colony.EconomyPlanetId == slot + 1, "Colony/economy slot должен быть уникальным и совпадать со слотом.");
            Require(colony.BodyId == $"planet:{expectedHomes[slot]}" && station.SystemId == expectedHomes[slot], "Неверно распределены домашние системы.");
            Require(fleet.Id == $"fleet:{slot + 1}" && fleet.ShipCount == 3 && fleet.Location is SystemLocation { } location && location.SystemId == expectedHomes[slot], "Неверный стартовый флот.");
        }
        Require(world.Systems.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == world.Systems.Count, "ID систем должны быть уникальны.");

        var twenty = SpaceWorld.CreateFixture(Enumerable.Range(1, 20).ToArray());
        Require(twenty.Systems.Count == 25 && twenty.Hyperlanes.Count == 40, "20 игроков должны получить квадратную сетку 5×5.");
        Require(twenty.Colonies.Select(item => item.BodyId).Distinct(StringComparer.Ordinal).Count() == 20, "Домашние системы 20 игроков должны быть разными.");

        var eightyTwo = SpaceWorld.CreateFixture(Enumerable.Range(1, 82).ToArray());
        Require(eightyTwo.Systems.Count == 100 && eightyTwo.Hyperlanes.Count == 180, "82 игрока должны получить валидную сетку 10×10.");
        Require(eightyTwo.Hyperlanes.All(lane => StringComparer.Ordinal.Compare(lane.FromSystemId, lane.ToSystemId) < 0 && lane.Id == lane.FromSystemId + "-" + lane.ToSystemId), "Концы и ID гиперлиний должны иметь единый ordinal-порядок.");
        var route = eightyTwo.FindRoute("S01", "S100", eightyTwo.Systems.Select(system => system.Id).ToHashSet(StringComparer.Ordinal));
        Require(route.Count > 1 && route[0] == "S01" && route[^1] == "S100", "Граф расширенной сетки должен оставаться связным.");
    }

    private static void VerifyInvalidParticipants()
    {
        Expect<ArgumentNullException>(() => SpaceWorld.CreateFixture(null!));
        Expect<ArgumentException>(() => SpaceWorld.CreateFixture());
        Expect<ArgumentException>(() => SpaceWorld.CreateFixture(0));
        Expect<ArgumentException>(() => SpaceWorld.CreateFixture(1, -1));
        Expect<ArgumentException>(() => SpaceWorld.CreateFixture(1, 1));
    }

    private static void Expect<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new Exception($"Ожидалось исключение {typeof(TException).Name}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new Exception(message); }
    }
}
