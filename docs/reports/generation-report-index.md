# Сохранённые отчёты генерации 4.5.2

13 октября 2026. По поручению пользователя сохранены отчёты планетной, стартовой и галактической генерации с последующими снимками. Копии в локальном архиве проверены SHA-256 побайтно относительно оригиналов. Это сохранение доказательств, не утверждение о готовой реализации или полном совпадении native seed.

Отчёты реверса остаются также в outputs исследовательского чата `01a1359a-86d4-78f3-a4d2-5dda015220f6`, вне управляемых worktree. Локальный резервный архив: `content-local/references/generation-reports-2026-10-13/` от корня экспериментального checkout; он игнорируется Git. Не удалять его при очистке worktree без переноса и повторной проверки хешей. Исходные игровые файлы, бинарные дампы, изображения и игровые assets в архив не копировались. Ниже сохранены имена и хеши; локальные копии не загружать автоматически.

| Отчёт | SHA-256 |
|---|---|
| `galaxy-and-planet-generation-native-reverse-452.md` | `6C937582B9B7CD0E64B3C60A5ABD4DD177201AC5E9FBD208B238409F92266BC8` |
| `native-generation-algorithm-452.md` | `5B951C59B827F5B091613390A415546A1B6387A2219E3364EA0F505AA44E9BA0` |
| `planet-generation-native-reverse-452.md` | `04AAC26FC84813DF5858F1B77F6EF01E6551186C2F4C3360F81EE07BC0EF84F2` |
| `native-startup-pipeline-452.md` | `A3B53E56484553665C836AE1A79A72E0A4FD69BC1F83E5A8EB23A166CBCB96F8` |
| `startup-building-branches-452.md` | `6CA6CA4766F192D055CE89835612D30056BE38086EA5F4AB1A17FE509021551F` |
| `startup-authority-mobile-452.md` | `14F52D4936254873A4AA5F4CC02559EC3F542147FE343528ACC0C5F007EDC21C` |
| `startup-zone-first-dependency-452.md` | `CEDD107199BC08D7F858E29FB396F6AAF8AA4BF703CA8D4BCB180A07F373C7D0` |
| `native-generation-reverse-addendum-452.md` | `0DE1D764915C60F51F40B9A4DFBD52724353E28C585E1D7F30994D20CD1BAD89` |
| `galaxy-generation-attempt1-handoff.md` | `A695EBC438B790C5D13ACA9866663697C7088F19F18A9136AF926C3C2D56975D` |
| `galaxy-generation-candidate-evidence.md` | `131AD292D1462FDC5B6C60FD47B2D93518165E1B9D531DFE7F39224E4B1873E7` |
| `galaxy-generation-evidence-index.md` | `F587261F24039F05C6137E75A4F79435902054548C2C346AA2D188921B95B593` |
| `galaxy-generation-geometry-evidence.md` | `154FAA7328C62E4EE8CC3AE49283DF3CB870BD1B643197249EFD676382B530AB` |
| `galaxy-generation-native-primitives.md` | `50CDD782E27CA0A3B09F50A049657EE5DBA0BCD59818C677E42D795BAB2EB30F` |
| `galaxy-generation-placement-evidence.md` | `24EAFD8A294891D1FF481006F558ACB54B4230284A3AB9992FCC64A9DA34782D` |
| `galaxy-generation-post-freeze-notes.md` | `D94E63B14CA4B5FA09C000A64F669E6E137078E2AE71A87B61A4A79838A977CE` |
| `galaxy-generation-rules-evidence.md` | `112FE3D10E8627F37A10E73E626AAEA2235291CB2F00342171BF65F990BDAAB9` |
| `galaxy-generation-independent-review.md` | `0B361E74B2EC6D0C4ACD1C6C598C8B32581F5701E0F8988EE330D9B27C44A8E8` |
| `galaxy-generation-next-stage-contract.md` | `FA1401FF0437793FBFA5B75D11472A8033335481DF50E9046870D3B3621D35BD` |
| `native-generation-reverse-addendum-452-244EB6F2135B.md` (последующий снимок) | `244EB6F2135B698C7373896572D5E146731BBF47B29D158AA46FB6A6C1683C4E` |

Дополнение реверса сохранено из того же исследовательского чата. Десять новых отчётов реализации, независимого review и следующего этапа сохранены из `docs/reports` отдельного worktree `feature/galaxy-generation`, владелец `01a1361b-6c10-7862-b855-b6e6170f4b8d`; оригиналы оставлены на месте. Их архивирование не означает приёмку кода галактики и не блокирует планетный прототип.

Изменяющееся дополнение реверса дополнительно сохранено отдельным снимком с префиксом хеша в имени; прежний архив `0DE1D764…` не перезаписан. Новые алгоритмы использовать только с точной версией evidence, поскольку позднее исследование исправило прежнее обозначение post-relax clamp на division.

Следующий checkpoint `checkpoint-43d7700/` содержит 30 текущих отчётов галактики и реверса, включая новые исследования релаксации и покрытия. Имена, размеры и SHA-256 перечислены в его `manifest.json`, SHA-256 манифеста `9F6B32920E12372848AF83DF2B488B34F17B630E3C7C8970AE2B6E3DB7C2D5BB`. Предыдущие версии не перезаписаны. Все ранние копии и новый checkpoint дополнительно перенесены в projectless `generation-reports-archive` от 13 октября 2026 вне управляемых worktree; обе копии проверены побайтно. Очистка checkout теперь не является единственной точкой сохранности отчётов. Original game assets и бинарные файлы в эти архивы не включались.

Стартовые исследования дополнительно сохранены в Git: [pipeline](native-startup-pipeline-452.md), [здания](startup-building-branches-452.md), [authority/mobile](startup-authority-mobile-452.md), [zone placement](startup-zone-first-dependency-452.md). Их текущий принятый checkpoint находится в истории `5a67a51`. Отчёты реверса содержат ещё открытые контракты; перед использованием конкретного алгоритма требуется проверка его источников и ограничений.
