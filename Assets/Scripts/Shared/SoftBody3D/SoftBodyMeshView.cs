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
    /// <para>
    /// <b>Interpolated</b>, like a Rigidbody set to Interpolate: the drawn pose blends from the
    /// previous fixed step to the latest by how far the frame is between them. Without it the mesh
    /// only moves at the physics rate (60 Hz), so on a faster display the same pose repeats for
    /// several frames and the motion judders — part of the "not smooth" first-playtest note. It
    /// costs one physics step of latency and never touches the simulation.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SoftBodyMeshView : MonoBehaviour
    {
        [SerializeField] MonoBehaviour _source;

        [Tooltip("Blend between physics steps for smooth motion at any frame rate.")]
        public bool interpolate = true;

        ISoftBodySource _softBodySource;
        SoftBodySolver _boundSolver;
        Mesh _mesh;
        Vector3[] _blended;
        int _seenSteps = -1;
        float _lastStepFixedTime = -1f;

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

            Vector3[] current = solver.Positions;
            if (!ReferenceEquals(solver, _boundSolver))
            {
                // New take: new solver. Topology can differ between configs, so rebuild from scratch.
                _boundSolver = solver;
                _blended = new Vector3[current.Length];
                _mesh.Clear();
                _mesh.vertices = current;
                _mesh.triangles = solver.Triangles;
            }

            // Only blend while the solver is actually stepping: during a hold (lead-in, end hold) the
            // last two poses are stale, and blending them every frame would make the body flicker.
            // "Stepping" = it advanced during the most recent fixed update.
            if (solver.StepCount != _seenSteps)
            {
                _seenSteps = solver.StepCount;
                _lastStepFixedTime = Time.fixedTime;
            }

            bool stepping = Mathf.Approximately(_lastStepFixedTime, Time.fixedTime);
            if (interpolate && stepping && solver.StepCount > 0 && Time.fixedDeltaTime > 0f)
            {
                // Time.time runs ahead of the last fixed step by up to one step; that fraction is how
                // far to blend from the previous pose to the latest.
                float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
                Vector3[] previous = solver.PreviousPositions;
                for (int i = 0; i < current.Length; i++)
                {
                    _blended[i] = Vector3.LerpUnclamped(previous[i], current[i], alpha);
                }

                _mesh.vertices = _blended;
            }
            else
            {
                _mesh.vertices = current;
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
