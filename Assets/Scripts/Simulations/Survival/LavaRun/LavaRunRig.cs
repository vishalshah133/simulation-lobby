using System;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>
    /// A kinematic piece that moves between its rest pose (t = 0) and a displaced pose (t = 1): pen gate
    /// (rest = closed, 1 = open), basin door (rest = open, 1 = closed), trapdoor (rest = shut, 1 = open),
    /// island rings (rest = up, 1 = sunk). The simulation drives <c>t</c> from its fixed tick; if the target has a
    /// kinematic Rigidbody it is moved with MovePosition/MoveRotation so marbles are carried by a moving collider.
    /// </summary>
    [Serializable]
    public sealed class LavaMover
    {
        public Transform target;

        [Tooltip("World-space displacement of the target at t = 1.")]
        public Vector3 moveWorld;

        [Tooltip("Euler rotation (degrees, applied on top of the rest rotation, in the target's frame) at t = 1.")]
        public Vector3 rotateEuler;

        Rigidbody _body;
        Vector3 _restPosition;
        Quaternion _restRotation;
        bool _captured;

        public LavaMover()
        {
        }

        public LavaMover(Transform target, Vector3 moveWorld)
        {
            this.target = target;
            this.moveWorld = moveWorld;
        }

        public bool IsValid => target != null;

        /// <summary>Remember the rest pose (first call only, so a re-Initialize never captures a displaced pose).</summary>
        public void Capture()
        {
            if (_captured || target == null)
            {
                return;
            }

            target.TryGetComponent(out _body);
            _restPosition = target.position;
            _restRotation = target.rotation;
            _captured = true;
        }

        /// <summary>Jump to pose <paramref name="t"/> without physics interpolation (used at Initialize).</summary>
        public void Snap(float t)
        {
            if (target == null)
            {
                return;
            }

            Capture();
            Pose(t, Vector3.zero, out Vector3 position, out Quaternion rotation);
            target.SetPositionAndRotation(position, rotation);
            if (_body != null)
            {
                _body.position = position;
                _body.rotation = rotation;
            }
        }

        /// <summary>Move to pose <paramref name="t"/> (0..1) plus a world-space offset (used for the ring tremble).</summary>
        public void Apply(float t, Vector3 extraOffset)
        {
            if (target == null)
            {
                return;
            }

            Capture();
            Pose(t, extraOffset, out Vector3 position, out Quaternion rotation);
            if (_body != null && _body.isKinematic)
            {
                _body.MovePosition(position);
                _body.MoveRotation(rotation);
            }
            else
            {
                target.SetPositionAndRotation(position, rotation);
            }
        }

        void Pose(float t, Vector3 extraOffset, out Vector3 position, out Quaternion rotation)
        {
            t = Mathf.Clamp01(t);
            position = _restPosition + moveWorld * t + extraOffset;
            rotation = _restRotation * Quaternion.Euler(rotateEuler * t);
        }
    }

    /// <summary>The spawn points one colour's marbles are seated on in a stage's pens.</summary>
    [Serializable]
    public sealed class LavaTeamPen
    {
        [Tooltip("Pen spawn points for this colour. Survivors beyond the number of points stack upward.")]
        public Transform[] spawnPoints;
    }

    /// <summary>A multiplier gate across a track.</summary>
    [Serializable]
    public sealed class LavaGate
    {
        [Tooltip("Trigger volume spanning the track lane. A marble's first entry multiplies it.")]
        public TriggerZone3D zone;

        [Tooltip("x1 = a safe lane (no clones), x2, x3, x5...")]
        [Min(1)] public int multiplier = 2;

        [Tooltip("Optional: where the presenter flashes the arch / pops the decal. Not read by the simulation.")]
        public Transform flashAnchor;
    }

    /// <summary>Scene wiring for one stage of track (stages 1-3).</summary>
    [Serializable]
    public sealed class LavaStageRig
    {
        public string name = "Stage";

        [Tooltip("Centreline of this stage, start (pens) to basin. Lava-front distance and marble progress are measured on it.")]
        public TrackPath path;

        [Tooltip("One entry per colour (length 4): that colour's pen spawn points for this stage.")]
        public LavaTeamPen[] pens = new LavaTeamPen[LavaRunConfig.TeamCount];

        [Tooltip("Kinematic pen gate. t = 1 is open. Null = nothing to open.")]
        public LavaMover penGate = new LavaMover();

        [Tooltip("Multiplier gates in path order. Index = gate index in events (max 32).")]
        public LavaGate[] gates = new LavaGate[0];

        [Tooltip("Path distance at which the basin door closes (the lava front stops here).")]
        public float basinEntryDistance;

        [Tooltip("Trigger volume of the safe area. Must be convex (box/sphere/capsule/convex mesh): occupancy uses ClosestPoint. " +
                 "For the bridge stage, the island landing zone.")]
        public TriggerZone3D basinZone;

        [Tooltip("Basin door. t = 1 is shut. Ignored when endsOnIsland.")]
        public LavaMover door = new LavaMover();

        [Tooltip("Basin floor. t = 1 is open (survivors fall). Ignored when endsOnIsland.")]
        public LavaMover trapdoor = new LavaMover();

        [Tooltip("Stage 3: the bridge leads straight onto the island. No door or trapdoor; survivors stay put and the island phase starts.")]
        public bool endsOnIsland;

        [Tooltip("A marble below this Y is dead (FellIn). Set just under this stage's lowest track so a marble that falls off " +
                 "one tier does not land on the next. -1000 = rely on the global killPlaneY.")]
        public float killPlaneY = -1000f;

        [Tooltip("Pinwheels / spinning obstacles on this stage. Phase seeded at Initialize, stepped every tick in every phase.")]
        public KinematicSpinner[] spinners = new KinematicSpinner[0];

        [Tooltip("Track width (metres) by path distance, baked by the builder. Read by the presenter for the lava ribbon; not used by the sim.")]
        public AnimationCurve widthByDistance = new AnimationCurve();
    }

    /// <summary>Scene wiring for the finale island.</summary>
    [Serializable]
    public sealed class LavaIslandRig
    {
        [Tooltip("Concentric ring platforms, outermost first. Each needs a collider; give it a kinematic Rigidbody so it carries marbles. " +
                 "The innermost never sinks.")]
        public Transform[] rings = new Transform[0];

        [Tooltip("Sweeper arm. Its degreesPerSecond is overwritten at runtime (the sim integrates the angle itself so the speed can ramp).")]
        public KinematicSpinner sweeper;

        public Transform centre;
    }

    /// <summary>
    /// Scene wiring for Lava Run, filled in by the scene builder. The simulation and presenter only read it.
    /// </summary>
    /// <remarks>
    /// Marble colour: one inactive template per colour (<see cref="marbleTemplates"/>), cloned into a per-colour pool prewarmed to
    /// config.poolPrewarmPerTeam (the pool grows past that unless config.maxLivePerTeam sets a hard cap). The scene builder bakes the colour into each template's material, so the sim never
    /// touches materials and a clone is already the right colour the moment it pops out of a gate.
    /// </remarks>
    public sealed class LavaRunRig : MonoBehaviour
    {
        [Tooltip("Stages 1-3 (length 3). Stage 3 (index 2) normally has endsOnIsland = true.")]
        public LavaStageRig[] stages = new LavaStageRig[LavaRunConfig.StageCount];

        public LavaIslandRig island = new LavaIslandRig();

        [Tooltip("Global kill plane: a marble below this Y is dead (FellIn).")]
        public float killPlaneY = -50f;

        [Tooltip("Trigger spanning the lava lake surface. A marble entering it is dead (FellIn).")]
        public TriggerZone3D lavaLakeZone;

        [Tooltip("Length 4, one per colour. Inactive: Rigidbody, SphereCollider, glass in the team colour. Cloned into the pools.")]
        public GameObject[] marbleTemplates = new GameObject[LavaRunConfig.TeamCount];

        [Tooltip("Parent for the pooled marbles. Falls back to this transform when empty.")]
        public Transform poolParent;
    }
}
