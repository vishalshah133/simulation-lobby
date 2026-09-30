# Format: Impact / Object vs. Object (`impact`)

One thing meets another, once. The hook is purely **what happens when it hits.** No rounds, no
score, no threshold.

> **Status: `idea` again (2026-09-30).** The code first built under this slug is now the `surv`
> format, *Wall vs Ball*. See [survival-wall.md](survival-wall.md). What was built here drifted
> into survival: nine escalating waves at a wall that shrank between them. That isn't the shape the
> reference channels run. This brief now describes the reference shape only, plus what the
> prototype taught about why the cheap version of it doesn't work.

Source: adjacent-channel research, 2026-09-29 (`KnowledgeBase/reference-channels.md`, "Adjacent
categories"). `Kawaken_3DCG`, `RenderZen`, and `Oyen_3D` all run variants: "Balloon vs Spike
pressure test", "Cone vs Shredder Soft Body Simulation", "Hitmonlee vs 15,625 Cubes", "Dropping
Realistic Water Physics on Minecraft Alex". Consistently their strongest-performing shape.

## What the reference shape actually is

- **One event.** A single press, drop, cut or collision. No series, no escalation loop.
- **The spectacle is the material, not the count.** Soft bodies deforming, fluid pouring, things
  shredding, a shape fitting perfectly. The satisfaction is *how the material behaves*, which is
  why the event can carry a whole video on its own.
- **Short.** Typically 15-40s.
- **Not competitive, not threshold.** The single-event spectacle tension type in
  `channel-strategy.md`.

## What the prototype proved (2026-09-29)

`impact-001` built the cheapest possible version: one rigid ball into a 300-block wall of plain
rigidbodies. **It settled in 3-4 seconds.** The flight is under a second, and rigid scatter damps
out almost immediately. It was physically correct and unpublishable.

The lesson: **plain rigidbody scatter isn't the spectacle.** The reference channels' single events
last because soft bodies, fluids and fracture take time to *unfold*. A rigid cube is knocked away
and stops. The earlier assumption that "15,625 Cubes" meant ordinary rigidbody scatter, and that
this was therefore the cheapest format to build, was wrong, or at least wrong for making a video
of it.

The prototype's fix was to bolt on a series (waves) and an escalation (a growing ball). It worked,
but it made a *different format*: survival. That code now lives in `Simulations/Survival` and is
documented in [survival-wall.md](survival-wall.md), including every physics, audio and settle
lesson the build produced.

**General lesson, beyond `impact`:** a physics event isn't a video. Ask of any new format "how many
seconds of footage does one event produce, and what makes the viewer want to keep watching?" If
the answer needs a series bolted on, check whether you're still building the format you set out
to build.

## If this is picked up again

A real `impact` build needs a physics behaviour that *unfolds*, not just scatter:

- **Soft body / cloth** (a balloon pressed onto spikes, jelly dropped on a grid). Unity has cloth,
  but not true soft bodies. Mass-spring meshes are a real build.
- **Fracture** (a slab shattering into pre-fractured chunks that tumble). Cheapest of the three in
  Unity, since pre-fractured meshes are just many rigidbodies with joints that break.
- **Fluid / particles** (thousands of small spheres poured onto a shape). Close to
  `AlgoMarblerYT`'s "N marbles vs object". Probably the cheapest *long-lasting* single event, and
  overlaps `rom`'s pooling needs.

Any of these is a materially bigger build than the prototype. Don't pick it up until the survival
format has shown whether 3D reads well on this channel at all.

## The sweep variant (built 2026-09-30)

RenderZen's "0% vs 100%" series repeats one event at several settings of one knob, with a caption per
take (`reference-channels.md`). That answers "one event is too short" without bolting on escalation:
each take is a fresh payoff at a fixed rhythm. First build is
[impact-003](../../plans/videos/impact-003-soft-sweep.md): an amber glass capsule dropped onto a
lacquer plinth, softness 0 → 100%, on new blocks `soft-body-xpbd`, `parameter-sweep`,
`studio-look-3d` and `material-contact-audio`.

### What building the soft body taught (headless harness, 2026-09-30)

The solver was verified outside Unity: a console harness compiling `Shared/SoftBody3D` against a
stand-in for Unity's math types, stepping every take and measuring particle speed, rebound, sag,
volume and determinism. Every bug below was invisible in code review and obvious in numbers.

- **The spike was dropped.** A closed soft body can only slide off a convex point. A needle, a
  0.14m dome and a 0.22m chrome ball all shed it, centred or not (float asymmetry is enough). The
  reference's impaled look needs the skin to *tear*. An impale mode was built (`SdfCollider.pierceable`,
  sideways push plus drag) and it does impale, but without tearing the skin around the hole can't
  stretch to the spike's girth and jitters at 100+ m/s. A tearing block is the prerequisite for
  any spike/blade/grater video.
