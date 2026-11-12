using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Adds the whole viewing rig to a scene: ride camera, fear post FX, procedural audio (with the listener and limiter) on the
    /// camera, the HUD canvas, and the presenter that binds everything once the host is ready. Idempotent: components that already
    /// exist are reused. Called by the scene builder (edit mode) or at runtime; it uses no editor APIs.
    /// </summary>
    public static class CoasterRigBuilder
    {
        /// <summary>
        /// Builds the rig. <paramref name="root"/> is the scene root every <see cref="ActSetBase"/> lives under (the HUD and the
        /// presenter are parented to it), <paramref name="cam"/> is the scene camera, <paramref name="host"/> the kernel host.
        /// Returns the presenter's GameObject.
        /// </summary>
        public static GameObject Build(GameObject root, Camera cam, HorrorCoasterHost host)
        {
            GameObject camGo = cam.gameObject;

            cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);
            RideCameraRig rig = GetOrAdd<RideCameraRig>(camGo);
            FearPostFx fx = GetOrAdd<FearPostFx>(camGo);
            CoasterAudio audio = GetOrAdd<CoasterAudio>(camGo);
            GetOrAdd<CoasterMasterLimiter>(camGo);
            GetOrAdd<ScareSlowMo>(camGo);
            if (camGo.GetComponent<AudioListener>() == null)
            {
                camGo.AddComponent<AudioListener>();
            }

            CoasterHud hud = root.GetComponentInChildren<CoasterHud>(true);
            if (hud == null)
            {
                hud = CoasterHud.Build(root.transform);
            }

            HorrorCoasterPresenter presenter = root.GetComponentInChildren<HorrorCoasterPresenter>(true);
            GameObject presenterGo;
            if (presenter == null)
            {
                presenterGo = new GameObject("Coaster Presenter");
                presenterGo.transform.SetParent(root.transform, false);
                presenter = presenterGo.AddComponent<HorrorCoasterPresenter>();
            }
            else
            {
                presenterGo = presenter.gameObject;
            }

            presenter.Wire(host, root.transform, rig, fx, audio, hud);
            return presenterGo;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
