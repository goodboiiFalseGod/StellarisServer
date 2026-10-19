# Независимое ревью structural initializer

Report ID: `GALAXY-INITIALIZER-STRUCTURE-001-independent-review-attempt1`.
Reviewer: `/root/galaxy_review`, независимый от автора `/root/galaxy_catalog` агент.
Дата: 13 октября 2026. Assigned branch `feature/galaxy-generation`.

Статус после attempt2 re-review: **accepted для bounded structural executor**.
Единственный P2 исправлен и повторно проверен; открытых actionable findings нет.
Прочитаны actual executor/numeric/check files, contract/handoff, frozen descriptor
и initializer evidence. Параллельный world API не входит в scope review.

## Исправленный P2 — обе границы integer range должны проверяться полностью

`src/Stellaris.Core/Galaxy/GalaxyInitializerExecution.cs:194`: integer Sample проверяет
только Minimum<int.MinValue и Maximum>int.MaxValue. Поддержка descending ranges требует
также Minimum>int.MaxValue и Maximum<int.MinValue. Например constant size2147483648
проходит этот guard, затем падает сырым OverflowException при `(int)Sample`, нарушая
declared typed integer validation. Более экстремальные descending endpoints способны
переполнить conversion/delta/Abs до source-context error.

Исправление: проверить каждый endpoint на полный int32 interval до delta; добавить
controlled invalid ascending/descending endpoint regressions с ожидаемым
GalaxyStructureException Code=integer-range. Не требуется расширять structural scope.

## Reviewed первоначальный snapshot SHA-256

| Файл | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/GalaxyInitializerExecution.cs | EE7F52DAEA1A7DF8683B4233D0DB4D497BFDDB54E55A1B6E0002B2F832FF85F7 |
| src/Stellaris.Core/Content/Galaxy/GalaxyNumericValues.cs | F7A247D35301FD3C20F429BD7168D74B91DE154B7C9E9413115F84ACD1966A38 |
| tests/Stellaris.GalaxyGenerationChecks/GalaxyInitializerChecks.cs | 0456FAF579B78CBAEFFB3664EBF143F7FA804D7C1C193089FB09C900331FBF1F |
| docs/reports/galaxy-generation-initializer-contract.md | D0F69B48C2F607CE7FC964DCE883FCE4A82441417874D8742EEC9DEEBA191457 |
| docs/reports/galaxy-generation-initializer-handoff.md | A36ADCCE034042FC38856141C6C6534680ABB8FFA23CFE6246F943A76128988E |
| docs/reports/galaxy-generation-descriptor-source-009cc4f18624.md | 009CC4F18624AB3C7194E5EB84AD96C8C38A691B67E02DC26090B8E005087126 |
| docs/reports/galaxy-generation-initializer-source-123da01dc034.md | 123DA01DC03434837D52604924DD693CAEE9B1BCF901347F7D3E6D240D1652E9 |

## Прочие проверенные свойства и ограничения

Numeric AST evaluation bounded, local/global lookup и cycle detection явные.
Compiler сохраняет source AST/unknown-field errors, unified child order, root descriptor
star ordinals, home aliases и inline parameter/conditional expansion. Executor сохраняет
count/class/distance/size/angle ledger, none advances orbit без body, recursive parent IDs,
missing class=random и selected-class size fallback. Star factor и habitable multiplier
применяются отдельными fixed truncations. Result immutable, GameplayComplete=false;
arbitrary content/effects/neighbors/naming остаются deferred/declared policies.

Old initializer evidence содержит исправленные поздним descriptor/contract сведения;
противоречащая старая prose (missing class, home aliases/child order, raw distance1.0)
не принимается за действующий контракт. Star randomizer,
belts RNG interleaving и supplied class predicates явно ограничены policy.

Author/root-run evidence73PASS/Core0warnings/errors reviewer повторно не запускал;
проверены actual checks, включая all360 compile и ordinary/home/template execution.
Машинный reverse/game/UI/full runtime не проверялись. Parsed/structural completion
не доказывает executable vanilla content или PLAYABLE_WORLD.
Единственная запись reviewer — этот Markdown; implementation/tests не изменялись.

## Attempt2: narrow repair re-review

Actual endpoints теперь оба проверяются на полный int32 interval до conversion/delta/Abs.
Шесть count endpoint combinations и два oversized size forms требуют typed
integer-range error. Negative/zero count consumes только свой count draw и пропускает
creation; две таких descriptors дают0bodies/2draws. Это соответствует новому frozen
range evidence, включая signed inclusive descending sampling и last-assignment/default0.
P2 закрыт. Остальные previously reviewed source paths не пересматривались широко.

| Final reviewed файл | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/GalaxyInitializerExecution.cs | 9204A18715D7FF03EFE0D0EF8611E68FF98D4693B8FEB585839AD5605BF77F65 |
| tests/Stellaris.GalaxyGenerationChecks/GalaxyInitializerChecks.cs | 3D2D8CC38245E8FB14B2999BE347585C05DB0D78D35FF6D7403A961650CD23F3 |
| docs/reports/galaxy-generation-range-source-0d65138c4df3.md | 0D65138C4DF3AD747F903644D90297A91E77043A3B755BD80F7186B34B74C5E0 |

Actual final hashes проверены reviewer read-only; range evidence прочитан полностью.
Author-run repair validation: build0warnings/errors, focused5PASS. Reviewer команды
повторно не запускал. Предыдущие73PASS относятся к attempt1 combined run; final combined
regression после ремонта не приписывается reviewer. Bounded structural acceptance не
утверждает playable/full-content readiness, остальные ограничения выше сохраняются.
