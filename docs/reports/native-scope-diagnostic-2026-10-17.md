# Приватная диагностика PLANET-SCOPE-002 / PRIMITIVE-FACT-001

17 октября 2026. Прямое разрешение пользователя передано текущим интегратором `01a14667-53c7-7601-a7ba-3b258065dd1f`: подготовить и выполнить эти два original-game опыта отдельным приватным модом вне установки. Повторное разрешение на мод не требуется. Это не изменение production-сервера и не разрешение менять профессии, пользовательский playset или существующие процессы.

## Реальный запуск v1: полученные данные и ограничения

Пользователь выполнил ручную native-сессию; интегратор собрал логи PowerShell 7. Локальный immutable run: [runs/20261017-020001-919](../../content-local/native-probes/scope-primitive-2026-10-17/runs/20261017-020001-919/). Сборщику мешала ExecutionPolicy; процессный `pwsh -NoProfile -ExecutionPolicy Bypass -File` сработал у интегратора, глобальные политики не менялись. **Exit 0 копирования не принят за успех probe.**

Все четыре raw файла независимо совпали с записанными bytes/SHA256. `game.log:1` сообщает `Cygnus v4.5.2`; игровая дата START, reset/seed и ordinary hooks — **2200.1.1**. Wall-clock START 01:57:50, reset 01:58:06, actor 01:58:16, Earth seed 01:58:24, hooks 01:58:56. Копии game/error logs имеют время изменения 17.10.2026 01:58:56, сбор выполнен в 02:00:01–02. Числовой checksum запущенного мода и точный активный playset этими четырьмя логами не установлены; предупреждения о найденных descriptor других модов не доказывают их включение.

| Raw файл | Bytes | SHA256 |
|---|---:|---|
| game.log | 20039 | `A6C9B401353C5DFAEE7F619C5DD84F6B075E7A4C81FD4DF55A71F01FFF4F37A9` |
| error.log | 20322 | `E84E9416B1EC5F47AEFDE84549F90909DC7FB07A8704CE59F4BEF4FD1BB4C3BB` |
| setup.log | 1143673 | `B57367B9FBB3194B3F9A3068921A6BBD2D2B7CC737061A0928B9A2BDAEBBB424` |
| system.log | 11595 | `81235CDD545DD51BC8959FF06059E7A67DABC03BDED70D2BB9A8475D7AD2DA90` |

**PRIMITIVE — отдельные observations, не доказательство actual actor.** `game.log:6–18`: START BEGIN/END, оба `default_control=present` и `preftl_control=present`. В строках 10–12 последовательно записаны `type_default=true`, `type_primitive=false`, `native_is_primitive=false`; в 15–17 — `type_default=false`, `type_primitive=true`, `native_is_primitive=true`. Ошибок с `ssprobe_country` в error.log не найдено. Но BEGIN имеет пустое `This=`, именованных/ID-bound объектов нет, повторные BEGIN/END не записаны для каждого объекта, возможна дедупликация сообщений. `faction_state=NOT_EXPOSED_BY_PROBE`. Поэтому это ограниченный aggregate результат генерации, **не основание присвоить actual human actor `is_primitive=false`**, не универсальный default страны и не закрытие PRIMITIVE-FACT-001. Для зависимых операций остаётся Pending до именованного actor/control замера и независимой оценки свидетельств.

**PLANET ordinary — выполненные hooks, повреждённая identity matrix.** `game.log:19–22`: reset ALL4_OK → reset перед actor → actor seed United Nations of Earth → planet seed Earth/тот же owner. Строки 23–54 — 32 сообщения `ordinary_potential` BEGIN→END; строки 55–86 — 32 сообщения `ordinary_allow` BEGIN→END. В обоих выведены `exists_planet=true`, `planet_matches_planet=true`, `owner_matches_planet_owner=true`, `owner_matches_actor=true`. Это фактически записанные значения flawed probe, **не переносимый production факт о roots/self-link**: `error.log` содержит `Invalid macro entry in ssprobe_identity: rev.GetName` (01:56:50), 16 `Wrong scope for trigger`, 10 runtime `Invalid scope for event target 'overlord'`, указывающих colony scope для country-only перехода. THIS/ROOT/Prev заголовок и identity-матрица недоверенны; false по ошибочному overlord не доказывает отсутствие владельца. Есть также четыре missing-localisation и две missing-texture ошибки собственного диагностического пункта; ранее static grammar/review не заменили native validation. В захваченном game.log **нет финального reset после ordinary_allow END**. Whole PLANET-SCOPE-002 — INCOMPLETE/Pending, без closure из отдельных true-строк.

