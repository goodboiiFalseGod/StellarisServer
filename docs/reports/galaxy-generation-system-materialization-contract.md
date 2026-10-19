# GALAXY-SYSTEM-MATERIALIZATION-001 attempt1

13 октября 2026. Прямое поручение пользователя: создать объекты систем по native
контракту, согласовать детали с research-партнёром и провести независимое review.
Pre-code contract; ответы constructor/registration/failure согласованы с партнёром.
Scope: отдельный Core/Galaxy module + own checks/reports. Прототип, Session/server/client,
shared ECS/protocols, пакеты, установка и stable checkout не изменяются. Commits/push нет.

## Поддержанный результат

Source-backed ordinary-system materialization при coordinate acceptance:
unsigned native handle, raw/stored(clamped) position, arm index и generation order;
изолированный registry объектов. RNG расходует только coordinate pipeline,
allocation/registration не должны добавлять draws. Reject не выделяет handle.
Неизвестные constructor/content/spatial поля не выдаются за zero/empty native defaults.
Новая API не подключается к планетной сборке и не снимает Triangle/full-world gates.

Начальное manager state обязательно задаётся явно. Первый bounded режим — fresh,
empty manager с корректной free chain и initialized generationFF; factory создаёт ascending chain.
Capacity/storage
должны быть достаточны и готовы. Nonfresh state/free/reuse и grow/failure paths
не имитируются по planet-manager аналогии; неподтверждённые ветви возвращают typed
unsupported/partial result, без fallback fake-system и без скрытого ID consumption.

## Уже сохранённый evidence

Frozen local analytical snapshot
[source-0c16f201abb1](galaxy-generation-relaxation-adapter-source-0c16f201abb1.md),
SHA256 `0C16F201ABB132D10B29E4744F5FE7736D9351B7801D0F84A65AF5BC202BC38D`, §16.4:
system allocator0x140267FF0, manager world+0x930+0x1078; object0x13B0,
handle+0x08; chunk size1024 objects, address index chunks[i>>10]+(i&0x3FF)×0x13B0.
Chunk ensure0x14030B850 initializes handleFFFFFFFF, constructor0x1408606B0;
new generation=(old high byte+1) modulo256; fresh FF→00, first handle=slot index.
Native field type unsigned: high bit не означает invalid, только full sentinelFFFFFFFF.
Ранее принятые coordinates/retry/clamp operators и adapters не переписываются без причины.

## Уточнения до реализации полной ветви

Ответы зафиксированы до основного materialization кода в immutable analytical snapshot
[source-244eb6f2135b](galaxy-generation-system-materialization-source-244eb6f2135b.md), SHA
`244EB6F2135B698C7373896572D5E146731BBF47B29D158AA46FB6A6C1683C4E`, §16.4–16.4.1.
Предыдущий received checkpoint091265DE… сохранён отдельно, не перезаписан.
Последний уточнённый free-link operator сохранён отдельно в
[source-7c4c181256fd](galaxy-generation-system-materialization-source-7c4c181256fd.md), SHA
`7C4C181256FD94B50866BB84F985F2DDD258BE846313DBC7A424A7DB6ED359FE`.
Head.prev/tail.next/freeHead sentinel00FFFFFF; pop меняет nexthead.prev и oldhead.next
на00FFFFFF при non-singleton. Existing snapshots091265/244EB6 не заменены.
Это копии исследовательского Markdown, без game binary bytes/assets/raw disassembly;
внешний outputs-файл не менялся. Соседние исследования в snapshot не исполняются этим срезом.

Capacity — count/exclusive bound1..00FFFFFE, max allocated slot00FFFFFD;
free-chain sentinel00FFFFFF. highestActive uint32, emptyFFFFFFFF: обновить если oldFFFFFFFF
либо slot>old unsigned. Нельзя signed max(-1,slot). GenerationFF→00 для fresh storage.
Ready chunks, stop34/35, capacity/freeHead/links/object words, active/highest fields,
resolver binding и initial world dirty/last-handle предоставляются явно.
Initial manager dirty также supplied; unknown native pointer/cache structures не моделируются.
Fresh support требует active0/highestFFFFFFFF/no registered slots; nonfresh/reuse — unsupported.
Чистые handle/grow arithmetic operators могут иметь vectors reuse, но free/grow не исполняются.

Success order: slot registration/count/dirty/highest → ctor defaults → computed fullhandle
→ re-resolve exact same object/fullhandle → copy explicit opaque name/context + worlddirty/last
→ raw position/sentinel → clamp → unconditional position helper no-op(randomized0)
→ optional arm assignment → generation-vector append.
Known ctor subset: position/auxiliary0, sentinelFFFFFFFF, arm−1, randomized0;
неизвестные многочисленные fields отсутствуют в модели, не fakezero.
Name/context — supplied opaque world-layer payload, не native-generated names.
Own registry — handle→те же managed object references, не native spatial cache.
Final native spatial/index refresh остаётся explicit pending.

Safe preflight проверяет весь initial snapshot, нужные free slots/chunks и same resolver
**до** candidate draws/IDs. Stop/grow/storage/unsafe binding возвращают typed unsupported;
это API-domain отказ, не симуляция native sentinel continuation. Корруптные chain/records —
typed invalid snapshot. Native postallocation resolver failure уже расходует ID: если
invariant нарушится после регистрации, abort с обновлённым snapshot и consumed-ID marker,
без sentinel mutation/append. Нельзя назвать его preallocation no-ID failure.
Нормальный owned-manager resolver invariant обеспечивается самой локальной таблицей.
Для insufficient capacity grown-state/OOM и nonfresh state не угадываются.

`Generate`/`Replay` materialize inline **при acceptance**, а не после всего journal;
reject сохраняет acceptedIndex и не выдаёт ID. Existing journal/collision/adapters не изменяются.

## Acceptance

Independent native vectors fresh slots0/1/1023/1024; generation wrap/unsigned validity;
reject-no-ID, acceptance order/raw→clamp/arm, object lookup/generation order, explicit
capacity/flags/storage/invalid-state gates без silent fallback. Deterministic local state
и сопоставление coordinates с принятым journal. Все33 предыдущие groups + new checks,
CoreRelease0/0. Затем code/hash freeze и review существующим `/root/galaxy_review`,
отдельный reviewer-authored artifact; author не принимает собственный diff.
