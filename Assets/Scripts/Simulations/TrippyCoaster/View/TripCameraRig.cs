using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// First-person ride camera. The camera sits on the cart pose (the simulation's pose is already smooth and the wobble is a handful
    /// of slow sines, so no extra filtering is needed). Adds a slow dizzy roll and a speed-driven FOV. Reads, never writes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class TripCameraRig : MonoBehaviour
    {
        public TrippyCoasterHost host;
        public Vector3 eyeOffset = new Vector3(0f, 1.1f, 0.2f);

        Camera _cam;
        float _fovSmooth;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _fovSmooth = _cam.fieldOfView;
        }

        void LateUpdate()
        {
            if (host == null || !host.Ready) return;
            ITripState s = host.State;
            TrippyCoasterConfig cfg = s.Config;
            TripWorldDef w = cfg.worlds[s.WorldIndex];

            float t = s.SimTime;
            // slow dizzy roll: under 0.12 Hz, a few degrees at most, scaled by the world's intensity
            float roll = Mathf.Sin(t * 0.7f) * 2.5f * w.intensity;
            Quaternion rot = Quaternion.LookRotation(s.CartForward, s.CartUp) * Quaternion.Euler(0f, 0f, roll);
            Vector3 right = Vector3.Cross(s.CartUp, s.CartForward);
            transform.SetPositionAndRotation(
                s.CartPosition + s.CartUp * eyeOffset.y + right * eyeOffset.x + s.CartForward * eyeOffset.z, rot);

            float speed01 = Mathf.Clamp01(s.Speed / Mathf.Max(1f, cfg.maxSpeed));
            float breathe = Mathf.Sin(t * 0.9f) * 2.5f * w.intensity;
            float target = cfg.baseFov + cfg.speedFov * speed01 + breathe;
            _fovSmooth = Mathf.Lerp(_fovSmooth, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            _cam.fieldOfView = _fovSmooth;
        }
    }
}
