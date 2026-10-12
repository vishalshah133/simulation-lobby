# Block: Territory Grid

**Status:** `building` (terr-001) · **Layer:** `Shared/Grids` + `Presentation` · **Used by:** `terr`; ready for `wars` colour-takeover variants

A tiled board where every cell has an owner, painted by balls that bounce off whatever they capture.

## Pieces

| Piece | Layer | Job |
|---|---|---|
| `OwnershipGrid` | `Shared/Grids` | Owners, per-owner counts (O(1)), version, append-only change log |
| `GridFrame` | `Shared/Grids` | Grid space ↔ world (corner transform + cell size) |
| `GridBouncer` | `Shared/Grids` | Sub-stepped paint-and-reflect for one ball; tiles are never colliders |
| `TileGridView` | `Presentation/VFX` | One runtime mesh + owner texture; pulse overlay on flips. Data-in, knows no grid type |
| `ShareBarHud` | `Presentation/UI` | Stacked N-way share bar: the always-on scoreboard |

## Rules

- Territory lives in grid space with our own maths. Never put 3,000 colliders in PhysX.
- The presentation reads `Owners` + `Version` and the change log from its last index. It never writes.
- The grid mesh is generated at runtime, not saved, so it avoids the stale-mesh trap (CLAUDE.md).