**Holding:** пользователь прямо сообщил «Проверить владения не удалось». В raw logs нет holding_potential/allow hooks. Статус **unavailable/Pending**, ordinary результат на holdings не переносится. Повторного запроса выполнить holding сейчас нет.

Ошибки diagnostic code относятся к нашему артефакту, не к выполнению пользователем инструкции. Установка, процессы, playset и gameplay при разборе логов не менялись. Ни одна из зависимых неизвестностей из текущих 42 этим разбором автоматически не закрывается.

## Изолированные исправления после первого запуска

Первый package/manifest/descriptor и raw run сохранены неизменными. SHA256 v1 manifest по-прежнему `E4582E626C187A5606EE0F21D5A05CD8358BA24F6CBF152527AD77D9C234776D`; все 10 записей mod/descriptor + own inputs перепроверены без изменения. Никакого исправления старых hooks задним числом не делалось.

- [Tooling-v2](../../content-local/native-probes/scope-primitive-2026-10-17/tooling-v2/README.md): отдельный generator/collector, UTF-8 **с BOM**, явный UTF-8 для чтения game.log и путь в исходный runs из подпапки. PowerShell 5.1 parser PASS и PowerShell 7 parser PASS, без исполнения на живых логах. Manifest SHA256 `FEEF3D10A8EFFE6BA4A5968FF5A0E27479023FADA6D0AACC405B573C43E1B42A`. Это не меняет ExecutionPolicy глобально и не валидирует успех native-опыта. Сборщик рассчитан на маркер v1, не на SSPROBE2.
- [Native-v2](../../content-local/native-probes/scope-primitive-v2-2026-10-17/README.md): отдельная папка, имя `SS Private Scope Primitive Probe v2`, namespace `ssprobe2`, log-prefix `SSPROBE2`. Новый descriptor зарегистрирован отдельно, SHA256 `8D7B585F4D7D396C2B1CA84CF8F9DE7E5E477905EB4E36BC0C5B669ED0D6BABF`; playset и старый descriptor не менялись. Два непараметризованных ordinary trigger измеряют только `planet` identity и owner-контроль, без LABEL/getter-макросов и THIS/ROOT/PREV/direct overlord. Effect country_event `.11` логирует actual saved actor с name/type/native result и отдельно существующий именованный pre-FTL target, не весь anonymous aggregate. Holding и автоматический стартовый перебор исключены. Собственные English/Russian BOM строки и ссылка на проверенную штатную research-lab иконку без копии bytes. Manifest SHA256 `DAAE0E34B10892525A8DC9E69DE20DBC401F867DD8AC34DE8B842A7CC6757D32`; собственный grammar/safety PASS, **runtime NOT_RUN**.

Независимый reviewer `condition_source_audit` принял **оба** ограниченных исправления для контролируемого ручного запуска, оставив native-v2 **NOT_RUN**. Замечаний к коду после affected review не осталось. Интегратор дополнительно проверил новый зарегистрированный descriptor: 208 bytes, SHA256 `8D7B585F4D7D396C2B1CA84CF8F9DE7E5E477905EB4E36BC0C5B669ED0D6BABF` совпадает с пакетом. Collector encoding-v2 принят только для V1; его маркер не подходит для SSPROBE2.

