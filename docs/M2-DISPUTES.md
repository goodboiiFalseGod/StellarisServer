# M2 — Questions and Limitations

| ID | Status | Detail |
|---|---|---|
| M2-01 | User-agreed | Host disconnect preserves speed/pause without automatic transfer of rights; process restart ends the test session. |
| M2-02 | Technical boundary | JSON/full snapshots, DOM without assets, and local PC/trusted LAN only. Internet, final networking and rendering are later stages. |
| M2-03 | Laboratory mode | Job slots can be edited without construction; speed is measured in months/second. This does not change target Stellaris rules. |
| M2-04 | M1 limitation | Shortage atomically stops monthly calculation; the full vanilla shortage system is not implemented. |
| M2-05 | Verification boundary | Local server checked with real WebSockets and three Chromium tabs. Physical LAN, separate Edge and a stressed slow TCP receiver were not checked; queues are bounded, sends have a timeout, and calculation does not wait for the network. |
| M2-06 | Resolved by review | Retrying unacknowledged commands could send more than 20 messages/second. Added a shared client queue capped at 100 commands and 8 commands/second, only after a fresh snapshot. |
| M2-07 | Fixed; manual check pending | With a blackhole, the socket could remain OPEN without responses while commands remained available. Added a 30-second client deadline and safe return; contract M2-CONNECTION-LIVENESS, automated checks M2-LIVENESS-VALIDATION. |

Add new disputed questions here; move extensive context to a separate linked file. Keep the main contract concise.
