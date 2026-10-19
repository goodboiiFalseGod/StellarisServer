# M3 — сессия v2 и запуск

Инженерная детализация [пространственного протокола](M3-SPATIAL-PROTOCOL.md). Реализовать отдельно от GameSession v1; не менять единицы/поведение M2. Это контракт, не отчёт о готовности.

## Запуск и границы

Сервер принимает `--mode m2|m3`, по умолчанию m2. Старые команды запуска остаются рабочими. M3: тот же обязательный `--game-root`, `--join-code` 8..64 и один `--urls` HTTP loopback (localhost/127.0.0.1/[::1], без userinfo/query/fragment, path=/); прочие hosts отклоняются до listen. Порт по умолчанию 5080, выбор другого через --urls. `/health` показывает активный protocol=2/mode=m3; `/ws` принимает только join protocol=2; `/m3/asset-manifest` выдаёт immutable metadata из startup builder. Режим m2 не публикует этот endpoint и не строит M3-профиль. Игровые файлы не являются webroot.

M3Session получает импортированную EconomyProjection/traits и проверенный M3AssetManifest. Собственная EconomyWorld, участники, command cache, gate и SpaceWorld; GameSession не оборачивать как таймер месячных шагов. Каталог/начальные запасы/600 Pop/600 обычной Workforce/2 вакансии каждой профессии/настройки traits и зданий наследуются из M2. Это ограниченная лабораторная экономика M1, не новая полная планетарная система. Colony→economyPlanetId берётся из реального EntityId.Planet.Index владельца; pure fixture slots 1/2 не сериализуются как реальные ID.

## Время, соединения и команды

Методы Join/Disconnect/Execute/Tick/View сериализуются одним lock. Для тестов явный неубывающий TimeSpan monotonicNow; production получает его из Stopwatch, не UTC. Время тайм-аутов и дневного deadline единое монотонное; назад — явная ошибка caller, не отрицательные интервалы. При изменении pause/speed/start/stepDay deadline сбрасывается. Один Tick исполняет максимум один день; отставание не догоняется пачкой. AFK спустя 60 секунд disconnect; приказы продолжаются. Хост сам не передаётся и пауза не меняется от disconnect. Rejoin с тем же token возвращает участника, но получает новый connectionId (случайный opaque), assetReady=false и lobby ready=false. Disconnect/Execute с прежним connectionId не отключает/не мутирует новое соединение.

Command DTO v2 сохраняет поля M2 и добавляет profileId/profileDigest, fleetId/systemId/targetSystemId, x/z. Для каждого type допустимы только его собственные поля: transport проверяет фактически присутствующие JSON names, включая null и повторы. Pure Execute дополнительно отклоняет ненулевые посторонние поля. Отсутствующие обязательные аргументы/unknown type/step v1 — INVALID_COMMAND; step объясняет stepDay. Все игровые мутации требуют assetReady текущего connectionId; assetReady/pause/передача прав/approveObserver разрешены для управления доступом без графики, но ready/start/configure/setJobs/movement/stepDay/speed требуют подтверждённого профиля. Наблюдателю assetReady допустим, pendingObserver не получает spatial/economy; после approve получает публичный мир и обе экономики.

assetReady ready=true проверяет profileId+digest, false снимает import-ready и lobby-ready. Любое новое соединение начинает с false. Command cache участника хранит fingerprint всех DTO-полей и исходный result, максимум 10000 IDs. Ошибки тоже кешируются. Повтор прежнего assetReady на новом соединении возвращает старый ACK, но НЕ меняет готовность нового соединения; клиент обязан выдать новый ID. Повтор принятого move/step не меняет tick/sequence/orderVersion. accepted-команда получает следующий commandSequence, appliedTick и stateSequence; отвергнутая не получает commandSequence. ID_CONFLICT без мутации; cache не содержит token в snapshot.

## Атомарный день

Под lock сначала вычисляются nextTick/nextSequence/календарь/новый SpaceWorld и будущий deadline. Затем на day%30==0 вызывается атомарный EconomyWorld.AdvanceMonth. Это ПОСЛЕДНЯЯ потенциально ошибочная gameplay-операция; после неё только присвоения уже подготовленных значений. Не делать checked/новые коллекции/проекции/внешние callbacks после экономического commit внутри транзакции. На промежуточных днях экономика не вызывается. Ошибка сохраняет spatial/day/tick/month/балансы/reports и уже принятые приказы, публикует безопасную lastError, paused=true и sequence+1. Метрика Stopwatch измеряет попытку целиком; ошибочный день не получает appliedTick нового дня.

## JSON и фильтрация

Union сериализуется явными DTO, не через базовые records без полиморфной настройки:

- location: `{kind:'system',systemId,position:{x,z}}` либо `{kind:'transit',fromSystemId,toSystemId,progress}`;
- target: null, `{kind:'point',systemId,position:{x,z}}` либо `{kind:'system',systemId}`;
- pendingAction: null, `{kind:'stop'}` либо `{kind:'destination',systemId}`.

Phase — camelCase строки существующих пяти фаз. Body position публикуется как localPosition, не внутреннее Position. Date только year/month/day; completedMonths передаётся полем month. Movement отсутствует у чужого флота; economyPlanetId отсутствует у чужой колонии, не null-подстановка секретного ID. StarBodyId только с видимым body. Проектор — отдельная pure-функция с явным разрешённым набором систем и тел; runtime известного fixture разрешает все 16, тест ограничивает. При скрытой route/target/pending ссылке не передавать её: movement исключается целиком, не частично искажённый приказ. Скрытый transit endpoint исключает весь флот. Colony/body/endpoints не оставляют dangling ссылок. Ошибки не содержат исходных скрытых ID.

## Транспорт и приёмка

Отдельный v2 handler: strict UTF-8 JSON object, duplicate keys/unknown fields/types отклоняются, ping допускает только type. Один send loop; bounded FIFO 64 control replies и один replaceable pending snapshot, снимки максимум раз в 200 мс. Replies не теряются, переполнение разрывает соединение; send timeout 3 с. Сериализованный outgoing ≤1 MiB проверяется до enqueue/send. Тайм-аут join 10 с, следующего входа 30 с, ≤8 sockets/20 messages в секунду; same-origin и join code без URL/token logs наследуются. На shutdown ticker/connection tasks завершаются с наблюдением исключений.

Проверить: весь M2 regression; новые join/version/readiness/reconnect/старое соединение; observers/host disconnect; 30/360 дней и monotonic clock, пауза/скорость/step cache; ошибка экономики на 30-м дне одновременно с движением без частичного commit; owned/foreign/hidden/unknown отрицательные команды; explicit JSON всех union branches; скрытые links/target/route/pending, реальная colony→planet связь 1/4; slow consumer/ограниченные очереди/size/rate/duplicate/unknown payload. Browser/client integration и FPS отдельны.

Детерминированная проверка slow-send использует WebSocket test double с отменяемым зависшим SendAsync: production deadline 3 с отменяет переданный токен, внешняя отмена прерывает раньше, oversized payload не вызывает socket.SendAsync. Production API ради теста не расширяется; reflection допускается в harness для закрытых send helpers. Управляемая задержка отдельно проверяет FIFO control replies, единственную конкурентную отправку и сохранение только последнего pending snapshot. Это проверка production send-path, не измерение TCP-буферов/физической медленной сети. Host `localhost` проверяется без учёта регистра, как и валидатор URL/Origin; прочие hosts по-прежнему запрещены.
