# M2 — Questions and feedback

Related [acceptance plan](22-m2-manual-acceptance.md). Answers here are not accepted decisions yet: confirmed changes must be recorded in DECISIONS.md and the topic specification before implementation. General future-game questions remain in OPEN-QUESTIONS.md; M2 limitations are in M2-DISPUTES.md.

## Before testing

1. Should we first test with two tabs on one PC or immediately with two physical PCs on a LAN? Is a second PC available now?
2. Which browsers will the two players use? Suggested minimum: Edge or Chrome on Windows.
3. Which PC will run the server: the current one or Ryzen 5 3600 / 16 GB? If current, what are its CPU and RAM?

Answers help organize testing; they do not block a local run.

## After the main run

1. Which items T01–T14 passed, which were not checked, and where did results differ?
2. Is the difference between selected jobs and the report for the last calculated month clear? Could the old report be mistaken for a command not taking effect?
3. Are ordinary Workforce, bonus Workforce, job output, and fixed building upkeep clearly distinguished? Which labels/numbers are confusing?
4. Is it clear whether a command is pending, accepted, or rejected? Where does the interface seem stuck or hide an error?
5. Are return, observer roles, and host transfer convenient? A departure from agreed behavior is a bug; a request to change rules is recorded separately.
6. What most gets in the way of testing: readability, navigation, time controls, job editing, or missing diagnostics?

## Before the next milestone

The next focus is already selected: [M3 graphical slice](M3-GRAPHICAL-SLICE.md) with a map, UI, and movement. The minimum scenario, asset set, and main performance criteria are agreed; [technical clarifications](M3-QUESTIONS.md) will be resolved before the corresponding implementation. There is no need to reopen the choice of direction.

Take save, combat, and final pacing questions from OPEN-QUESTIONS.md only when the selected scope requires them; already accepted M2 rules do not need to be renegotiated. M3 separately allows a stage without saves; this does not cancel the target recovery requirements.

## Result or issue card

```text
Commit / OS / browser:
Server CPU and RAM / local or LAN:
Check ID and status:
Reproduction steps:
Expected:
Actual:
Reproducible: always / sometimes / once
Month, role, event time:
Screenshot / terminal excerpt:
Type: bug / usability issue / suggestion / question
```

Do not send access codes, tokens, or full sessionStorage. Redact secrets from logs before sharing them. Keep suggestions separate from defects: first check M2 against the accepted contract, then agree on changes.
