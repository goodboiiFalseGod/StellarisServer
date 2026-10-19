# Control and Session Lifecycle

## Roles

Each player controls one assigned empire. The host can also manage pause, speed, game start, kicks, bans and transfer of authority. Transferring the host role does not transfer the server process or its data. Observers have full visibility but cannot issue gameplay commands.

There are no accounts, but participants must be identified reliably within a game. The working proposal is a server-issued secret reconnection token, separate from host permissions. An IP address is not sufficient identification.

## Connection and Control

Connection loss and empire status are tracked separately. The 60-second timeout uses real time and is independent of pause or game speed. Existing simulation continues before and after the transition to ordinary AFK; that transition gives no in-game advantage.

During ordinary AFK, existing orders, the economy, queued construction and the selected research continue. The server does not automatically select a new technology when current research finishes and does not make new strategic decisions. Previously configured automation needs its own explicit allowlist of actions.

Ordinary reconnection restores control of the player's own empire. Another player cannot take over an AFK empire. After a kick, the host decides whether the player may return; after a ban or surrender, control cannot be restored.

## Full Stasis

A kick, ban or surrender stops the empire's economy, research, construction and order execution. The empire remains in the world and can be attacked or conquered. Ships and stations continue returning fire.

Stasis must not be implemented by simply removing every entity from simulation. Incoming damage, destruction, conquest, ownership changes and permitted defense continue. Freezing progress must not create upkeep debt or cause queued work to complete immediately if a kicked player is allowed to return.

Queue cancellation or preservation, ongoing battles, retreat, events, treaties, regeneration and repairs are not yet defined. Agree on these boundaries before implementing the mode.

## Events and Diplomacy

Use the normal response deadline and default to refusal on expiry. Pending requests do not automatically pause the entire game. Define a separate agreed contract for events without a deadline, choices without a refusal option and mandatory decisions.

## Checks

The timeout works while paused; disconnection does not change the fleet profile; returning does not allow control of another empire; observers cannot send gameplay commands; a former host loses host permissions after transfer; and a prohibited return is rejected with a clear message.
