# Доказательства GALAXY-GENERATION-001 attempt1

13 октября 2026. База реализации `5a67a51`, изолированная ветка `feature/galaxy-generation`.
Отчёты — исследовательские свидетельства, не требования и не доказательство полного native совпадения.

Исходные три Markdown отчёта исследовательского чата `01a1359a-86d4-78f3-a4d2-5dda015220f6`
сохранены вне worktree, в outputs чата `planet-generation-reverse-452` за 2026-10-13.
13 октября проверены наличие и SHA-256. Они не изменены и не скопированы в Git.
Резервные семь файлов координатора находятся в ignored
`content-local/references/generation-reports-2026-10-13/` его checkout;
индекс координатора проверен в `095aa96:docs/reports/generation-report-index.md`.
Проверены также наличие и хеши всех семи резервных файлов: совпали с индексом координатора.
Этот собственный индекс сохраняется как обычный файл, независимо от состояния чата.

## Collision/clamp и no-ID placement journal

Addendum checkpoint SHA
`0DE1D764915C60F51F40B9A4DFBD52724353E28C585E1D7F30994D20CD1BAD89`
проверен author/reviewer по отдельным единым bytesnapshots. §16.4 содержит обе
distance branches, scaled/ordinary vectors и retry10000/10001 correction.
[Own placement evidence](galaxy-generation-placement-evidence.md).
§20/§21 M36/M29/M37 — pointers внешнего evidence, не интегрированные стадии.

## M11/M13 geometry checkpoint

Addendum SHA `703AAA62FC1FEBCAE1C8657141BB1D01F6E6994D1EFF8C64069A0CB05DBF5CB9`
проверен по единому bytesnapshot. Восстановленные algorithms требуют exact embedded
Triangle1.6/SINGLE backend, которого здесь нет. Dependency-free centroid и input
quantization реализуются отдельно; composite stages не объявляются готовыми.
[Собственный конспект](galaxy-generation-geometry-evidence.md).

## Продолжение координации: M16/M20 и pre-relaxation candidate

Пользователь поручил применять новые отчёты по мере успехов реверса.
Текущий использованный durable checkpoint addendum:
`1323B90D713D589F531FF9E751C045C6F10BBA0AA6576DBECA9DC1A221AE5ACE`.
Hash рассчитан по одному прочитанному bytesnapshot; §12 содержит widths/vectors,
§16.6 — exact fixed/trig evaluation order и candidate A/B.
Санитизированное собственное evidence: [M16/M20](galaxy-generation-rules-evidence.md),
[candidate](galaxy-generation-candidate-evidence.md).
Ниже checkpoints M01 и initial freeze сохранены как история используемого evidence.

## Текущий M01 checkpoint после прямого поручения пользователя

13 октября после freeze пользователь поручил независимое ревью и реализацию M01.
Прочитан addendum §10.1–10.4, SHA256
`44E346A9E3A32DFC180CD62EB805E1C2CCAFD704DEBF2047FD405BEA590109C1`;
hash повторно проверен после чтения и подтверждён research-партнёром.
Read-only hash установленного `stellaris.exe`:
`400DF27C82DDC845AA9DCE79BD468D81F18F060D299CD263E3AFA93AEF4F7A83`,
совпадает с provenance report. Raw bytes/disassembly не копировались и не сохранялись.
M01 теперь реализован только для explicit local seed, без auto/global RNG;
четыре независимых state/first-draw vectors сохранены в [конспекте](galaxy-generation-native-primitives.md).
Ниже hash91C/BB8D и M01-open описывают первоначальный freeze, не текущую реализацию.

| Исходный файл | SHA-256 |
|---|---|
| galaxy-and-planet-generation-native-reverse-452.md | 6C937582B9B7CD0E64B3C60A5ABD4DD177201AC5E9FBD208B238409F92266BC8 |
| native-generation-algorithm-452.md | 5B951C59B827F5B091613390A415546A1B6387A2219E3364EA0F505AA44E9BA0 |
| planet-generation-native-reverse-452.md | 04AAC26FC84813DF5858F1B77F6EF01E6551186C2F4C3360F81EE07BC0EF84F2 |

Сохранённые стартовые исследования базы: [pipeline](native-startup-pipeline-452.md),
[building branches](startup-building-branches-452.md),
[authority/mobile](startup-authority-mobile-452.md),
[zone dependency](startup-zone-first-dependency-452.md).
Их реализация не входит в эту задачу.

## Источники первого среза

- `map/setup_scenarios/*.txt`: проверены реальные определения vanilla 4.5.2;
  scenario хранит `num_stars`, `radius`, повторяемый `supports_shape`, настройки и partitions.
- `map/galaxy/galaxy_shapes.txt`: проверены реальные определения, включая точное написание `seperation`.
- `launcher-settings.json`: runtime версия импорта. Каждое прочитанное содержимое хешируется;
  исходные scripts остаются только в установке и памяти процесса.
- Native master `0x14098D4E0–0x14098E2B7`: порядок стадий взят из algorithm report §3;
  первоначальные M01–M40 из §10 уточняются только новым достаточным evidence.
- Новый `native-generation-reverse-addendum-452.md` из тех же outputs, SHA-256
  `91C7DA143A34F096FB1B5D761C0667A59FC16258738DA96767CBA6156B91BA6A`:
  файл прочитан и хеш проверен 13 октября. Закрывает M02 и raw M04;
  M01 world seed, M03, M06–M11, M38 остаются открытыми.
  Собственная санитизированная фиксация использованного подмножества:
  [native primitives](galaxy-generation-native-primitives.md).
  Предыдущий прочитанный checkpoint имел SHA-256
  `BB8D46B96EAA1ED1C1CC2A08DA3A988B293A4689BC9C72E6D558F4A3CD9B5442`;
  партнёр дополнил новый файл parser evidence. Исходные три отчёта не менялись,
  M02/M04 и vectors дополнением не изменены.

Никакие bytes executable, disassembly, игровые scripts или assets здесь не сохраняются.
Собственные контракты и проверки: [границы этапа](../GALAXY-GENERATION.md).
