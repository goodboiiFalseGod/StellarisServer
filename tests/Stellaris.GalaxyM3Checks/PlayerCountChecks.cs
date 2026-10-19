using System.Reflection;
using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class PlayerCountChecks
{
    public static int Run(string root, M3GalaxyRuntime galaxy, M3GalaxySettings settings)
    {
        // Общий source snapshot импортируется один раз для всех размеров лобби.
        var source = new ContentSession(root);
        var content = new NeutralEconomyImporter(source).Import(100);
        var traits = new[] { "trait_agrarian", "trait_industrious", "trait_ingenious" }.ToDictionary(id => id,
            id => new TraitWorkforceImporter(source).Import([id], content.Jobs));
        source.VerifyStable();
        var manifest = new M3AssetManifestBuilder().BuildAsync(root).GetAwaiter().GetResult();
        var startup = M3StartupCatalog.ImportAsync(root).GetAwaiter().GetResult();
        var catalog = new PlanetEconomyImporter().ImportAsync(root).GetAwaiter().GetResult();
        var research = new TechnologyImporter().Import(root);
        var rarePolicy = M3RareDepositResearchPolicy.Import(root);
        var economy = new M3BaselineEconomyProfile(catalog, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, research.CategoryParents);
        var construction = M3ConstructionContent.ImportAsync(root, catalog).GetAwaiter().GetResult();
        var keys = M3TechnologyKeys.Import(root);
        var legacyConstruction = new ConstructionDefinitionImporter().ImportAsync(root).GetAwaiter().GetResult();
        var manifests = new Dictionary<string, M3AssetManifest>(StringComparer.Ordinal);
        M3AssetManifest GeneratedManifest(IReadOnlyList<string> classes, IReadOnlyList<string> objectIds)
        {
            var key = string.Join("\n", classes.Order(StringComparer.Ordinal)) + "\n--objects--\n" + string.Join("\n", objectIds.Order(StringComparer.Ordinal));
            if (!manifests.TryGetValue(key, out var generated))
            {
                generated = new M3AssetManifestBuilder().BuildAsync(root, new GeneratedWorldVisualRequirements(classes, objectIds.Count == 0 ? null : objectIds)).GetAwaiter().GetResult();
                manifests.Add(key, generated);
            }
            return generated;
        }

        var passed = 0;
        foreach (var playerCount in new[] { 1, 2, 3, 10, 20 })
        {
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException($"M3, игроков {playerCount}: {message}");
            }
            var session = new M3GameSession(content, traits, manifest, construction: legacyConstruction,
                constructionImportAttempted: true, startup: startup, sourceEconomy: economy, constructionContent: construction,
                researchCatalog: research, technologyKeys: keys, galaxy: galaxy, galaxyManifest: GeneratedManifest,
                rareDepositResearchPolicy: rarePolicy);
            var now = TimeSpan.Zero;
            var serial = 0;
            // Первый observer намеренно сдвигает participant IDs относительно economy slots.
            var observer = session.Join("Наблюдатель", "observer", null, now);
            var players = Enumerable.Range(1, playerCount).Select(index => session.Join($"Игрок {index}", "player", null, now)).ToArray();
            var host = players[0];
            M3SessionView View(M3JoinGrant who) => session.View(who.ParticipantId);
            M3CommandResult Send(M3JoinGrant who, M3ClientCommand command) => session.Execute(who.ParticipantId, who.ConnectionId,
                command with { CommandId = "player-count-" + ++serial }, now);
            void Accept(M3JoinGrant who, M3ClientCommand command)
            {
                var result = Send(who, command);
                Check(result.Accepted, $"{command.Type}: {result.Code}: {result.Message}");
            }
            void Reject(M3JoinGrant who, M3ClientCommand command, string? code = null)
            {
                var result = Send(who, command);
                Check(!result.Accepted && (code is null || result.Code == code), $"{command.Type} должен отклоняться ({code}), получено {result.Code}");
            }
            M3ClientCommand Ready(long revision) => new("", "ready", Ready: true, ExpectedLobbyRevision: revision);
            M3ClientCommand InitialStart() => new("", "start", ExpectedLobbyRevision: View(host).Galaxy!.LobbyRevision);
            M3ClientCommand Bound(string type)
            {
                var view = View(host);
                return new("", type, Ready: type == "assetReady" ? true : null,
                    ProfileId: view.AssetProfile.ProfileId, ProfileDigest: view.AssetProfile.ProfileDigest,
                    GalaxyWorldFingerprint: view.Galaxy!.WorldFingerprint,
                    ExpectedLobbyRevision: type == "start" ? view.Galaxy!.LobbyRevision : null);
            }

            Check(View(observer).Planets.Count == 0, "ожидающий наблюдатель получает чужую экономику");
            foreach (var player in players) Accept(player, new("", "selectEmpireTemplate", TemplateId: "economic"));
            Accept(host, new("", "setGalaxySettings", GalaxySeed: settings.Seed, GalaxyScenarioId: settings.ScenarioId,
                GalaxyShapeId: settings.ShapeId, GalaxyDensity: settings.Density));
            var revision = View(host).Galaxy!.LobbyRevision;
            Accept(host, new("", "approveObserver", TargetParticipantId: observer.ParticipantId));
            Check(View(host).Galaxy!.LobbyRevision == revision, "наблюдатель изменил ревизию голосования игроков");
            foreach (var player in players) Accept(player, Ready(revision));
            var last = players[^1];
            session.Disconnect(last.ParticipantId, last.ConnectionId, now);
            var disconnected = playerCount == 1 ? null : View(host);
            Check((disconnected is null || disconnected.Participants.All(participant => participant.Id != last.ParticipantId)) &&
                (disconnected is null || disconnected.Participants.Where(participant => participant.Role == "player").All(participant => !participant.Ready)) &&
                Send(last, new("removed-ready", "ready", Ready: true)).Code == "OUT_OF_SCOPE",
                "отключение игрока удалило место и сбросило голоса");
            var oldTokenRejected = false;
            try { session.Join("Возврат", "player", last.Token, now); }
            catch (InvalidOperationException) { oldTokenRejected = true; }
            Check(oldTokenRejected, "старый токен не возвращает удалённое место в лобби");
            last = session.Join("Возврат", "player", null, now);
            players[^1] = last;
            if (playerCount == 1) host = last;
            var returned = View(host);
            Check(last.ParticipantId > players.Where(player => player.ParticipantId != last.ParticipantId).Select(player => player.ParticipantId).DefaultIfEmpty(0).Max() &&
                returned.Galaxy!.LobbyRevision > revision && returned.Participants.Where(participant => participant.Role == "player").All(participant => !participant.Ready),
                "новый вход получил монотонный ID и текущую ревизию без старых голосов");
            Reject(last, Ready(revision), "STALE_LOBBY_REVISION");
            Accept(last, new("return-template", "selectEmpireTemplate", TemplateId: "economic"));
            revision = View(host).Galaxy!.LobbyRevision;
            foreach (var player in players.SkipLast(1)) Accept(player, Ready(revision));
            Reject(host, InitialStart(), "INVALID_PHASE");
            Check(View(host).Galaxy!.WorldFingerprint is null && View(host).Phase == "lobby", "неполный состав готовых игроков опубликовал мир");
            Accept(last, Ready(revision));
            Accept(host, InitialStart());
            var prepared = View(host);
            Check(prepared.Phase == "lobby" && prepared.Galaxy!.StartPending && !string.IsNullOrEmpty(prepared.Galaxy.WorldFingerprint),
                "общая готовность не подготовила мир в ожидании ассетов");
            var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            Check(candidate.Generated.Homes.Count == playerCount && candidate.Space.Colonies.Count == playerCount && candidate.Space.Fleets.Count == playerCount,
                "число стартовых владений отличается от состава игроков");
            Check(candidate.Generated.Homes.Select(home => home.SystemId).Distinct(StringComparer.Ordinal).Count() == playerCount &&
                candidate.Generated.Homes.Select(home => home.BodyId).Distinct(StringComparer.Ordinal).Count() == playerCount,
                "игроки делят домашнюю систему или мир");
            Check(candidate.Space.Colonies.Select(colony => colony.EconomyPlanetId).Distinct().Count() == playerCount, "повторяется economy planet ID");
            Check(players.Any(player => View(player).Planets.Single().Id != player.ParticipantId), "сценарий не разделил participant/economy IDs");
            foreach (var player in players)
            {
                var home = candidate.Generated.Homes.Single(item => item.ParticipantId == player.ParticipantId);
                var colony = candidate.Space.Colonies.Single(item => item.OwnerParticipantId == player.ParticipantId);
                var plan = candidate.Startup[colony.Id];
                var planet = View(player).Planets.Single();
                Check(planet.OwnerId == player.ParticipantId && colony.EconomyPlanetId == planet.Id,
                    "participant ID подменил привязку economy planet");
                Check(colony.BodyId == home.BodyId && plan.OwnerParticipantId == player.ParticipantId && plan.Homeworld.PlanetClass == home.ClassId &&
                    plan.Homeworld.PlanetSize == home.Size && plan.Homeworld.InitializerId == home.InitializerId && plan.Plan.Gates.Count == 0 && plan.Buildings.Count > 0,
                    "generated home не совпадает с source startup игрока");
                Check(candidate.Space.Bodies.Count(body => body.Kind == "station" && body.OwnerParticipantId == player.ParticipantId && body.SystemId == home.SystemId) == 1,
                    "у игрока нет собственной стартовой станции");
            }
            foreach (var player in players.SkipLast(1))
            {
                Accept(player, Bound("assetReady"));
                Check(View(host).Phase == "lobby", "ACK без последнего игрока запустил сессию");
            }
            Reject(last, Bound("assetReady") with { GalaxyWorldFingerprint = "stale-world" }, "STALE_GALAXY_CANDIDATE");
            Reject(last, Bound("assetReady") with { ProfileDigest = "wrong-profile" }, "STALE_GALAXY_CANDIDATE");
            Reject(host, Bound("start"), "INVALID_PHASE");
            Accept(last, Bound("assetReady"));
            Check(View(host).Participants.Where(p => p.Role == "player").All(p => p.Ready && p.AssetReady), "последний ACK сбросил согласие игроков");
            // Автоматическую отправку второго protocol Start проверяет клиентский тест.
            Accept(host, Bound("start"));
            var running = View(host);
            Check(running.Phase == "running" && running.ElapsedDay == 0 && running.Month == 0 && running.Paused && running.LastError is null,
                "мир не стартовал на паузе с нулевого дня");
            Check(!running.Participants.Single(p => p.Id == observer.ParticipantId).AssetReady, "наблюдатель неожиданно подтвердил импорт");
            Accept(observer, Bound("assetReady"));
            var general = View(observer);
            Check(general.Planets.Count == playerCount && general.Spatial.Colonies.Count == playerCount && general.Spatial.Fleets.Count == playerCount,
                "общая перспектива наблюдателя потеряла игроков");
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var wireSizes = new List<(string Label, int Bytes)>();
            int Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions).Length;
            void RecordBudget(string label, M3SessionView view)
            {
                var bytes = Bytes(new { type = "snapshot", state = view });
                wireSizes.Add((label, bytes));
                if (playerCount == 20)
                {
                    Console.WriteLine($"WIRE M3 players={playerCount} pov={label} snapshot={bytes} " +
                        $"state.planets={Bytes(view.Planets)} state.catalog={Bytes(view.Catalog)} " +
                        $"state.spatial={Bytes(view.Spatial)} state.development={Bytes(view.Development)} state.research={Bytes(view.Research)}");
                }
            }
            RecordBudget("observer-general", general);
            Reject(observer, new("", "stepDay"));
            foreach (var player in players)
            {
                var view = View(player);
                RecordBudget("player-" + player.ParticipantId, view);
                var planet = view.Planets.Single();
                var development = view.Development!.Colonies.Single();
                Check(planet.OwnerId == player.ParticipantId && development.OwnerParticipantId == player.ParticipantId && development.EconomyPlanetId == planet.Id &&
                    view.Research!.Empires.Single().ParticipantId == player.ParticipantId, "приватная экономика/development/research перешла другому игроку");
                Check(development.Economy!.Jobs.Any(job => job.Assigned > 0), "source startup не создал занятую Workforce");
                Check(view.Spatial.Colonies.Single(colony => colony.OwnerParticipantId == player.ParticipantId).EconomyPlanetId == planet.Id &&
                    view.Spatial.Colonies.Where(colony => colony.OwnerParticipantId != player.ParticipantId).All(colony => colony.EconomyPlanetId is null),
                    "spatial economy binding раскрывает чужую планету");
            }
            var largest = wireSizes.MaxBy(item => item.Bytes);
            var largestPlayer = wireSizes.Where(item => item.Label.StartsWith("player-", StringComparison.Ordinal)).MaxBy(item => item.Bytes);
            Console.WriteLine($"WIRE M3 players={playerCount} views={wireSizes.Count} max={largest.Bytes} pov={largest.Label} " +
                $"maxOwner={largestPlayer.Label} ownerBytes={largestPlayer.Bytes} observerGeneralBytes={wireSizes[0].Bytes}");
            Accept(observer, new("", "observerView", TargetParticipantId: last.ParticipantId));
            var perspective = View(observer);
            Check(perspective.Planets.Single().OwnerId == last.ParticipantId && perspective.Development!.Colonies.Single().OwnerParticipantId == last.ParticipantId &&
                perspective.Research!.Empires.Single().ParticipantId == last.ParticipantId, "наблюдатель не получил приватную перспективу последнего игрока");
            Accept(observer, new("", "observerView", TargetParticipantId: 0));
            if (playerCount > 1)
            {
                Accept(host, new("", "transferHost", TargetParticipantId: last.ParticipantId));
                Reject(host, new("", "stepDay"));
                host = last;
                Check(View(host).Participants.Single(p => p.IsHost).Id == last.ParticipantId, "права не передались последнему игроку");
            }
            Accept(host, new("", "stepDay"));
            Check(View(host).ElapsedDay == 1 && View(host).Month == 0, "шаг дня преждевременно продвинул месяц");
            for (var day = 1; day < 30; day++) Accept(host, new("", "stepDay"));
            foreach (var player in players)
            {
                var view = View(player);
                var planet = view.Planets.Single();
                Check(view.ElapsedDay == 30 && view.Month == 1 && view.Paused && view.LastError is null && planet.ReportMonth == 1 &&
                    planet.Report is not null && planet.Report.Produced.Values.Any(value => value > 0), "у игрока не рассчитан реальный source месяц");
            }
            var beforeReturn = View(last);
            var monthObserverBytes = Bytes(new { type = "snapshot", state = View(observer) });
            Check(monthObserverBytes <= 1024 * 1024, "Месячный общий снимок наблюдателя превышает 1 MiB.");
            Console.WriteLine($"WIRE M3 month1 players={playerCount} observerGeneralBytes={monthObserverBytes}");
            var ownPlanet = JsonSerializer.Serialize(beforeReturn.Planets);
            var ownColony = beforeReturn.Development!.Colonies.Single().ColonyId;
            var ownFleet = beforeReturn.Spatial.Fleets.Single(fleet => fleet.OwnerParticipantId == last.ParticipantId).Id;
            session.Disconnect(last.ParticipantId, last.ConnectionId, now);
            var removed = View(host);
            Check(removed.Participants.Single(participant => participant.Id == last.ParticipantId) is { Connected: false, Ready: false },
                "отключение в running сохраняет участника, но снимает готовность");
            var rejoined = session.Join("Возврат", "player", last.Token, now);
            Check(rejoined.ParticipantId == last.ParticipantId && rejoined.SessionId == last.SessionId && rejoined.ConnectionId != last.ConnectionId,
                "running reconnect сохраняет текущего участника и сессию");
            last = rejoined;
            host = rejoined;
            players[^1] = rejoined;
            Accept(last, Bound("assetReady"));
            var fresh = View(last);
            Check(JsonSerializer.Serialize(fresh.Planets) == ownPlanet && fresh.Development!.Colonies.Single().ColonyId == ownColony &&
                fresh.Spatial.Fleets.Single(fleet => fleet.OwnerParticipantId == last.ParticipantId).Id == ownFleet && fresh.Participants.Count == playerCount + 1,
                "возврат потерял владения, экономику или добавил участника");
            var serialized = JsonSerializer.Serialize(fresh);
            Check(players.Append(observer).All(player => !serialized.Contains(player.Token, StringComparison.Ordinal)), "снапшот раскрыл токен участника");
            var rejectedJoin = false;
            try { session.Join("Поздний игрок", "player", null, now); }
            catch (InvalidOperationException) { rejectedJoin = true; }
            Check(rejectedJoin && View(host).Participants.Count == playerCount + 1, "поздний новый игрок вошёл в running");
            passed++;
            Console.WriteLine($"PASS M3: generated лобби → source startup → ACK → месяц → возврат, игроков {playerCount}");
        }
        return passed;
    }
}