- **Tilt or offset makes an upright capsule topple** and roll off the plinth (every take at 8°).
  Drop straight. It's also the fairer one-variable comparison.
- **Float32 volume cancels badly** if measured from the world origin: ~0.5% noise that the volume
  constraint "corrects" into m/s of jitter. Measure from the centroid, in double.
- **UV-sphere meshes are unusable for a solver.** Dozens of needle triangles meet at each pole and
  it rang at 100+ m/s. Use rings with vertex count proportional to circumference (`SoftBodyShapes.Capsule`).
- **Rigidity by shape matching doesn't work.** A push on one of ~750 particles moves the best-fit
  pose by 1/750th, so the body sinks and is then ejected. 0% is a true rigid body (XPBD rigid, Müller 2020).
- **Stiff edges ring at contact.** Keep edge compliance soft at every setting and let shape
  matching carry the stiffness range.
- **A stiff shell around an incompressible volume is a rubber ball** (~70% rebound), and no
  damping fixes it, because the energy returns as whole-body motion. Cap body-level restitution
  (`softRestitution` 0.15). Separately, strong deformation damping at the stiff end (600/s)
  stops ringing.
- **First playtest (same day): "50/75% barely deform, 100% should behave like a fluid."** Right:
  it squashed only ~14% at every setting. Two causes. The skin was stiff (squashing at constant
  volume stretches the surface), and shape memory stayed high through the middle takes. Fix: a
  stretchy skin everywhere (edge compliance 1) and shape memory falling as
  `0.3 × ((1−s)/s)^1.26` per second, reaching 0 at 100%. The earlier "low shape memory topples
  the capsule" finding was really stiff skin plus low friction. With a stretchy skin and friction
  1.0, a no-memory body splats evenly. Result (squash on impact → settled height):
  25% 73→90%, 50% 60→81%, 75% 45→65%, 100% 33→42% (a water-balloon splat 0.9m wide, on a 2m plinth).
- **Second playtest: a hole narrower than the capsule makes the best gradient yet.** The glass
  rests in a brass cup, and softer takes squeeze deeper (19 → 39 → 49 → 59cm). Two rigid-body bugs
  surfaced and were fixed. (1) A thin lip slipped between the rigid body's collision points: the
  rigid path now samples particles + edge midpoints + triangle centres, and the lip tube is kept
  ≥ 0.06. (2) Per-contact restitution on a ring of tilted contacts was order-dependent and added
  energy (a centred drop flew off sideways): bounce and sliding friction are now applied once, at
  the λ-weighted average contact.
- **Third playtest: "the soft body doesn't go in the hole."** It did, 39–59cm, but a camera 7°
  above the plinth top can't see into a hole, and the opaque plinth hid everything below the rim.
  The plinth is now smoked glass. Catch for any glass-inside-glass shot: the hero glass refracts the
  *opaque* image, which never contains transparent objects. So the outer glass is plain
  alpha-blended URP Lit, drawn after the hero glass (queue +10), not a second refraction shader.
  Otherwise each would be invisible to the other.
- **Lacquer is only as bright as what it reflects.** The plinth front read as a black hole until a
  softbox card sat where that face's reflection lands (behind the camera, just above camera
  height).
- **A membrane can only get "water balloon", not liquid.** Its volume constraint and skin keep a
  dome. A true pour or puddle needs particle fluid (Obi Fluid, Zibra Liquids) or an offline bake.

## Title / hook patterns

"X vs N of Y". Name the impactor, name the scale: "Bowling Ball vs 10,000 Dominoes", not "Ball Hits
Blocks".

## Open questions

- Is fracture enough spectacle to carry 15-40s, or does it need soft body / fluid?
- Does a flat-landing softness sweep carry a Short without the spike's drama? impact-003 answers it.
- Does a single-event Short work on a channel whose audience so far came for escalation (`esc`)?
