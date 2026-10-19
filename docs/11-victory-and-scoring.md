# Victory Points and Achievements

Status: preliminary foundation selected by the user on 11 October 2026. Implementation has not started. Source: [provided POINTS EXPLANATION](references/victory-points-source.txt), preserved unchanged. Source contradictions are not silently corrected.

## Model

Players accumulate points in two ways: periodic rankings and achievements triggered by events. The game end date is configurable; a fixed end at year 60 has not been established. The working proposal is that the eligible participant with the highest total wins. Agree on setting units and range, default value, whether it can change after start, eligibility, and how a final tie is resolved before implementation.

Source intervals: every 10 years, at years 10, 20, …, 100. The project retains a 50–60-year balance target; the actual duration is set by configuration. The source's mention of 100 years does not establish a mandatory duration or an upper setting limit. Proposal: count years from the start of the game. The exact in-game day boundary, ordering relative to monthly processing, and any final award outside the normal interval are not yet defined.

## Interval Categories

| Type | Places | Points by place, in order |
|---|---|---|
| Primary | 1–10 | 7, 4, 4, 3, 3, 2, 2, 1, 1, 1 |
| Secondary | 1–3 | 3, 1, 1 |

Places outside the awarded range receive 0. Rankings are calculated separately for each category; results are added to accumulated points rather than replacing previous awards.

| Category | Type | Source metric |
|---|---|---|
| Economy | Primary | Stellaris in-game economic weight; not simply balance or net income. Research the formula for the target version. |
| Science | Primary | Current science production; clarify what is included and the measurement period. |
| Unity | Primary | Current net Unity income: production minus expenses. |
| Diplomatic Weight | Secondary | Diplomatic Weight as used in the Galactic Community, regardless of membership. |
| Controlled Space | Secondary | Number of systems owned by the player. |
| Controlled Megastructures | Secondary | Number of controlled megastructures past the site stage, including restored ones. |

Excluded from the megastructure count: orbital rings, habitats, gateways, kilostructures and hyperrelays. Map game IDs to the source's terms after content selection. The ownership/occupation rule for megastructures is not yet defined.

### Tied Metrics

The source requires tied participants to receive the lower score among the points for the tied places: two players tied for first receive 4 each; two tied for tenth receive 0.

Proposed generalized interpretation, not yet approved: a group of k participants starting at place r each receives the points for place r+k−1; the next participant starts at r+k. For example, three tied for first in a primary category receive 4 each; four receive 3 each; two tied for third in a secondary category receive 0. Do not arbitrarily break ties by EntityId and assign different points for equal metrics.

Zero metrics, negative Unity, precision for comparing fractional values, and eligible-player membership require agreement.

### Other Players' Starting Worlds

Homeworld IDs are fixed at game start; moving the capital does not change them. Points are awarded for other players' starting worlds, not their current capitals.

Accepted by the user: **4 points for each controlled starting world belonging to another player, on every interval**. The source contradiction (7 in a general paragraph and 4 in a separate category) is resolved in favor of 4; the source text is preserved unchanged. Still to define: ownership versus occupation, what happens if a world is destroyed, and how to handle empires without a standard starting world.

## Source Achievements

The table preserves the complete reference. A listed row does not enable its mechanics in the first version. Wherever “first” is specified, define the scope explicitly; do not assume it is galactic by default.

