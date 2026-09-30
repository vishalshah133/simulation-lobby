# Block: Soft Body (XPBD)

**Status:** `built` (2026-09-30, awaiting first playtest) · **Layer:** `Assets/Scripts/Shared/SoftBody3D/` · **Used by:** `impact`

A deformable closed mesh that squashes, drapes and wobbles, with one `softness` knob from rigid (0)
to jelly (1). Unity has cloth but no soft bodies, and `impact` needs something that *unfolds* on contact
(`KnowledgeBase/formats/impact.md`). Plain rigidbody scatter settles in 3–4s and isn't the spectacle.

## Why XPBD and not PhysX

- **Deterministic by construction.** Plain single-threaded C# over a fixed particle and constraint order,
  stepped from `FixedUpdate`. No solver state we don't own. Same machine, same seed, same bytes.
- **One knob maps cleanly.** XPBD *compliance* is a physical stiffness that doesn't depend on timestep,
  so "Soft 50%" means the same thing at any substep count.
- It's the standard technique (Müller et al.), well documented and a few hundred lines.

## Parameterized

| Knob | Notes |
|---|---|
| `softness` 0–1 | Master knob. Maps to the three below through curves in the config, not hard-coded |
| Edge compliance | Stretch. 0 at softness 0 |
| Volume compliance | Global pressure/volume preservation. Keeps jelly from collapsing flat |
| Shape-matching stiffness | 1 at softness 0 (behaves rigid), falls toward ~0.05 at 1 |
| Damping | Per-substep velocity damping. Too low = endless jiggle, too high = clay |
| Substeps | 10–20 per fixed step. Raise before touching iterations |
| Source mesh | Icosphere or capsule, welded (shared vertices), subdivision level 3–4 |
| Collider friction | Per analytic collider |

## Steps

1. `SoftBodySolver` (pure C#, no `MonoBehaviour`): particles (pos, prev pos, inverse mass),
   edge constraints, one global volume constraint, shape-matching pass. `Step(dt, substeps)`.
2. **Analytic SDF colliders** (`PlaneCollider`, `ConeCollider` with a **rounded tip**, `BoxCollider`,
   `CylinderCollider`). They're cheap and exact, and they keep PhysX out of the loop. Particle-vs-SDF
   projection with friction.
3. `SoftBody3D` component: owns the solver, steps it in `FixedUpdate`, exposes **read-only metrics**
   for Presentation: `ContactImpulse` (this tick), `VolumeRatio`, `MaxStrain`, `CentroidVelocity`,
   `WobbleEnergy`, `HasContacted`. Audio and camera read these and nothing else.
4. `SoftBodyMeshView` (**Presentation**): copies positions into a `Mesh` in `LateUpdate` and
   recalculates normals and bounds. Highlights have to slide across the deforming surface; frozen
   normals look like plastic wrap.
5. Profile at subdivision 4 (2,562 particles). Offline render doesn't need real time, but preview
   should hold ≥20 fps. Fallback: level 3 for the sim plus smoothed normals.
6. **No Burst unless `FloatMode.Deterministic`.** Fast-math reorders floats and breaks reproducibility.

## Known risks

- **Tunnelling through a sharp tip.** Particle spacing is larger than the tip radius, so the spike pokes
  between particles. Mitigate with a rounded tip ≥ half the particle spacing, plus a triangle-vs-point
  check if needed.
- **Self-intersection** when very soft bodies fold. The volume constraint prevents most of it. Self-collision
  is out of scope for v1. If it shows, raise volume stiffness.
- **No tearing/puncture** in v1. The skin drapes over the spike. Puncture is a later block.

## As built — where it differs from the plan above

Verified in a headless harness before handover (see `KnowledgeBase/formats/impact.md`, "What
building the soft body taught", for the numbers):

- **0% is a true rigid body** (XPBD rigid: pose, velocities, inertia-weighted contacts, restitution),
  not shape matching at stiffness 1. That ejected the body.
- **Mesh:** rings with vertex count proportional to circumference, not a UV sphere or icosphere
  (`SoftBodyShapes.Capsule(radius, halfLength, segments)`, ~750 particles at 40).
- **Edges stay compliant at every softness.** Shape matching carries the stiffness range.
- **Added:** a contact velocity pass (touching particles can't separate), body-level restitution
  (a soft body is otherwise a rubber ball), and deformation damping relative to best-fit *rigid*
  motion (Müller 2007), not just the mean velocity.
- **Volume is measured from the centroid in double.** From the world origin, float32 cancellation
  made it jitter.
- **Point guards work for the rigid path. Soft bodies can't rest on a point at all**, they slide off.
  The experimental `pierceable` impale mode needs a tearing block before any video uses it.

## Acceptance criteria

- [ ] softness 0 drop: visually rigid (no visible squash), bounces/rolls off.
- [ ] softness 1 drop: drapes and wobbles ≥1.5s before settling, no collapse, no explosion.
- [ ] Same config twice → identical particle positions at tick N (hash compare via `DeterminismVerifier`).
- [ ] Volume stays within ±10% of rest at every softness.
- [ ] No particle ends inside a collider (post-step SDF check logs zero violations).
- [ ] Deleting `SoftBodyMeshView` and all audio changes nothing in the sim (read-only proof).
