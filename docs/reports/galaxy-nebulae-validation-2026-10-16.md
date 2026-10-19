# Процедурные туманности — проверка первого среза

16 октября 2026. Ветка `feature/galaxy-generation-content`, база master `5e8f5af`.
Контракт: [GALAXY-NEBULAE](../GALAXY-NEBULAE.md).

## Область проверки

Автор кода — `/root/nebula_implementation`; независимый reviewer — `/root/nebula_review`;
интеграция и документация — `/root`. Настройки ответственных агентов унаследованы,
изменения модели/effort не запрашивались; фактические runtime model/usage недоступны.
Пользователь разрешил реализацию прямым запросом; master и основной checkout не меняются.

Туманности — геометрия/имена/membership в Core snapshot и harness. Это не завершённое
игровое поведение: renderer, сенсоры, сетевые DTO и кнопки лобби этим срезом не изменены.
Нулевой count у существующих Session callers сохраняется.

## Доказательства

Реверс §18.5 в `native-generation-reverse-addendum-452.md`, SHA-256
`2AD39F747DEE73C7128DC1D9FCA2D77A64CBDD70295DF4E048ED179142D4C5D3`.
Контракт отдельно проверен reviewer: count сценариев 2/3/6/8/10, radius60,
minimum separation100/100/200/200/200, ordered names pool и правила границ подтверждены.
Пустой pool не расходует names draw в нашем supplied-seed профиле; native ошибочная
ветвь не заявлена восстановленной.

Тесты используют имеющийся внешний Triangle worker только для чтения, SHA-256
`CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71`.
Бинарный файл, игровые scripts и производные ассеты в Git не включаются.

## Результаты

Срез реализован. `/root/nebula_review` прочитал actual diff всех семи code/test файлов,
включая новые, и подтвердил совпадение финальных hashes с просмотренными.
Actionable замечаний нет. Root также прочитал реализацию и проверки; исходное дерево
было чистым, чужих правок нет. Имена/координаты/membership экспортируются в
`galaxy-world-v2`; прочие special families по-прежнему отклоняются.

| Проверка | Результат |
|---|---|
| `dotnet run --project tests/Stellaris.GalaxyGenerationChecks --` | 58 PASS у автора; root повторил собранный executable через `--no-build`: 58 PASS |
| Тот же project: `-- --world-checks game-root worker sha256` | 5 world checks PASS у автора: tiny shapes/densities/seeds, huge 1000, туманности/replay/membership/metadata, zero-count regression, startup seam |
| `dotnet build tools/Stellaris.GalaxyHarness/Stellaris.GalaxyHarness.csproj --no-restore` | 0 warnings/errors у автора; Core собран как зависимость |
| `dotnet build src/Stellaris.Server/Stellaris.Server.csproj -c Release --nologo -v quiet` | 0 warnings/errors у root, включая Session integration compile |
| Реальный `GalaxyHarness --generate` с tiny/elliptical, seed `nebula-smoke-2026-10-16`, одним игроком и `Nebulae=2` | 200 систем, 2122 тела, 285 lanes, 2 туманности, 29 member systems, 3 placement draws, 2 names draws, 0 diagnostics |
| Независимый разбор экспортированного JSON у root | version v2, все ссылки разрешены, принадлежность без повторов, каждый member строго внутри радиуса |
| `git diff --check` | PASS |

Smoke fingerprint: `63BAA18CE39EE2665861C796236816DC740F1F1F3D48DFD1E14F02B4B6748B35`.
Request/world JSON остаются в ignored `artifacts/galaxy-nebulae/`; не входят в Git.
Source fingerprint smoke: `E73B940A7FB71507AD6EC9184E2F980E00A627E0D00B1B0D89CA4283DAD5C7B6`.
Названия оригинальных туманностей и исходные scripts в отчёт не скопированы.

## Ограничения и среда

- Symlink fixture: SKIP, Windows не разрешил создание ссылки (privilege 1314).
  Guards `LinkTarget`/reparse в коде проверены ревью, фактическая fixture не пройдена.
- Стандартный `--triangle-worker` suite остановился после семи PASS на отсутствии
  companion test executables возле production worker. Это не PASS полного Triangle suite.
  Новый `--world-checks` выполняет существующий GalaxyWorldChecks с production worker;
  данные реальной генерации и smoke проверены, backend не изменён.
- Первая сборка Server остановилась на отсутствии node_modules. Выполнен штатный
  `pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts --offline`:
  2 зависимости из локального кеша, 0 загрузок; lockfile не менялся.
  Повторная Release сборка прошла. Пользовательские серверы не запускались/не останавливались.
- Browser/GPU QA не проводилась: Session/UI/renderer в этом срезе не меняются.
  Существующий клиент пока не получает и не рисует созданные туманности.
- Native global/script RNG parity, полное vanilla execution, сенсоры и прочие
  игровые эффекты туманностей не заявляются. Генерация доступна через Core API/harness,
  ненулевой count не включён автоматически в production lobby.

## NEBULA-02 — подключение к игре, 16 октября

Поздний запрос пользователя расширил scope до последовательных готовых игровых
срезов, с небольшим числом целевых проверок. Ограничения первого среза выше
исторические: туманности теперь включены в production lobby/settings/projection/map.
Автор `/root/nebula_playable`, review `/root/nebula_playable_review`, root — QA/docs.

- Source default tiny=2; host может выбрать 0..scenario maximum. Optional старый
  параметр означает default выбранного scenario, explicit0 сохраняется.
- Изменение count использует прежний candidate/Ready/ACK lifecycle; invalid count
  не публикует частичное состояние. DTO фильтрует membership по разрешённым systems.
- Карта рисует procedural cloud по реальному center/radius, локализованную подпись;
  информация системы показывает принадлежность. Объекты не перехватывают picking,
  owned GPU resources освобождаются при перестроении. Нативный shader не заявляется.

Целевые проверки автора: `GalaxyM3Checks --nebula-only` — 1 focused smoke
(host/defaults/invalid atomicity/prepare/privacy/ACK/start/count0);
`M3ServerChecks --nebula-wire-only` — 6 wire checks;
`node --test tools/asset-probe/m3-nebulae.test.mjs` — 3 PASS;
финальная Server Release сборка — 0 warnings/errors.
Широкие прежние 58/560 suites не повторялись для этого среза.

Root проверил в IAB production UI на собственном сервере loopback5188:
default2 → count0 → count1 → reload/connect → server count1.
Первый сценарий выявил отсутствующий transport whitelist для `galaxyNebulae`:
соединение разрывалось с InvalidDataException. Whitelist/type gate исправлены,
добавлены wire assertions; финальный browser повтор прошёл.

Root также проверил production `createNebulaGroup` на WebGL в ignored visual harness
с настоящим Core output: 200 систем, 2 облака, 29 members, подписи видимы;
переключение system/count0 удаляет слой и освобождает его ресурсы. Это проверка
настоящего renderer module, но не полный старт двух клиентов с локальным импортом
ассетов. Последний путь в браузере не пройден; backend smoke проводит ACK/start.
Пользовательский сервер/порты не затронуты; собственные 5187/5188 остановлены.

Сенсоры и другие ещё не реализованные системы симуляции не добавлялись.
Actual-diff review всех 16 code/test paths: замечаний нет, база `6d1e31f`.
Freeze digest упорядоченного `relative-path lowercase-sha256` списка:
`8ff1943ca6b2cf9dd2f501a367ed57174bb1e62107ea09e8bd1af449ea593825`.
