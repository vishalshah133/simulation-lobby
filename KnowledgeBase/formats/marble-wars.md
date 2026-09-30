# Format: Marble Wars (`wars`)

Factions of marbles collide in an arena; on contact one converts or destroys the other. Last team
standing wins. Naturally long-form — the appeal is the swing of territory over minutes.

## Rules on screen

"Touch converts. Last color standing wins." One line, shown once.

## Build notes

- Conversion-on-contact is more watchable than destruction — the population stays high and the
  visual of a color sweeping the arena is the whole product.
- Population is the story. Show a live **stacked bar of team counts** at all times; it is the
  retention device, more than the arena itself.
- Arena geometry should prevent stalemates: no safe corners, some churn (rotating walls, central
  attractor, periodic shrink).
- Start counts equal unless the premise is explicitly an underdog ("1 red vs 200 blue").

## Camera

Wide and mostly static — the swing of color across the whole arena is the point. Punch in only for
a last-survivor moment.

## What makes a good run

A near-wipe followed by a comeback. Seeds that produce a monotonic takeover are boring; scan and
discard them. Target runs where the leading team changes at least twice.

## Variations

- Underdog: 1 vs many.
- Three or four factions instead of two (messier, higher comment volume).
- Powerups that convert in an area.
- Temporary alliances between weakest teams.

## Failure modes

- One team snowballs in the first 20s → add churn or spawn separation.
- Stalemate oscillation that never resolves → introduce a timed arena shrink.
- Counts on screen too small to read on a phone.

## Open questions

- Does destruction-mode (counts drop to zero) retain better than conversion-mode (counts stay 200)?
- Optimal arena shrink timing — fixed schedule or triggered by population stability?
