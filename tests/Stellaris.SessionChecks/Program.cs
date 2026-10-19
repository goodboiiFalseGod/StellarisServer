using System.Text.Json;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;
using Stellaris.Session;

var suites = new (string Name, Action Run)[]
{
    ("Лобби, готовность и наблюдатель", Checks.Lobby),
    ("Команды, владение и защита от повторов", Checks.Commands),
    ("Пауза, скорости и AFK", Checks.ClockAndAfk),
    ("Возврат, токены и передача прав", Checks.ReconnectAndHost),
    ("Фильтрация экономических данных", Checks.Filtering),
    ("Сессия с одним игроком", () => Checks.PlayerCount(1)),
    ("Сессия с двумя игроками", () => Checks.PlayerCount(2)),
    ("Сессия с тремя игроками", () => Checks.PlayerCount(3)),
    ("Сессия с десятью игроками", () => Checks.PlayerCount(10)),
    ("Сессия с двадцатью игроками", () => Checks.PlayerCount(20)),
    ("Раздельные источники расходов", Checks.UpkeepBreakdown),
    ("Атомарная ошибка месячной экономики", Checks.MonthFailure)
};
var failures = 0;
foreach (var suite in suites)
{
    try
    {
        suite.Run();
        Console.WriteLine($"PASS: {suite.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {suite.Name}\n{exception}");
    }
}
Console.WriteLine($"Наборов: {suites.Length}; ошибок: {failures}.");
return failures == 0 ? 0 : 1;

