using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Glue between the kernel host and every view: waits until <see cref="HorrorCoasterHost.Ready"/>, then hands the
    /// <see cref="ICoasterState"/> to each <see cref="ActSetBase"/> under the scene root and to the rig components (camera,
    /// post FX, audio, HUD). Rebinds if the host rebuilds its state (a second Play with the same scene, a re-seed).
    /// Read-only: it only calls <c>Bind</c>, never touches simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HorrorCoasterPresenter : MonoBehaviour
    {
        [Header("Wiring (set by CoasterRigBuilder)")]
        [SerializeField] HorrorCoasterHost _host;
        [SerializeField] Transform _sceneRoot;
        [SerializeField] RideCameraRig _camera;
        [SerializeField] FearPostFx _postFx;
        [SerializeField] CoasterAudio _audio;
        [SerializeField] CoasterHud _hud;

        ICoasterState _bound;

        public int ActSetsBound { get; private set; }

        public void Wire(HorrorCoasterHost host, Transform sceneRoot, RideCameraRig cameraRig, FearPostFx postFx, CoasterAudio audio, CoasterHud hud)
        {
            _host = host;
            _sceneRoot = sceneRoot;
            _camera = cameraRig;
            _postFx = postFx;
            _audio = audio;
            _hud = hud;
        }

        void Update()
        {
            if (_host == null)
            {
                return;
            }

            if (!_host.Ready)
            {
                _bound = null;
                return;
            }

            ICoasterState state = _host.State;
            if (state == null || ReferenceEquals(state, _bound))
            {
                return;
            }

            Bind(state);
        }

        void Bind(ICoasterState state)
        {
            _bound = state;

            CoasterLookTarget.Clear();

            ActSetBase[] sets = FindObjectsByType<ActSetBase>(FindObjectsInactive.Include);
            int count = 0;
            for (int i = 0; i < sets.Length; i++)
            {
                if (_sceneRoot != null && !sets[i].transform.IsChildOf(_sceneRoot))
                {
                    continue;
                }

                sets[i].Bind(state);
                count++;
            }

            ActSetsBound = count;

            if (_camera != null)
            {
                _camera.Bind(state);
            }

            if (_postFx != null)
            {
                _postFx.Bind(state);
            }

            if (_audio != null)
            {
                _audio.Bind(state);
            }

            if (_hud != null)
            {
                _hud.Bind(state);
            }
        }
    }
}
