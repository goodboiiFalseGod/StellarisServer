# Navigation Boundaries NAV-BOUNDARY-01

Direct request, 16 October 2026: transition arrows use incorrect boundaries.
Diagnosis at `17e4889`: generated `SystemDefinition.Radius` grows with system contents, but `SpaceWorld.Exit` always places exits/entries at the old fixture `EXIT_RADIUS140`; `renderer.addNavigation` also draws an exit ring at 140. When outer orbits exceed 140, a fleet begins its jump inside the system. Fix authoritative movement and visuals together.

Keep the approved fixture 150/140 and provisional speed 7.2/charge 15/transit 5. Use one rule for generated and fixture systems, `SystemRelativeExitInsetV1`:

```text
exit radius = SystemDefinition.Radius - 10 local units
```

This is an explicit local policy, not a recovered native formula. Direction points toward the neighboring system's center on the galaxy map. Arrival uses the destination system's radius, not the origin system's.

Do not change source orbit geometry, BFS, permissions or commands.

On the client, build the inner exit ring from received server exit positions for the current system. Do not use the literal 140 or substitute client-calculated server coordinates. If there are no permitted exits, no ring is needed. The manual limit remains `system.radius`, inclusive. Do not add per-system wire fields: the large snapshot is already close to the 750000 limit. If validation checks radius, compare it with actual received positions.

One focused backend fixture: systems with different radii; actual order → approach → charge → transit → arrival at the destination's own radius minus 10; previous 150/140 fixture and bounds; permission failure preserves the earlier order. One headless renderer test checks actual `addNavigation`: ring/arrow/route use received coordinates, empty exits and the outer manual limit. No browser or broad test suite; independent review and a separate commit.

Do this after preserving GATEWAY-01; do not edit the renderer concurrently.