internal static class Checks
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] JOB_IDS = ["technician", "miner", "farmer"];
    private static readonly string[] BUILDING_IDS = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    private static readonly string[] TRAIT_IDS = ["trait_ingenious", "trait_industrious", "trait_agrarian"];

    public static void Lobby()
    {
        var session = NewSession();
        var host = session.Join("Хост", "player", null, NOW);
        var second = session.Join("Игрок", "player", null, NOW);
        var observer = session.Join("Наблюдатель", "observer", null, NOW);
        Check(session.View(host.ParticipantId).Participants.Single(item => item.Id == host.ParticipantId).IsHost, "Первый игрок становится хостом");
        Check(session.View(observer.ParticipantId).Planets.Count == 0, "Ожидающий наблюдатель не видит планеты");
        RejectCommand(session, host.ParticipantId, new("early-start", "start"));
        Accept(session, host.ParticipantId, new("host-ready", "ready", Ready: true));
        Accept(session, second.ParticipantId, new("second-ready", "ready", Ready: true));
        var before = Json(session.View(host.ParticipantId));
        RejectCommand(session, host.ParticipantId, new("bad-config", "configure", Traits: ["unknown"], Buildings: []));
        Check(Json(session.View(host.ParticipantId)) == before, "Некорректный configure не меняет настройки, sequence и готовность");
        Accept(session, host.ParticipantId, new("config", "configure", Traits: [TRAIT_IDS[0]], Buildings: [BUILDING_IDS[0]]));
        Check(!session.View(host.ParticipantId).Participants.Single(item => item.Id == host.ParticipantId).Ready, "Configure снимает готовность");
        RejectCommand(session, second.ParticipantId, new("approve-other", "approveObserver", TargetParticipantId: observer.ParticipantId));
        Accept(session, host.ParticipantId, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
        Accept(session, host.ParticipantId, new("host-ready-again", "ready", Ready: true));
        session.Disconnect(second.ParticipantId, NOW);
        RejectCommand(session, host.ParticipantId, new("offline-start", "start"));
        session.Join("Игрок", "player", second.Token, NOW);
        Accept(session, second.ParticipantId, new("second-ready-again", "ready", Ready: true));
        Accept(session, host.ParticipantId, new("start", "start"));
        var view = session.View(host.ParticipantId);
        Check(view.Phase == "running" && view.Month == 0 && view.Paused, "Старт на паузе с месяцем 0");
        var planet = view.Planets.Single();
        Check(planet.Report == null, "До первого месяца нет фиктивного отчёта");
        Equal(1000, planet.Balances["energy"], "Начальный запас");
        Check(planet.Traits.SequenceEqual(new[] { TRAIT_IDS[0] }) && planet.Buildings.SequenceEqual(new[] { BUILDING_IDS[0] }), "Настройки применены при старте");
        Equal(0.15m, planet.BonusMultipliers["technician"], "Бонус выбранного trait");
        Check(session.View(observer.ParticipantId).Planets.Count == 2, "Разрешённый наблюдатель видит обе планеты");
        RejectCommand(session, host.ParticipantId, new("late-config", "configure", Traits: [], Buildings: []));
        RejectJoin(() => session.Join("Третий", "player", null, NOW));
    }

    public static void Commands()
    {
        var (session, host, second, observer) = Running();
        var own = session.View(host.ParticipantId).Planets.Single().Id;
        var foreign = session.View(second.ParticipantId).Planets.Single().Id;
        RejectCommand(session, observer.ParticipantId, new("observer-jobs", "setJobs", PlanetId: own, Job: "technician", Count: 1));
        var foreignResult = session.Execute(host.ParticipantId, new("foreign", "setJobs", PlanetId: foreign, Job: "technician", Count: 1), NOW);
        var unknownResult = session.Execute(host.ParticipantId, new("unknown", "setJobs", PlanetId: int.MaxValue, Job: "technician", Count: 1), NOW);
        Check(!foreignResult.Accepted && !unknownResult.Accepted && foreignResult.Message == unknownResult.Message, "Ошибка владения не раскрывает существование чужой планеты");
        foreach (var count in new[] { -1m, 6.5m, 0.1m })
        {
            RejectCommand(session, host.ParticipantId, new("bad-count-" + count, "setJobs", PlanetId: own, Job: "technician", Count: count));
        }
        RejectCommand(session, host.ParticipantId, new("bad-job", "setJobs", PlanetId: own, Job: "missing", Count: 1));
        Accept(session, host.ParticipantId, new("jobs", "setJobs", PlanetId: own, Job: "technician", Count: 0.5m));
        Equal(0.5m, session.View(host.ParticipantId).Planets.Single().Jobs["technician"], "Вакансии приняты без продвижения месяца");
        var command = new ClientCommand("step-id", "step");
        Accept(session, host.ParticipantId, command);
        Check(session.View(host.ParticipantId).Month == 1, "Шаг рассчитывает месяц");
        var before = Json(session.View(host.ParticipantId));
        Accept(session, host.ParticipantId, command);
        Check(Json(session.View(host.ParticipantId)) == before, "Повтор ID не повторяет шаг и не меняет sequence");
        RejectCommand(session, host.ParticipantId, new("step-id", "pause", Paused: false));
        Check(Json(session.View(host.ParticipantId)) == before, "Новая нагрузка старого ID не меняет мир");
        RejectCommand(session, host.ParticipantId, new("", "step"));
        RejectCommand(session, host.ParticipantId, new(new string('x', 65), "step"));
    }

    public static void ClockAndAfk()
    {
        var (session, host, second, _) = Running();
        RejectCommand(session, second.ParticipantId, new("other-speed", "speed", Speed: 2));
        RejectCommand(session, second.ParticipantId, new("other-pause", "pause", Paused: false));
        RejectCommand(session, second.ParticipantId, new("other-step", "step"));
        RejectCommand(session, host.ParticipantId, new("bad-speed", "speed", Speed: 3));
        session.Disconnect(second.ParticipantId, NOW);
        session.Tick(NOW.AddSeconds(59));
        Check(!session.View(host.ParticipantId).Participants.Single(item => item.Id == second.ParticipantId).Afk, "AFK не раньше 60 секунд");
        session.Tick(NOW.AddSeconds(60));
        var view = session.View(host.ParticipantId);
        Check(view.Participants.Single(item => item.Id == second.ParticipantId).Afk && view.Month == 0, "AFK работает на паузе без продвижения экономики");
        Accept(session, host.ParticipantId, new("speed", "speed", Speed: 4), NOW.AddSeconds(60));
        Accept(session, host.ParticipantId, new("resume", "pause", Paused: false), NOW.AddSeconds(60));
        RejectCommand(session, host.ParticipantId, new("running-step", "step"), NOW.AddSeconds(60));
        session.Tick(NOW.AddSeconds(60.25));
        Check(session.View(host.ParticipantId).Month == 1, "Скорость 4 рассчитывает месяц через четверть секунды");
        session.Tick(NOW.AddSeconds(100));
        Check(session.View(host.ParticipantId).Month == 2, "Долгая задержка не создаёт рывок пропущенных месяцев");
        Accept(session, host.ParticipantId, new("stop", "pause", Paused: true), NOW.AddSeconds(100));
        session.Tick(NOW.AddSeconds(1000));
        Check(session.View(host.ParticipantId).Month == 2, "На паузе календарь стоит");
        session.Join("Игрок", "player", second.Token, NOW.AddSeconds(1000));
        Check(!session.View(host.ParticipantId).Participants.Single(item => item.Id == second.ParticipantId).Afk, "Возврат снимает AFK");
    }

    public static void ReconnectAndHost()
    {
        var (session, host, second, observer) = Running();
        RejectJoin(() => session.Join("Дубль", "player", host.Token, NOW));
        var before = session.View(host.ParticipantId);
        session.Disconnect(host.ParticipantId, NOW);
        var returned = session.Join("Хост", "player", host.Token, NOW.AddSeconds(1));
        Check(returned.ParticipantId == host.ParticipantId && returned.SessionId == host.SessionId, "Токен возвращает к тому же участнику");
        Check(session.View(returned.ParticipantId).Planets.Single().Id == before.Planets.Single().Id, "Возвращается полный свежий снапшот своей планеты");
        Accept(session, host.ParticipantId, new("once", "step"), NOW.AddSeconds(1));
        session.Disconnect(host.ParticipantId, NOW.AddSeconds(1));
        session.Join("Хост", "player", host.Token, NOW.AddSeconds(2));
        Accept(session, host.ParticipantId, new("once", "step"), NOW.AddSeconds(2));
        Check(session.View(host.ParticipantId).Month == 1, "Кеш ID сохраняется при возвращении");
        RejectCommand(session, host.ParticipantId, new("observer-host", "transferHost", TargetParticipantId: observer.ParticipantId));
        session.Disconnect(second.ParticipantId, NOW.AddSeconds(2));
        RejectCommand(session, host.ParticipantId, new("offline-host", "transferHost", TargetParticipantId: second.ParticipantId));
        session.Join("Игрок", "player", second.Token, NOW.AddSeconds(2));
        Accept(session, host.ParticipantId, new("transfer", "transferHost", TargetParticipantId: second.ParticipantId), NOW.AddSeconds(2));
        RejectCommand(session, host.ParticipantId, new("old-host-step", "step"), NOW.AddSeconds(2));
        Accept(session, second.ParticipantId, new("new-host-speed", "speed", Speed: 2), NOW.AddSeconds(2));
        Accept(session, second.ParticipantId, new("new-host-resume", "pause", Paused: false), NOW.AddSeconds(2));
        session.Disconnect(second.ParticipantId, NOW.AddSeconds(2));
        session.Tick(NOW.AddSeconds(2.5));
        var view = session.View(host.ParticipantId);
        Check(!view.Paused && view.Speed == 2 && view.Month == 2, "Отключение хоста сохраняет скорость и расчёт");
        Check(view.Participants.Single(item => item.Id == second.ParticipantId).IsHost && !view.Participants.Single(item => item.Id == host.ParticipantId).IsHost, "Автопередачи прав нет");
        var newSession = NewSession();
        RejectJoin(() => newSession.Join("Хост", "player", host.Token, NOW));
        Check(newSession.Join("Новый", "player", null, NOW).SessionId != host.SessionId, "После нового процесса сессия другая");
    }

    public static void Filtering()
    {
        var session = NewSession();
        var first = session.Join("Первый", "player", null, NOW);
        var second = session.Join("Второй", "player", null, NOW);
        var observer = session.Join("Третий", "observer", null, NOW);
        Accept(session, second.ParticipantId, new("secret-config", "configure", Traits: [TRAIT_IDS[2]], Buildings: [BUILDING_IDS[2]]));
        Accept(session, first.ParticipantId, new("ready1", "ready", Ready: true));
        Accept(session, second.ParticipantId, new("ready2", "ready", Ready: true));
        Accept(session, first.ParticipantId, new("start", "start"));
        var view = session.View(first.ParticipantId);
        Check(view.Planets.Count == 1 && view.Planets.Single().OwnerId == first.ParticipantId, "Игрок получает только свою экономику");
        Check(session.View(observer.ParticipantId).Planets.Count == 0, "Неутверждённый наблюдатель не видит экономику");
        using var json = JsonDocument.Parse(Json(view));
        var participants = json.RootElement.GetProperty("Participants");
        foreach (var participant in participants.EnumerateArray())
        {
            Check(!participant.TryGetProperty("Traits", out _) && !participant.TryGetProperty("Buildings", out _) && !participant.TryGetProperty("Token", out _), "Публичный участник не раскрывает настройки или токен");
        }
        var serialized = Json(view);
        Check(!serialized.Contains(first.Token, StringComparison.Ordinal) && !serialized.Contains(second.Token, StringComparison.Ordinal) && !serialized.Contains(observer.Token, StringComparison.Ordinal), "Снапшот не раскрывает никакие токены");
        Check(!serialized.Contains("PRIVATE-CONTENT-ROOT", StringComparison.Ordinal) && !serialized.Contains("StateHash", StringComparison.Ordinal), "Нет полного пути контента или hash мира");
    }

    public static void PlayerCount(int count)
    {
        var session = NewSession();
        var players = Enumerable.Range(1, count).Select(index => session.Join($"Игрок {index}", "player", null, NOW)).ToArray();
        var host = players[0];
        var observer = session.Join("Наблюдатель", "observer", null, NOW);
        Check(session.View(host.ParticipantId).Participants.Count == count + 1, $"Все {count} игроков и наблюдатель входят в лобби");
        Check(players.Select(player => player.ParticipantId).Distinct().Count() == count, "У игроков разные идентификаторы");
        Check(session.View(observer.ParticipantId).Planets.Count == 0, "Ожидающий наблюдатель не получает экономику любого игрока");
        RejectCommand(session, host.ParticipantId, new("unready-start", "start"));
        foreach (var player in players)
        {
            Accept(session, player.ParticipantId, new("ready", "ready", Ready: true));
        }
        foreach (var player in players)
        {
            Accept(session, player.ParticipantId, new("not-ready", "ready", Ready: false));
            RejectCommand(session, host.ParticipantId, new($"unready-{player.ParticipantId}", "start"));
            Check(session.View(host.ParticipantId).Phase == "lobby", "Неготовность любого игрока сохраняет лобби");
            Accept(session, player.ParticipantId, new("ready-again", "ready", Ready: true));
            session.Disconnect(player.ParticipantId, NOW);
            RejectCommand(session, host.ParticipantId, new($"offline-{player.ParticipantId}", "start"));
            Check(session.View(host.ParticipantId).Phase == "lobby", "Отключение любого игрока сохраняет лобби");
            session.Join("Возврат", "player", player.Token, NOW);
        }
        Accept(session, host.ParticipantId, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
        session.Disconnect(observer.ParticipantId, NOW);
        Accept(session, host.ParticipantId, new("start", "start"));
        var started = session.View(host.ParticipantId);
        Check(started.Phase == "running" && started.Month == 0 && started.Paused, $"{count} готовых игроков запускают игру без подключённого наблюдателя");
        RejectJoin(() => session.Join("Поздний игрок", "player", null, NOW));
        session.Join("Наблюдатель", "observer", observer.Token, NOW);
        var visible = session.View(observer.ParticipantId).Planets;
        Check(visible.Count == count && visible.Select(planet => planet.Id).Distinct().Count() == count, "Наблюдатель видит отдельную планету каждого игрока");
        Check(visible.Select(planet => planet.OwnerId).Order().SequenceEqual(players.Select(player => player.ParticipantId).Order()), "У каждой планеты свой игрок-владелец");
        RejectCommand(session, observer.ParticipantId, new("observer-step", "step"));

        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            var view = session.View(player.ParticipantId);
            Check(view.Planets.Count == 1 && view.Planets.Single().OwnerId == player.ParticipantId, "Игрок видит только собственную планету");
            var planet = view.Planets.Single();
            Accept(session, player.ParticipantId, new("own-technicians", "setJobs", PlanetId: planet.Id, Job: "technician", Count: index % 5 * 0.5m));
            Accept(session, player.ParticipantId, new("own-miners", "setJobs", PlanetId: planet.Id, Job: "miner", Count: 1));
            Accept(session, player.ParticipantId, new("own-farmers", "setJobs", PlanetId: planet.Id, Job: "farmer", Count: 1));
            if (count > 1)
            {
                var foreign = session.View(players[(index + 1) % count].ParticipantId).Planets.Single().Id;
                RejectCommand(session, player.ParticipantId, new("foreign-jobs", "setJobs", PlanetId: foreign, Job: "technician", Count: 6));
            }
            var json = Json(session.View(player.ParticipantId));
            Check(players.Append(observer).All(participant => !json.Contains(participant.Token, StringComparison.Ordinal)), "Снапшот не раскрывает токены участников");
            using var document = JsonDocument.Parse(json);
            foreach (var participant in document.RootElement.GetProperty("Participants").EnumerateArray())
            {
                Check(!participant.TryGetProperty("Traits", out _) && !participant.TryGetProperty("Buildings", out _) && !participant.TryGetProperty("Token", out _), "Список участников не раскрывает их настройки или токены");
            }
        }
        Accept(session, host.ParticipantId, new("step", "step"));
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            var planet = session.View(player.ParticipantId).Planets.Single();
            var energy = index % 5 * 0.5m * 6;
            Equal(1000 + energy, planet.Balances["energy"], "Энергия рассчитывается отдельно для каждого игрока");
            Equal(1004, planet.Balances["minerals"], "Минералы рассчитываются у каждого игрока");
            Equal(1006, planet.Balances["food"], "Пища рассчитывается у каждого игрока");
            Check(planet.ReportMonth == 1 && planet.Report != null, "Каждый игрок получает отчёт первого месяца");
            Equal(energy, planet.Report!.Produced["energy"], "Отчёт соответствует собственным вакансиям игрока");
            var before = Json(session.View(player.ParticipantId).Planets);
            session.Disconnect(player.ParticipantId, NOW);
            var returned = session.Join("Возврат", "player", player.Token, NOW.AddSeconds(1));
            Check(returned.ParticipantId == player.ParticipantId && returned.SessionId == player.SessionId, "Каждый игрок возвращается к прежней сессии");
            Check(Json(session.View(returned.ParticipantId).Planets) == before, "Возврат любого игрока сохраняет его планету, ресурсы, вакансии и отчёт");
            Check(session.View(returned.ParticipantId).Participants.Count == count + 1, "Переподключение не добавляет нового участника");
        }
        RejectCommand(session, host.ParticipantId, new("self-host", "transferHost", TargetParticipantId: host.ParticipantId));
        RejectCommand(session, host.ParticipantId, new("observer-host", "transferHost", TargetParticipantId: observer.ParticipantId));
        foreach (var target in players.Skip(1))
        {
            session.Disconnect(target.ParticipantId, NOW.AddSeconds(1));
            RejectCommand(session, host.ParticipantId, new($"offline-host-{target.ParticipantId}", "transferHost", TargetParticipantId: target.ParticipantId));
            session.Join("Возврат", "player", target.Token, NOW.AddSeconds(1));
            Accept(session, host.ParticipantId, new($"transfer-{target.ParticipantId}", "transferHost", TargetParticipantId: target.ParticipantId));
            RejectCommand(session, host.ParticipantId, new($"old-host-{target.ParticipantId}", "speed", Speed: 2));
            Accept(session, target.ParticipantId, new("new-host-speed", "speed", Speed: 2));
            Check(session.View(target.ParticipantId).Participants.Single(participant => participant.IsHost).Id == target.ParticipantId, "Права передаются любому подключённому игроку");
            host = target;
        }
    }

    public static void MonthFailure()
    {
        var session = NewSession(100000);
        var host = session.Join("Хост", "player", null, NOW);
        var second = session.Join("Игрок", "player", null, NOW);
        Accept(session, second.ParticipantId, new("upkeep", "configure", Traits: [], Buildings: [BUILDING_IDS[0]]));
        Accept(session, host.ParticipantId, new("r1", "ready", Ready: true));
        Accept(session, second.ParticipantId, new("r2", "ready", Ready: true));
        Accept(session, host.ParticipantId, new("start", "start"));
        var firstBefore = Json(session.View(host.ParticipantId).Planets);
        var secondBefore = Json(session.View(second.ParticipantId).Planets);
        Accept(session, host.ParticipantId, new("resume", "pause", Paused: false));
        session.Tick(NOW.AddSeconds(1));
        var view = session.View(host.ParticipantId);
        Check(view.Month == 0 && view.Paused && !string.IsNullOrWhiteSpace(view.LastError), "Ошибка месяца сохраняет календарь и ставит паузу");
        Check(Json(view.Planets) == firstBefore && Json(session.View(second.ParticipantId).Planets) == secondBefore, "Ошибка другой страны не оставляет частично применённых ресурсов или отчётов");
        var error = view.LastError ?? throw new InvalidOperationException("Отсутствует общая ошибка экономики.");
        Check(!error.Contains("energy", StringComparison.OrdinalIgnoreCase) && !error.Contains(second.ParticipantId.ToString(), StringComparison.Ordinal), "Общая ошибка не выдаёт чужую экономику");
    }

    public static void UpkeepBreakdown()
    {
        var session = NewSession(2, 1);
        var host = session.Join("Хост", "player", null, NOW);
        var second = session.Join("Игрок", "player", null, NOW);
        Accept(session, host.ParticipantId, new("building", "configure", Traits: [], Buildings: [BUILDING_IDS[0]]));
        Accept(session, host.ParticipantId, new("ready1", "ready", Ready: true));
        Accept(session, second.ParticipantId, new("ready2", "ready", Ready: true));
        Accept(session, host.ParticipantId, new("start", "start"));
        Accept(session, host.ParticipantId, new("step", "step"));
        var report = session.View(host.ParticipantId).Planets.Single().Report ?? throw new InvalidOperationException("Нет отчёта первого месяца.");
        Equal(2, report.JobUpkeep["energy"], "Две занятые единицы technician создают JobUpkeep");
        Equal(2, report.BuildingUpkeep["energy"], "Здание сохраняет собственный BuildingUpkeep");
        Equal(4, report.Upkeep["energy"], "Общий расход суммирует источники один раз");
        using var json = JsonDocument.Parse(Json(session.View(host.ParticipantId)));
        var jsonReport = json.RootElement.GetProperty("Planets")[0].GetProperty("Report");
        Check(jsonReport.TryGetProperty("JobUpkeep", out _) && jsonReport.TryGetProperty("BuildingUpkeep", out _) && jsonReport.TryGetProperty("Upkeep", out _), "JSON содержит разбивку и совместимый итог");
    }

    private static (GameSession Session, JoinGrant Host, JoinGrant Second, JoinGrant Observer) Running()
    {
        var session = NewSession();
        var host = session.Join("Хост", "player", null, NOW);
        var second = session.Join("Игрок", "player", null, NOW);
        var observer = session.Join("Наблюдатель", "observer", null, NOW);
        Accept(session, host.ParticipantId, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
        Accept(session, host.ParticipantId, new("r1", "ready", Ready: true));
        Accept(session, second.ParticipantId, new("r2", "ready", Ready: true));
        Accept(session, host.ParticipantId, new("start", "start"));
        return (session, host, second, observer);
    }

    private static GameSession NewSession(decimal buildingUpkeep = 2, decimal jobUpkeep = 0)
    {
        var manifest = new ContentManifest("4.5.2", "synthetic-session", [new ManifestFile("PRIVATE-CONTENT-ROOT", "synthetic", 0)], ["Синтетическая экономическая проекция для проверки сессии"]);
        var resources = new[] { new ResourceDefinition("energy", 1000000), new ResourceDefinition("minerals", 1000000), new ResourceDefinition("food", 1000000) };
        var jobs = new[]
        {
            new JobDefinition(JOB_IDS[0], "worker", 100, Amounts(("energy", 6)), Amounts(("energy", jobUpkeep))),
            new JobDefinition(JOB_IDS[1], "worker", 100, Amounts(("minerals", 4)), Amounts()),
            new JobDefinition(JOB_IDS[2], "worker", 100, Amounts(("food", 6)), Amounts())
        };
        var buildings = BUILDING_IDS.Select((id, index) => new BuildingDefinition(id, Amounts(("energy", buildingUpkeep)), new[] { new Modifier("worker", resources[index].Id, "produces", 0.5m, 0) })).ToArray();
        var content = new EconomyProjection(manifest, resources, jobs, new[] { new CategoryDefinition("worker", null) }, buildings, [], 100);
        var catalog = TRAIT_IDS.Select((id, index) => new TraitWorkforceProjection(manifest, new[] { id }, JOB_IDS.ToDictionary(job => job, job => job == JOB_IDS[index] ? 0.15m : 0), []))
            .ToDictionary(projection => projection.Traits.Single());
        return new GameSession(content, catalog);
    }

    private static IReadOnlyDictionary<string, decimal> Amounts(params (string Id, decimal Value)[] amounts)
    {
        return amounts.ToDictionary(item => item.Id, item => item.Value);
    }

    private static void Accept(GameSession session, int participant, ClientCommand command, DateTimeOffset? now = null)
    {
        var result = session.Execute(participant, command, now ?? NOW);
        Check(result.Accepted, $"Команда {command.Type}/{command.CommandId} отклонена: {result.Message}");
    }

    private static void RejectCommand(GameSession session, int participant, ClientCommand command, DateTimeOffset? now = null)
    {
        var result = session.Execute(participant, command, now ?? NOW);
        Check(!result.Accepted && !string.IsNullOrWhiteSpace(result.Message), $"Команда {command.Type}/{command.CommandId} должна явно отклоняться");
    }

    private static void RejectJoin(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Ожидался явный отказ подключения.");
    }

    private static string Json(object value)
    {
        return JsonSerializer.Serialize(value);
    }

    private static void Equal(decimal expected, decimal actual, string message)
    {
        Check(expected == actual, $"{message}: ожидалось {expected}, получено {actual}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