| Name | Condition | Points |
|---|---|---|
| Winning War | Achieve war goals; only the main attacker and main target, not wars against AI | 7 |
| First Blood | First victory in the galaxy in each war category; examples: Animosity, Subjugation, Plunder, Expropriation, Conquest, Liberation, Ideology, Independence | +7 |
| Big Enemy, Much Glory | Defeat an opponent who had at least 10% more points at the start of the war; rule is WIP | Additional 10% of the loser's points; baseline timing is unclear |
| Master Builder | First to complete a four-stage megastructure / Galactic Wonder; repairs award half; ring-world segment repair is separate | 2 / 4; repair 1 / 2; segment 1 |
| Kilotons | First to complete a kilostructure | 1 |
| I am the Senate | Become Galactic Emperor | 10 |
| Custodes | Become Custodian; removal of a Custodian is tied to a point loss, recipient to be clarified | +5 / −5 |
| Senator | First to receive a council appointment from the Emperor | 3 |
| Councilor | Become a member of the Galactic Council | 3 |
| To Infinity and Beyond | Open the L-Gates | 3 |
| Golden Horde Throne | Kill the Khan and obtain the Khan's Throne | 7 |
| Defender of the Galaxy | Destroy a Fallen Empire | 7 |
| Diplomat | Pass a law you proposed | 1 |
| Traditionalist | Complete a tradition tree | 1 per tree |
| Ascensionist | Complete empire ascension | 1 |
| Whaler | Clear the Tiyanki home system | 1 |
| Nuketown | Successfully use a Colossus on an inhabited planet | 1 per planet |
| Bulky | Build the first Juggernaut | 1 |
| Indiana Blorges | First in the galaxy to enter a precursor home system | 1 per system |
| Heretic | First to make a pact with an Eldritch Dark Entity | 1 |
| Dr.Frankenstein | Reanimate a Leviathan | 1 per Leviathan |
| Evil Surgeon | First to vivisect the crew of a player's science ship | 1 |
| Has reached Infinity, twice | First in the galaxy to research Jump Drives | 1 |
| Has delved too Greedily and too Deep | Kill a Crystalline Entity while playing as a Subterranean Empire | 1 |
| Hast vanquished a False Deity, Most Noxious | Kill the Toxic God, neutral or player-controlled | 1 |
| Pulled from Beyond | First in the galaxy to summon a fleet from another dimension | 1 |
| Has established a Joint Venture | First in the galaxy to open a branch office on another player's capital | 1 |
| Has reached Immortality | Make a ruler immortal | 1 |
| Has reached the peak of their career | Obtain a level 10 leader | 1 |
| Has acquired a nice Trinket | Obtain the first relic | 1 |
| Dig through space | Complete the first astral rift | 1 |
| Has dug through Time | Complete the first archaeological excavation | 1 |
| Ka-Boom! | Your homeworld explodes with the Doomsday Origin | 1 |
| Abolish the Golden Rule | Get rid of the ruler with Under One Rule | 1 |
| Galactic Nemesis | Annual award/deduction involving other empires with an intact Aetherophasic Engine; wording is ambiguous | +1 / −1; recipients and multiplier are open questions |
| Galactic Nemesis Ended | Your fleets are present in the system when the Aetherophasic Engine is destroyed | 10 |
| Planetary transformations | Create/restore an Ecumenopolis; terraform/use Gaia Seeders to make a Gaia, Hive or Machine World; once per type, inhabited controlled planet | 4 for Ecumenopolis; 2 for other types |

For Winning War, clarify a status quo, surrender, coalition sides and participant exclusions. First Blood needs an approved list of categories and a rule for simultaneous victories. For planetary transformations, clarify whether the inhabited-planet and once-only restrictions also apply to Ecumenopolis.

## First-Version Subset

Discussion proposal: start with Economy, Science, Unity, Controlled Space, other players' starting worlds, Winning War and Traditionalist. Add First Blood after war categories are fixed. Preserve source values except for explicitly agreed changes.

Diplomatic Weight and megastructures require additional calculations and mechanics. Achievements depending on NPCs, crises, galactic offices, archaeology, rifts and special origins remain in the reference until the corresponding content exists. Disabled achievements are explicitly marked in the game profile; silently awarding them or substituting a condition is not allowed.

## Proposed Technical Requirements

- A versioned points profile is fixed before start: intervals, award tables, active categories and achievements. Changes during a game require a separate policy and are not currently authorized.
- Awards are based on server-side facts, not client claims. Achievements and interval awards are protected against duplicate application.
- The points ledger records the recipient, category, interval or triggering event, score change, tick and rules version. On rollback, the ledger rolls back with the world.
- The snapshot preserves points, earned achievements, global “firsts,” original homeworlds, interval schedule and data for the initial comparison of war bonuses.
- The UI shows accumulated points, a breakdown and the next interval. Publishing other players' metrics/achievements must follow intel rules; observers have full visibility.
- The result is computed from the agreed state after the last permitted award. The order for ending the game and processing events on the same tick must be explicit.

## Checks Before Acceptance

1. Primary and secondary rankings with 10 and 20 players; places outside the range receive zero.
2. Source examples: two tied for first in a primary category → 4 each; two tied for tenth → 0 each. Extended tie cases require approval.
3. Interval awards occur only once, including after reconnect, restart and replay.
4. Moving the capital does not change the homeworld; capture/occupation awards follow the approved rule.
5. A coalition participant does not receive Winning War for participation alone; a status quo is checked separately.
6. Once-only and global achievements are not awarded again after an object returns or a save is loaded.
7. AFK/static changes and concession of defeat follow the agreed eligibility rules.
8. Ledger and total match after recovery/rollback; hidden information does not leak through the score table.
9. The last interval and final tie are checked at the approved game-end boundary.
10. One other player's starting world awards 4 points per interval, two award 8; a player's own world awards no points in this category.
11. The game ends according to the configured duration, not a hard-coded year 60. The setting is saved and restored with the game.

Open details are listed in Q02 and Q18–Q24 of [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md). The foundation is accepted; score implementation awaits answers to dependent questions.
