using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The seven glass tubes. Pure lookup for the simulation (which tube holds this point?) plus a visual-only
    /// winner glow on the fill-line ring. Built by <c>TubeRackBuilder</c>; the root never moves.
    /// </summary>
    public sealed class TubeRack : MachineStation, ITubeRack
    {
        [Header("Tubes (root-local; the root sits on the floor)")]
        public Vector3[] tubeLocalCentres;
        public float innerRadius = 0.6f;
        public float[] floorHeights;
        public float[] fillHeights;
        public float[] rimHeights;

        [Header("Fill line visuals")]
        public Renderer[] fillRings;
        public Material glowMaterial;
        public Color[] tubeColours;

        int _winner = -1;
        MaterialPropertyBlock _block;
        bool _cached;
        Vector3[] _world;
        Bounds _worldBounds;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int TubeCount => tubeLocalCentres != null ? tubeLocalCentres.Length : 0;

        void EnsureCache()
        {
            if (_cached) return;
            int n = TubeCount;
            _world = new Vector3[n];
            for (int i = 0; i < n; i++) _world[i] = transform.TransformPoint(tubeLocalCentres[i]);
            if (n > 0)
            {
                _worldBounds = new Bounds(_world[0], Vector3.zero);
                for (int i = 0; i < n; i++)
                {
                    _worldBounds.Encapsulate(new Vector3(_world[i].x - innerRadius, floorHeights[i], _world[i].z - innerRadius));
                    _worldBounds.Encapsulate(new Vector3(_world[i].x + innerRadius, rimHeights[i], _world[i].z + innerRadius));
                }
            }

            _cached = true;
        }

        public int TubeIndexAt(Vector3 p)
        {
            EnsureCache();
            if (!_worldBounds.Contains(p)) return -1;
            float r2 = innerRadius * innerRadius;
            for (int i = 0; i < _world.Length; i++)
            {
                float dx = p.x - _world[i].x, dz = p.z - _world[i].z;
                if (dx * dx + dz * dz < r2 && p.y > floorHeights[i] && p.y < rimHeights[i]) return i;
            }

            return -1;
        }

        public Vector3 TubeTop(int tube)
        {
            EnsureCache();
            return new Vector3(_world[tube].x, rimHeights[tube], _world[tube].z);
        }

        public Vector3 TubeFillLine(int tube)
        {
            EnsureCache();
            return new Vector3(_world[tube].x, fillHeights[tube], _world[tube].z);
        }

        public void MarkWinner(int tube)
        {
            if (tube < 0 || tube >= TubeCount) return;
            _winner = tube;
            if (fillRings != null && tube < fillRings.Length && fillRings[tube] != null && glowMaterial != null)
                fillRings[tube].sharedMaterial = glowMaterial;
        }

        public override void Step(float simTime, float dt) { }

        // Visual only: a gentle pulse on the winner's ring.
        void Update()
        {
            if (_winner < 0 || fillRings == null || _winner >= fillRings.Length || fillRings[_winner] == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            Color c = tubeColours != null && _winner < tubeColours.Length ? tubeColours[_winner] : Color.white;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            _block.SetColor(BaseColorId, c);
            _block.SetColor(EmissionColorId, c * (1.5f + pulse * 2.5f));
            fillRings[_winner].SetPropertyBlock(_block);
        }
    }
}
