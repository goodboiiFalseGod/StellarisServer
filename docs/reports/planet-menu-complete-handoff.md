# Меню планеты — точная передача

17.10.2026. Owner `01a14667-53c7-7601-a7ba-3b258065dd1f`.
Копия `planet-menu-catalog-continuation/StellarisServer`, ветка
`feature/construction-condition-evaluation`; baseline `46e30a4`.
База63ad6f6/recovery4947f45. Основной checkout/чужой dirty, серверы и порты
сохранены. Push/main/stable/пользовательские процессы вне scope.

## Цель и текущий цикл

Закрывать условия всех зданий/зон по original4.5.2; native Unknown не угадывать,
профессии отдельно. Текущее прямое поручение: через manual owner выполнить
PLANET-SCOPE-002/PRIMITIVE-FACT-001 разрешённым приватным модом вне установки,
затем подключать подтверждённые факты. Проверки контекста/переходов уже исправлены.
Root — единственный интегратор/production/docs; actual_context_checks — только
тесты (Luna low), condition_source_audit — read-only source/review.
Без nested delegation и повышения моделей/effort. Любой code diff требует
независимого review: production root → audit; tests Luna → root.
Usage неизвестен.

Реестр42 ID/зависимостей/source/owners, baseline и команда воспроизведения —
[PLANET-CATALOG-COVERAGE](../PLANET-CATALOG-COVERAGE.md). Новый критерий переходов —
верх [runtime](../PLANET-CONSTRUCTION-RUNTIME.md). Не читать всю историю.

## Проверенный baseline46e30a4

Actual source seed123,3presets, HUM owner/founder/population/tech/positive CG,
foreign registry и завершённые районы. Здесь system graph отсутствует.
Catalog648 (498 buildings/146 zones/4 districts), economy194, jobs59.
Каждый preset eligibility504F/42U/102T. Potential economic/scientific396F/55U/197T,
military395F/55U/198T. Farming4/mining2/generator3; два здания farming/generator
Unknown→True, mining Unknown. District72/full368/system32/Release0/0 PASS,
known-loss0, DTO/quote/hash guards сохранены. Независимые reviews accepted.
Логи `artifacts/catalog-district-presence-source.log`, `catalog-district-*-regression.log`.
Positive lifecycle/HTTP/browser на этом срезе не проверялись.

TRANSITION-ACCOUNTING-001 завершён: source85/Release0/0 PASS; exact8P/2E transitions,
42-ID состав и полные карты648×3 сверены, negative accounting7. Root test review/
independent docs audit accepted. Production не менялся; baseline42 сохранён.
Логи artifacts/catalog-transition-accounting-{build,source}.log. Не сравнивать с partial90U.

## Owners и факты

Reference `01a12f66-8bb2-7ee3-aced-76f5d06d2338`;
manual `01a12d73-b37b-7080-943c-684cc080b1f6`.
PRIMITIVE-FACT-001/custom_tooltip.text Pending; primitive probe в отложенной очереди.
NOT-MULTI/SPECIES-SCOPE/SYSTEM-CONSTRUCTION Pending; повторять опрос неизменённого
ответа не нужно. Последние turn IDs — в реестре42.
PLANET-SCOPE-002: source example документирует planet scope potential обычного
building, но planet=self-link, allow scope и holding roots не установлены;
17.10 человек разрешил отдельный приватный мод вне установки для этого probe и
PRIMITIVE-FACT-001. Manual owner получил поручение выполнить оба; результатов ещё нет.
Новый source факт сам по себе не снимает Unknown.
Private package prepared/independent review accepted после reset/gating fixes;
manifest E458…776D, полный hash/проверки в верхнем runtime. Descriptor зарегистрирован
отдельно без смены playset. Native UI недоступен; нужен user run по README в
manual-owner ignored content-local/native-probes/scope-primitive-2026-10-17/.
Run v1 `20261017-020001-919` собран root через pwsh7; native4.5.2/2200.1.1.
Observed primitive false/true и ordinary planet/owner positives, но macro/scope
errors/нет finalreset: wholeprobe INCOMPLETE, actor primitive не доказан. Holding
human unavailable, hooks0. Hash/подробности верх runtime, baseline42 неизменён.
Отдельный corrected v2 prepared, сохраняет v1/manifest/raw; namespace ssprobe2,
nonparameterized ordinary planet/owner hooks и actor-bound primitive effect.
Independent code review accepted для controlled user run; holding не повторять.
Сценарий .12→.11 (без планеты)→.10 (своя колония)→catalog→.12; только v2 playset,
пауза. После «готово» root собирает raw logs, старый collectorv1 не использовать.
V2 manifest DAAE…7D32, полные hashes/критерии верх runtime; runtime NOT_RUN,42 неизменно.
MINING/DESIGNATION повторно точечно проверены: native owner_or_space_owner и явное
назначение designation отсутствуют в подтверждённых данных; сохранять Unknown.
Следующий шаг после текущего коммита: только новое evidence для групп реестра42,
связать rule/fact/expected truth до изменения evaluator. Не подменять species founder,
DLC наличием файлов, missing variable нулём, partial graph полным.

## Сохранённые требования и границы

План `e0aa889:docs/PLANET-MENU-INTEGRATION-PLAN.md`; все §13/V01–R02 остаются.
Точная прежняя таблица статусов/проверок и pending сценариев:
`46e30a4:docs/reports/planet-menu-complete-handoff.md`.
Reference HTML `6b5bb59:docs/references/planet-menu-452.html`,
blob `ccf6829788face6c4c297193e0ba8305e55e1696` неизменён.
DOM/assets/UI parity/zoom/drag/resize/full lifecycle pending; WF не native jobs.
Arkship внутренняя компоновка — единственное human deferral; полный RC не принят.
Protected snapshots/server5257–5263: `54318e1:этот файл`;
`artifacts/check-protected-snapshots.mjs`. Не перезапускать.
История условий/source/inline/HUM/экономики: `46e30a4:этот файл` и topic contracts.
BRANCH-FACTS3b6a609:75U→44U; SYSTEM-GRAPH9efe2fb: partial graph, class/star,
без applied flags/starbase/полноты эффектов. Browser/fixture/grants не разрешены.
