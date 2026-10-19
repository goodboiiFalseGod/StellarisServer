# Habitable Worlds HABITABLE-01

Direct request, 16 October 2026. Core already applies `HabitableMultiplier` to weights for colonizable classes in random class selection and to source inline-usage weights. M3 currently creates a `GalaxyLobbyRequest` with an implicit multiplier of 1 and no player setting. Expose the existing generation setting to the host and validate it.

Add a separate M3 habitable-world-frequency setting with default 1, local M3 domain 0..5 and precision 5; use the same validation for commands and UI. Do not narrow the core's existing 0..100 domain. This is a prototype-interface limit, not a claim about the exact vanilla slider. Do not confuse it with `GuaranteedColonies` 0/1/2.

Pass the setting through host lobby → validated command → resolved request → existing weight pipeline → snapshot/fingerprint/UI. Do not create a second class allocator or change native source weights/conditions. Ordinary colonizable types come from source definitions, not an invented list. At 0, random colonizable weight is 0; explicit home/ideal and guaranteed scripted worlds remain. Higher values change weights; they do not promise a particular count or a monotonic count for every seed.

One focused source fixture checks random colonizable weights 0/1/5, source star coefficients, and explicit home/guaranteed classes. M3 settings must pass end to end; malformed/fractional/out-of-range values and commands from a non-host are rejected. One headless client fixture covers controls and confirmed settings. Do not sweep seeds or sizes and do not run a browser. Complete independent review and a separate commit before moving to another feature.