Следующий разрешённый ручной шаг — отдельный playset только `SS Private Scope Primitive Probe v2`, новая/подходящая обычная organic тестовая партия на паузе → без выбора планеты `event ssprobe2.12` → `event ssprobe2.11` actual actor/именованный контроль → выбрать свою планету, `event ssprobe2.10` → один раз открыть обычный каталог/навести на диагностический пункт → закрыть каталог/снять выбор → `event ssprobe2.12`, не снимая паузу. Старую matrix/holding повторять не надо. После сообщения пользователя «готово» **интегратор сам копирует и хеширует текущие raw game.log/error.log обычными файловыми tools**; повторно запускать PowerShell-сборщик от пользователя не требуется. Полнота и ошибки нового лога проверяются отдельно, не по успешности копирования. Новых native-v2 результатов пока нет; подготовка/принятый review не выдаются за выполненный опыт или closure gameplay-неизвестностей.

---

Ниже сохранён исторический отчёт первоначальной подготовки v1, до ручного запуска и полученных ошибок. Его прежние статусы не заменяют актуальный разбор выше.

## Фактический статус

**Подготовка выполнена и независимо принята для контролируемого ручного probe; native-опыты не выполнены.** Доступное Computer Use ограничено встроенным браузером iab; управление native Stellaris недоступно и не обходилось через shell/Win32/input. При проверке процессов `stellaris`/`dowser` не было. Игру, наш сервер и пользовательские процессы не запускали/не останавливали. Подготовлен минимальный ручной запуск в отдельном playset; отсутствие runtime-наблюдений оставляет обе зависимости Pending.

Источник версии: локальный `launcher-settings.json` сообщает `Cygnus v4.5.2 (9776)`. Это не снятый checksum запущенного мода. Source/API ориентиры: `common/scripted_triggers/00_scripted_triggers.txt:16–34` содержит штатный `print_scope_trigger` с `log`; там же существующие `if/limit/else` подтверждают используемый синтаксис. `common/buildings/00_example.txt:50–62` описывает potential/allow, `common/buildings/15_overlord_holdings.txt` — `owner_type=subject_holding`. Эти источники не доказывают исследуемые native roots/self-link или default `is_primitive`.

## Локальный артефакт и воспроизводимость

- Пакет: [content-local/native-probes/scope-primitive-2026-10-17](../../content-local/native-probes/scope-primitive-2026-10-17/README.md), целиком Git-ignored.
- Собственный генератор `prepare_probe.py`, отдельный мод `ss_scope_primitive_probe/`, descriptor, инструкция `README.md` и сборщик локальных логов `collect_probe.ps1`. Только собственные диагностические скрипты; оригинальные/derived Paradox assets не копировались и не коммитятся.
- [manifest.json](../../content-local/native-probes/scope-primitive-2026-10-17/manifest.json) содержит размер/SHA256 семи mod/descriptor-файлов, трёх собственных входных файлов и восьми read-only source-файлов. SHA256 манифеста: `E4582E626C187A5606EE0F21D5A05CD8358BA24F6CBF152527AD77D9C234776D`.
- Новый descriptor зарегистрирован без перезаписи: `D:/Documents/Paradox Interactive/Stellaris/mod/ss_scope_primitive_probe.mod`, SHA256 `77F800D20F64601ED7A91FA6ED437D97DDF474940BA0657ED6F2B578FD71293A`. Он указывает на приватную папку в проекте, не включает мод и не меняет `dlc_load.json`/playset. Существующие сторонние моды обнаружены; их не отключали, вместо этого инструкция требует отдельный диагностический playset.

## Точный сценарий и пределы метода

**PRIMITIVE-FACT-001:** собственный `on_game_start` запускает `ssprobe.1`, перечисляет actual страны с `type=default` и `type=primitive`, пишет для каждой native `is_primitive=yes` true/false и type/name. Отсутствие любой контрольной группы отмечается `MISSING`. Ни числовые defaults, ни результат из отсутствующих полей не подставляются. Faction-state не раскрывается этим probe и явно записывается `NOT_EXPOSED_BY_PROBE`; реальные IDs/faction-state потребуют доступного диагностического сохранения/штатного наблюдения. Стартовая генерация с модом не называется немодифицированным vanilla-проходом. Дату до первого продвижения времени нужно подтвердить в полученном native логе/UI.

