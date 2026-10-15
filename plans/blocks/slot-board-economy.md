# Block: Slot-Board Economy

**Status:** `building` (terr-001) · **Layer:** `Shared/Economy`, `Shared/Physics3D`, `Shared/Pooling`, `Presentation/UI` · **Used by:** `terr`; `rom` should migrate onto it

Balls drop through a physical peg board into slots, and each slot applies a count effect (×n, +n, release,
bonus) to a bank. This is the Release-or-Multiply engine, made independent of what the bank pays for.

## Pieces

| Piece | Layer | Job |
|---|---|---|
| `SlotEffect` / `SlotEffectKind` | `Shared/Economy` | Data for one slot: kind + value + label |
| `CountBank` | `Shared/Economy` | Saturating counter: multiply, add, drain-and-reset |
| `TriggerZone3D` | `Shared/Physics3D` | Collects bodies entering a slot; drained once per tick |
| `BodyPool3D` | `Shared/Pooling` | Capped, deterministic pool of board balls |
| `SlotReelHud` | `Presentation/UI` | Jackpot reel that lands on a result the sim already drew |

## Rules

- Drain triggers in a fixed order (board, slot, pool index). Callback order is never part of the outcome.
- Bonus outcomes are drawn in the sim the moment they trigger and applied N ticks later. The reel only shows them.
- Follow-up: move `rom`'s `GateKind` onto `SlotEffect` (adds Halve/Wipe kinds) when `rom` is next touched.
