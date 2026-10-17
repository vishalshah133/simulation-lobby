using SimulationLobby.Shared;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>
    /// Scene wiring for one team: its Plinko board, its slot triggers, its cannon. Filled in by the scene
    /// builder; the simulation and presenter only read these references.
    /// </summary>
    public sealed class TerritoryTeamRig : MonoBehaviour
    {
        [Tooltip("Inactive: Rigidbody, SphereCollider, CollisionDetector3D, team glass. Cloned into the pool.")]
        public GameObject ballTemplate;

        [Tooltip("Parent for the pooled board balls. Falls back to this transform when empty.")]
        public Transform ballParent;

        [Tooltip("Top-centre of the board. Its right axis runs across the board.")]
        public Transform dropPoint;

        [Min(0f)] public float dropHalfWidth = 1.6f;

        [Tooltip("Left to right as seen by the camera. Length must equal config.boardSlots.Length.")]
        public TriggerZone3D[] slots;

        public Transform[] slotLabelAnchors;

        [Tooltip("Where the big bank number floats.")]
        public Transform counterAnchor;

        [Tooltip("World-space TMP decal on the board: the bank counter.")]
        public TMP_Text counterText;

        [Tooltip("World-space TMP decal: \"FIRING n\".")]
        public TMP_Text firingText;

        [Tooltip("World-space TMP decals on the slot chips, left to right. Length should equal config.boardSlots.Length.")]
        public TMP_Text[] slotTexts;

        [Tooltip("Yaw pivot of the cannon (visual only). Its forward is the barrel direction.")]
        public Transform cannonPivot;

        public Transform cannonMuzzle;

        [Tooltip("Timed diagonal plank over the SPIN lane. The simulation toggles its collider; null when there is no partition.")]
        public Collider laneGate;

        [Tooltip("Visual of the lane gate. The presenter hides and pops it; never the collider.")]
        public Renderer laneGateRenderer;

        // ---- terr-002 (all optional; terr-001 scenes leave them empty) ----

        [Tooltip("The cannon's root (base disc + pivot). The presenter animates the knock-out and repair under it.")]
        public Transform cannonRoot;

        [Tooltip("Barrel, cap and muzzle renderers: scorched by the presenter while the cannon is down (property block).")]
        public Renderer[] cannonTintRenderers;

        [Tooltip("Upright world-space TMP above the cannon, facing the camera: the down countdown (\"10\"). Empty text when up.")]
        public TMP_Text downText;

        [Tooltip("World-space TMP decal: the team name painted on the board above the bank number. Null when the theme has none.")]
        public TMP_Text nameText;

        [Tooltip("The flag on its pole by the cannon. Null when the theme has no flags.")]
        public Transform flagRoot;

        [Tooltip("The base plate disc and rim (the knock-out target). Scaled in X/Z by the presenter when the showdown grows the plates.")]
        public Transform basePlate;
    }
}