**PLANET-SCOPE-002:** новые diagnostic ordinary building и subject holding вызывают logger в настоящих `potential` и `allow`. Allow заканчивается `always=no`, строить их нельзя; jobs/effects/платежей нет. Логируются THIS/ROOT/PREV, доступность `planet`/owner/overlord и native `is_same_value` относительно заранее сохранённых исходной планеты, её владельца/overlord и actual actor. Имена — подписи, не доказательство identity. Seed-события лишь сохраняют контрольные объекты, не подменяют execution potential/allow событиями в planet scope.

Для каждого каталога обязательна пауза: закрыть каталоги/снять выбор → `event ssprobe.12` (сброс ВСЕХ четырёх targets) → `event ssprobe.11` (actual actor; тоже сбрасывает старые ссылки) → выбрать исходную планету → `event ssprobe.10` → одно открытие соответствующего каталога и наведение → закрыть/снять выбор → немедленно `event ssprobe.12`, не снимая паузу. Без actor seed планеты отказывается; gates требуют actor и planet. Нельзя фильтровать по предполагаемому THIS до установления scope, поэтому повторные UI-логи ограничиваются этим коротким paused-интервалом.

Нужны отдельные BEGIN/END для `ordinary_potential`, `ordinary_allow`, `holding_potential`, `holding_allow`, со свежими успешными reset/seed и финальным `RESET|ALL4_OK`. Частичные или смешанные последовательности — INCOMPLETE. Для holding нужен уже существующий субъект с разными planet owner/holding actor и доступным holding slot; без него этот контроль unavailable, создавать субъект/договоры/ресурсы нельзя. Нельзя считать отсутствие вызова false.

## Проверки подготовки и review

- Генерация пакета и собственный базовый parser Clausewitz: PASS. Проверены токены, assignments/lists и сбалансированные блоки. **Это не native parser/API validation.**
- В собственных scripts проверено отсутствие `add_resource`, `add_modifier`, `add_technology`, `set_owner`, `add_pop`, `create_country`. Допустимые диагностические записи — только собственные event targets/reset и логи.
- PowerShell parser для `collect_probe.ps1`: PASS; сборщик не запускался на старых пользовательских логах и не выдаёт их за новый опыт.
- Независимый reviewer `condition_source_audit`, организованный интегратором `01a14667-53c7-7601-a7ba-3b258065dd1f`, принял исправленный diagnostic code **только для контролируемого ручного probe**. Первое замечание о запрете trigger-log снято после native counterevidence `print_scope_trigger`. Подтверждённые замечания об actor reset и UI flood исправлены полным reset ALL4, gates и bounded paused-инструкцией; affected review подтвердил исправления и manifest `E458…776D`. Native API/результаты по-прежнему Pending. Это review диагностического артефакта, не готовность gameplay или production-интеграция.

## Raw evidence и минимальный недостающий шаг

**Новых native raw logs нет.** Исходный путь после будущего ручного запуска: `D:/Documents/Paradox Interactive/Stellaris/logs/game.log`, вместе с `error.log` и доступными setup/system logs. Сборщик копирует их в ignored `content-local/native-probes/scope-primitive-2026-10-17/runs/<timestamp>/`, создаёт `probe-lines.txt` и `log-manifest.json`; без `SSPROBE|START|BEGIN` отказывается считать лог подходящим. Сборщик — только raw-copy: его exit 0 **не подтверждает** актуальную сессию, полноту или успешный probe. Даже наличие маркера не заменяет отдельного completeness review game.log + error.log: текущей даты, полного reset/seed/hooks-контекста, ошибок и контрольных объектов. Это принятое operational limitation; новый framework-валидатор в эту задачу не входит.

Недостаёт **одной ручной native-сессии**: в launcher выбрать отдельный playset только `SS Private Scope Primitive Probe`, создать обычную organic тестовую партию на паузе и выполнить короткую последовательность из локального README, затем собрать game/error logs. Для holding при отсутствии подходящего existing save честно вернуть unavailable. Это просьба выполнить недоступный UI-шаг, а не повторный запрос разрешения. Новых данных пока нет: ни PLANET roots/self-link, ни PRIMITIVE true/false не доказаны, неизвестные остаются Pending и блокируют только зависимые helpers.
