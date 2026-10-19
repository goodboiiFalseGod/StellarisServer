# Проверка численных эффектов технологий RC

14 октября 2026. База `451fe8b6ebddaa806bc458d39ff90bd1e82dced6`, ветка `feature/technology-source-effects`, отдельная managed copy `technology-source-effects`. Контракт до кода: `c5db734`, уточнение `2d78efe`; [требования](../TECHNOLOGY-EFFECTS-RC.md). Канонический a58 research runtime сохранён. Shared RC, stable, UI/renderer/assets и установка игры этой веткой не изменялись; новых постоянных серверов/автоматизаций нет.

## Результат и происхождение

- Импортированы source economic category parents, static Workforce composites, identity modifiers и нормализованные technology fields/swaps. Каталог 679 определений, manifest 761 файлов, включая прежние метаданные иконок.
- Работают source research all/area speed, numerical job output/upkeep, Workforce composites и строительные speed/cost consumers. Приоритетные реальные определения: `00_phys_tech.txt:642` Admin AI (+.05 all speed, +.05 researcher upkeep), `00_eng_tech.txt:2347` powered exoskeletons (+.05 worker/simple drone bonus WF), `00_eng_tech.txt:3627` assembly pattern (+.25 building speed), `00_soc_tech.txt:183` eco simulation (+.05 farmer food output). Identity research speed — actual ethics/civics/origin, без копирования других preset effects.
- Повторяемые эффекты используют число завершённых уровней. Effective swaps согласованы между расчётом и карточками. Неизвестные swap/выражения и эффекты без реализованного потребителя остаются адресно deferred; country-wide resource effects шире jobs не подменены job-only consumer.
- Старый overlay определяет текущий месяц, новый — будущее preview/работу. Daily/command structural refresh начинает с чистых trait значений, installed graph effects сохраняются отдельно. PreviewPlanetReport — pure Calculate без публикации отчёта, смены месяца и балансов. Ошибка второй империи откатывает весь кандидат месяца.
- Idle reserve исправлен на RAW `S+F`; active multiplier `1-.25+all+areaOnly` один раз, source no-leader baseline сохранён. Переключение, потеря перепроизводства, repeatable costs, strict wire, POV/replay и explicit enqueueUpgrade не заменялись.
- LOC gate: удалён серверный translation loader. В bootstrap проверяются только native keys фиксированного профиля 232×2, общий список подключён embedded resource без копии whitelist. Значения переводов не сохраняются/не передаются; RINFIX/дополнительные файлы исключены. DTO — optional nameKey/descriptionKey/categoryKey, effect localizationKey/amount/format/targetDefinitionId/sourceKey, unavailable nameKey; legacy display поля содержат IDs/наши подписи. Format `number`/`percent`, amount — source fraction (например .05); браузер форматирует и разрешает original texts.

## Выполненные проверки

| Проверка | Результат |
|---|---|
| `dotnet build src/Stellaris.Server/Stellaris.Server.csproj -c Release` | PASS, 0 warnings/errors |
| `dotnet run --project tests/Stellaris.ResearchChecks -c Release -- <game-root>` | PASS: source import, per-area/RAW idle, identity/additive speed, upkeep/WF/output, construction, swaps/unknowns/effective metadata, repeatable levels |
| `dotnet run --project tests/Stellaris.M3SessionChecks -c Release` | 16 наборов PASS, включая fixed profile/RINFIX/missing-file keys test |
| `dotnet run --project tests/Stellaris.M3SessionChecks -c Release -- --game-root <game-root> --effects-only` | PASS: old/current/new month, upkeep, WF+farmer, повторные refresh, pure preview, второй owner atomic overflow, pending building old day60/new day61 speed и fixed paid price; smoke всех трёх пресетов |
| `dotnet run --project tests/Stellaris.M3SessionChecks -c Release --no-build -- --game-root <game-root> --long-automatic-only` | 16 PASS + 6000 automatic days → paid build/upgrade PASS |
| `dotnet run --project tests/Stellaris.PlanetEconomyChecks -c Release -- --game-root <game-root>` | 8 PASS, source coverage 165 buildings/29 zones, job closure 29 covered/30 gated/0 missing |
| `dotnet run --project tests/Stellaris.M3ServerChecks -c Release -- --game-root <game-root>` | 535 PASS, реальный временный HTTP/WebSocket listener, новые LOC key/value assertions, clean shutdown |
| `git diff --check` | PASS |

Начальная новая копия потребовала обычного `pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts`; lockfile не менялся. Первый synthetic key test выявил вложенные каталоги и upper-case штатных имён; regex профиля и fixture исправлены, повторный тест PASS. Первый transport прогон дошёл до устаревшего expected multiplier .75; economic preset теперь правильно .80 (=1-.25+.05 materialist). Исправлен только assertion, повтор 535 PASS. Неверная первая CLI форма PlanetEconomyChecks исправлена на `--game-root`; PASS относится к корректному запуску.

## Ревью и оставшиеся gates

Авторы Core — `/root/effects_core`, Session — `/root/effects_session`, LOC/schema/bootstrap — `/root`; независимый actual-diff/contract reviewer `/root/effects_review`: APPROVED для bounded single-colony slice. Ведущий также прочитал actual diff исполнителей. Настройки lead/reviewer/численных исполнителей наследованы без повышения; фактическая маршрутизация backend и расход токенов недоступны, экономия не заявляется. После ревью исправлена только устаревшая transport assertion/документальная формулировка; адресная повторная проверка выполнена.

- Текущая authoritative economy model имеет одну planet на owner. Historical spatial fixture IDs 1/4 не второй economy binding; multiple-colony input явно блокируется. Несколько независимых owners поддержаны и проверены. Новая colonization/binding система в этот этап не добавлена.
- Общая RC уже содержит UI consumer `52e003e` и funding test fix `b342c78`; они отсутствуют в UI базы 451. Поэтому полный source ResearchIntegration здесь упирается в старый UI без enqueueUpgrade. Funding ordering учтён в тестовом diff; полный исследование → paid upgrade consumer тест обязателен после объединения с RC. `--effects-only` — отдельный адресный набор, не PASS отсутствующего UI.
- UI key consumer принадлежит другому владельцу и должен принять указанные DTO fields. Новых браузерных/ручных/GPU проверок не выполнялось. Ручную приёмку пользователь проводит после общей сборки, stable пока не изменять.
- Original technology DDS bytes не менялись/не добавлялись в Git; прежняя a58 проверка 674/674 относится к прежним metadata. Этот этап не заявляет новую asset/GPU приёмку.

Ignored local evidence: `artifacts/technology-effects-serverchecks.log`, `artifacts/technology-effects-serverchecks-retry.log`, `artifacts/technology-effects-long-session.log`.
