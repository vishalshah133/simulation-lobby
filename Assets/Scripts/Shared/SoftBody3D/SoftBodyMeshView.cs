using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>Anything that owns a live <see cref="SoftBodySolver"/> — usually a format's simulation.</summary>
    public interface ISoftBodySource
    {
        /// <summary>The current solver. May be replaced between takes; may be null before the first.</summary>
        SoftBodySolver Solver { get; }
    }

    /// <summary>
    /// Draws a soft body: copies its particle positions into a mesh every frame and recalculates the
    /// normals, so highlights and reflections slide across the surface as it deforms. Frozen normals
    /// on a moving surface are what make a soft body look like shrink-wrap.
    /// </summary>
    /// <remarks>
    /// Read-only over the solver, and runs in <c>LateUpdate</c> after the fixed step. The mesh is in
    /// world space, so keep this object's transform at identity.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SoftBodyMeshView : MonoBehaviour
    {
        [SerializeField] MonoBehaviour _source;

        ISoftBodySource _softBodySource;
        SoftBodySolver _boundSolver;
        Mesh _mesh;

        public void Bind(MonoBehaviour source)
        {
            _source = source;
            _softBodySource = source as ISoftBodySource;
        }

        void Awake()
        {
            _softBodySource = _source as ISoftBodySource;
            _mesh = new Mesh { name = "SoftBody (live)" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        void LateUpdate()
        {
            SoftBodySolver solver = _softBodySource?.Solver;
            if (solver == null)
            {
                return;
            }

            if (!ReferenceEquals(solver, _boundSolver))
            {
                // New take: new solver. Topology can differ between configs, so rebuild from scratch.
                _boundSolver = solver;
                _mesh.Clear();
                _mesh.vertices = solver.Positions;
                _mesh.triangles = solver.Triangles;
            }
            else
            {
                _mesh.vertices = solver.Positions;
            }

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
