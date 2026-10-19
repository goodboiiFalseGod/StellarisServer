# Fleets and Combat

## Foundation

The system should provide an experience analogous to current Stellaris combat; changes require agreement. Ship, section and component definitions, and their behavior, come from target content within the supported set.

Required features include fleet composition, ship design and construction, orders, inter-system travel, tactical movement, target selection, damage, destruction and retreat. Exact formulas and simulation granularity have not yet been approved.

The client presents combat using server events and permitted state. A visual projectile does not determine a hit or damage.

## Orders and Profiles

Players set profiles and options. When disconnected, existing orders and settings remain; the controller does not assign new strategic objectives. The provisional names Passive, Defensive, Escort and HoldPosition describe desired profile capabilities, not their exact semantics.

Before implementing each profile, define reaction range, whether it may leave the system, pursuit, escort behavior, target loss, unavailable routes and retreat conditions. Distinguish automatic tactical execution of an order from independent strategic decisions.

## Retreat

A retreating fleet is hidden and returns regardless of ordinary route obstacles. If its original base is lost, consider another random eligible base or the capital planet. Random choice uses the server's reproducible RNG.

Clarify eligible bases, when a target is selected and rechecked, time away, retreat losses, fallback when there is no base or capital, and interaction with full stasis. Do not replace retreat with ordinary pathfinding.

## Empire in Stasis

Its ships and stations return fire and take damage; strategic orders are stopped. Behavior during an ongoing battle, tactical movement and retreat require separate agreement.

## Checks

Reproducible combat with a fixed seed; a target destroyed while firing; an escorted object disappearing; loss of the base during retreat; hiding an absent fleet; return across closed borders; and stopping orders for a static empire while preserving return fire.
