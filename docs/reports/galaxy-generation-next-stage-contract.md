# GALAXY-NEXT-CONTRACT-001

13 октября 2026. **Docs-only, после отчёта frozen.** Принятый код, check project,
контракт и HEAD `5a67a51a2e28609cfdcf861a1b25fc0bd840b573` не менялись; commits нет.
Входная база: воспроизводимый tiny/elliptical seed1 journal200 pre-relaxation coordinates,
28 author-run checks PASS, Core0 warnings/errors. Это ещё не native world/ID graph.
Архив координатора/index `e3be626` принят как сообщение о сохранении; сам архив здесь не менялся.

## Следующий исполнимый этап

**Первый выбор — GALAXY-RELAXATION-ADAPTER-001**, изолированные adapters input/output
вокруг существующего supplied-tessellation pass, без установки/подмены Triangle:

| Шаг | Вход → выход | Зависимости и приёмка |
|---|---|---|
| Подготовить relaxation input | accepted stored coordinates в insertion order + resolved coreRadius + defines → ordered binary32 ordinary sites, затем dummy sites | scale×100; dummy step/trig/sequential FM(coreRadius,0.9,100) строго по §16.5; source hash/defines обязательны; независимый dummy/site conversion vector до кода |
| Принять exact supplied tessellation | Triangle primal/dual output → уже реализованный simultaneous pass | bbox inclusive ±50000; switches `pczAevnQ` first, `pczAevn` rebuild; backend отсутствие остаётся typed unsupported, не generic fallback |
| Конвертировать ordinary output | finite binary32 sites в bbox → fixed int64 positions в прежнем порядке | `t=cvttss2si(float)` → `t×100000` → FixedDiv(...,10000000), ровно `t×1000`; dummy sites не materialize; RNG state unchanged |

Source-backed defines проверены read-only: `common/defines/00_defines.txt:780–781`,
RELAX_ITERATIONS=1, CENTER_DUMMY_POINTS=256;
SHA256 `82EEBD807EB54AF347025B061AF2991FC40A1B1059E39C2CB8EE135DCA02B7CC`.
Текущий addendum §16.5 прочитан единым snapshot,
SHA256 `3B0CFA3FD85F1DED03A18A8728B6E484053D45E4622D3A1C00E5F2902D2F62D5`.
Партнёр исправил прежнее неверное слово «post-relax clamp»: `0x140308ED0` — division,
дополнительного clamp нет. Vectors: ±1.999→±1000, ±0.999→0, ±50000→±50000000.
NaN/out-of-bbox/out-of-int32 supplied output отвергать явно; indefinite cvtt не обещать.
`0x140254880` spatial rebuild/write-system effects остаются вне adapters/planet runtime.

**Второй шаг — fresh-system materialization**, после durable system-specific allocator
контракта: handle фиксируется при acceptance, rejected candidates не allocate.
Начальное manager/free-list/generation состояние — обязательный вход;
«fresh sequential slots» допустимы только при явно пустом fresh manager.
Не переносить planet/deposit/district manager semantics на systems без evidence.
Партнёр сообщил system object0x13B0, handle+0x08, manager world+0x930+0x1078,
chunk ensure0x14030B850 с initial FFFFFFFF, allocator0x140267FF0/ctor0x1408606B0:
fresh FF→00, first handle=slot. После узкого запроса partner сохранил specifics и vector
fresh-head0→handles00000000,00000001,… в **§16.4**; проверен единый bytesnapshot SHA
`5F87F13480B2B2C48895B3AB5711DBD2F84736A7AC2F620DC670751DA12A84F0`.
System-specific free/reuse/destructor side effects отдельно не audited, сюда не входят.

## Что остаётся gated

- **Exact Triangle backend:** 1.6/TRILIBRARY/SINGLE/NO_TIMER, default divconq/Dwyer,
  robust predicates, private RNG reset1, memory-pool/writeedges direction/order.
  Official-source build является recovered source-level dependency, а не доказательством
  universal bit identity: FP/MXCSR/compiler, pointer ordering, degeneracies и rebuild
  allocation history требуют behavior checks. Установка packages сейчас запрещена.
- **Unresolved evidence/data:** dummy/input conversion boundary vectors; конкретный
  initial system pool, flags/capacity/free chain. System-specific allocator paragraph
  и fresh-head vector теперь durable, но constructor/register/spatial side effects и
  failure/OOM path этой bounded phase отдельно не приняты.
  Full system/spatial registration side effects и exact backend FP/allocation behavior
  не закрываются существующим raw journal или supplied pass.
- Acceptance backend: §19.4 triangle/square/reverse-square/duplicate edge order,
  annulus equality/interior/length vectors, centroid distinguishing bits и stitch/orientation.
  Прохождение этих случаев подтверждает покрытие случаев, не всю native seed-equivalence.
- M36/startup, planet/deposit/district allocators, pruning/global RNG и stable integration
  не входят в следующий этап. Полная galaxy не gate планетного прототипа.

## Проверяемое независимое ревью текущего delta

Reviewer **`/root/galaxy_review`**, существующий отдельный агент, не автор реализации.
Reviewer-authored report ID **`GALAXY-GENERATION-001-independent-review`**:
[galaxy-generation-independent-review.md](galaxy-generation-independent-review.md),
SHA256 **`0B361E74B2EC6D0C4ACD1C6C598C8B32581F5701E0F8988EE330D9B27C44A8E8`**.
В отчёте 15 hashes фактически прочитанных source/check/contract files; actual delta —
полное содержимое новых untracked файлов плюс последовательные изменения, не пустой git diff.
Текущий placement delta проверен против §16.4 snapshot
`0DE1D764915C60F51F40B9A4DFBD52724353E28C585E1D7F30994D20CD1BAD89`:
ordinary/scaled collision, raw/stored clamp, retry10000/10001, state after failed draws,
immutable journal и отсутствие world/planet coupling. Earlier evidence hashes — в его отчёте.
Outcome accepted/no actionable findings; tests/native reverse reviewer не повторял,
full Triangle/native IDs/runtime/full seed-equivalence не принимал. Это его actual-code
review artifact, не авторская фраза APPROVE. Новых агентов в этом задании не создано.
