using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Presentation only: slowly scrolls the water's ripple normal map so the surface looks alive. Writes a MaterialPropertyBlock
    /// on its own renderer in <c>LateUpdate</c>; it reads nothing from the simulation and touches no physics, so determinism is intact.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class NatureWaterScroller : MonoBehaviour
    {
        [Tooltip("Ripple tiles across the plane (the plane is ~900 m wide, so 180 = a 5 m ripple tile).")]
        public float tiling = 180f;

        [Tooltip("Scroll speed in tiles per second, per axis.")]
        public Vector2 speed = new Vector2(0.012f, 0.007f);

        Renderer _renderer;
        MaterialPropertyBlock _block;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            float t = Time.time;
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapST, new Vector4(tiling, tiling, Mathf.Repeat(t * speed.x, 1f), Mathf.Repeat(t * speed.y, 1f)));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
