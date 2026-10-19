# Независимое ревью world API

Report ID: `GALAXY-WORLD-API-001-independent-review-attempt1`.
Reviewer: `/root/galaxy_review`, независимый от автора/root агент.
Дата: 14 октября 2026. Ветка `feature/galaxy-generation`, baseline `090d1bc`.

Статус после attempt2: **accepted для ReadyForBootstrap producer/consumer seam**.
Оба P2 исправлены и повторно проверены; открытых actionable findings нет.
Прочитаны actual7code/project/check files, world/integration contracts, handoff,
prototype mapping и конец author validation log. Actual Program diff — один wiring
call против baseline, без удаления прежних checks. Предыдущие source-backed stages
не аудировались повторно широко.

## Исправленные findings первоначального snapshot

1. **P2: blank seed не соответствует contract.** GalaxyLobbyCatalog.Resolve использует
   `request.Seed ?? Guid`, поэтому пустая/whitespace строка отклоняется SeedBundle,
   хотя world contract обещает одноразовый resolve пустого seed. Проверять
   IsNullOrWhiteSpace перед host generation; regression должен replay resolved seed.
2. **P2: consumer body/system pair не проверяется.** GalaxyBootstrapValidation проверяет
   Entity.BodyId и Entity.SystemId независимо на существование. Дополнительная
   noncapital entity может указывать body из другой существующей системы и пройти
   validator. Требуется body→system correspondence при наличии обоих полей и negative
   fixture с individually valid, совместно inconsistent IDs. Capital exact-home
   checks уже присутствуют; исправление относится ко всем consumer entity references.

## Reviewed первоначальный SHA-256

| Файл относительно checkout | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/GalaxyLobby.cs | 9087F1B5FE02276195005854691894F762EF8DAC9083367DD972119FAE8193DC |
| src/Stellaris.Core/Galaxy/GalaxyWorldGeneration.cs | 948929E187DE31BA6C52FE1D5C4F41F8547A0C9861021F7408AAE0C730080402 |
| src/Stellaris.Core/Galaxy/GalaxyBootstrapValidation.cs | ADAD7E08CACE2D4E9FCA2544F4AF3E6D945CCECD0E83651412EF90A8D3089872 |
| tests/Stellaris.GalaxyGenerationChecks/GalaxyWorldChecks.cs | C8FD534D179EEEF930390F5E6391B650781F17B63FB75FDAB306AB65564B142F |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | 437E2600A48E1F77210BC181532B5FAD1218B23F71C3ED45BC891435A3C8640B |
| tools/Stellaris.GalaxyHarness/Program.cs | 778FA49A93E78ED3E0EB6E5AED76DB98D0BD3B7DA7B2B254105023BAF08C025C |
| tools/Stellaris.GalaxyHarness/Stellaris.GalaxyHarness.csproj | D50E2FD8B47D1D454FEDFA331458F073982163120502D167CEBB8EE64321FF0C |
| docs/reports/galaxy-generation-world-api-contract.md | F50C3D95BF0A300B743D95FDF786638F2003CC422CBBB335583AC62802A68DDB |
| docs/reports/galaxy-generation-integration-readiness-contract.md | CED95651F1C2DBEB875E218317CFF3EAD3CCA18F81F7789F95B6CF74E55A5E14 |
| docs/reports/galaxy-generation-world-api-handoff.md | D9EEE13EA97ECB4D10B50ABCAF5FD9AD6C5E345BDC9EC2A8994E34C64B3A6708 |
| docs/reports/galaxy-generation-prototype-mapping.md | AB2CC001237CB564373E4B1E0E0E7A060A0D13A1C4CEBC2F3F5D7AA1A933303B |

## Прочие свойства и ограничения

Producer API разделяет Resolve/Generate/bootstrap validation, сохраняет immutable
settings/seed/content/world fingerprints и атомарно публикует world только после
успеха всех стадий. Profiles/pools, spatial-BFS/maximin homes, connectivity repairs,
local-orbit projection и deferred omissions раскрыты; strict VanillaComplete и
special nonneutral content отклоняются. Реальные generated home class/size/parent
сохраняются, включая nested moon. Core producer не импортирует planet startup;
existing importer вызывается только integration checks по exact generated carrier.

Consumer contract проверяет actual host projection IDs/definitions, ownership,
slot bijection, home class/initialsize, positive economy population, anchors, actors,
fingerprints и exhaustive disposition requests. Synthetic accepted fixture — только
protocol test. Producer Readiness остаётся ReadyForBootstrap; real gameplay launch
и staged consumer runtime transaction не выполнены. CLI экспортирует metadata,
не оригинальные AST/script bodies/assets.

Author-run Core/CLI build0warnings/errors, combined78PASS: tiny shapes/seeds/densities,
one/two slots, nested home, huge1000, source StartupBuildingImporter anchors и consumer
failure fixtures. Reviewer runtime tests/build не повторял. Исторические source
evidence используются через ранее принятые stages, native/game reverse не повторялся.
Full effects/startup/content, special families и настоящая Session/M3 integration
вне приёмки. Единственная запись reviewer — данный Markdown, code/tests не менялись.

## Attempt2 narrow re-review

Actual Resolve использует IsNullOrWhiteSpace для null/empty/whitespace seed, возвращает
host-generated value в resolved request. Две blank cases проверяют непустой resolved
seed и совпадение settings fingerprint при replay. Body→system dictionary проверяет
соответствие любых supplied entity pairs; extra-colony regression использует отдельно
существующие foreign body и home system и требует точный body-system-mismatch error.
Оба замечания закрыты. Остальные previously reviewed четыре code/project paths unchanged.

| Final reviewed файл | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/GalaxyLobby.cs | 394E6C16B96F03878E01A6004F3E3969F5FDCAE270297E36B0FF783CC115CB47 |
| src/Stellaris.Core/Galaxy/GalaxyBootstrapValidation.cs | 89CADDAE7669FB4CB8C3A1E4C0EB73F09E788005D7A3156CBFD8B9D26B5B66C6 |
| tests/Stellaris.GalaxyGenerationChecks/GalaxyWorldChecks.cs | 84258C63B26C0DB716438206A5FF40DBAFFDCA9E45EA2394A2F22745DFB3833C |
| docs/reports/galaxy-generation-world-api-handoff.md | C9B1B0AACE5F9DDFCCED996509B6E6555FE2B4E90D44967271200C32D6138C2C |

Final hashes сверены reviewer read-only. Post-repair author/root-run combined78PASS
подтверждается прочитанным `artifacts/galaxy-integration-ready/world-reviewed-checks.txt`;
source build0warnings/errors сообщён автором. Reviewer команды повторно не запускал.
Acceptance относится к integration seam, не actual Session/M3 gameplay или full vanilla.
Producer остаётся ReadyForBootstrap; actual consumer startup transaction/manual gameplay
и прочие ограничения выше сохраняются.
