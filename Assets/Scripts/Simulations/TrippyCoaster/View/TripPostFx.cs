using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// Drives a runtime global URP Volume from the ride: bloom carries the glow, chromatic aberration and lens warp scale with the world's
    /// intensity and the cart's speed, and the whole image drifts slowly around the colour wheel. Every rate is slow and there is no
    /// full-frame flash (hue shift peaks near 5 degrees/second, lens warp moves well under 1 Hz).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class TripPostFx : MonoBehaviour
    {
        public TrippyCoasterHost host;

        VolumeProfile _profile;
        Bloom _bloom;
        ChromaticAberration _chroma;
        LensDistortion _lens;
        ColorAdjustments _color;
        Vignette _vignette;
        float _intensity;

        void Start()
        {
            var go = new GameObject("Trip Post Volume (runtime)");
            go.transform.SetParent(transform, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = _profile;

            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.85f);
            _bloom.scatter.Override(0.7f);
            _bloom.highQualityFiltering.Override(true);
            _chroma = _profile.Add<ChromaticAberration>(true);
            _lens = _profile.Add<LensDistortion>(true);
            _color = _profile.Add<ColorAdjustments>(true);
            _color.saturation.Override(25f);
            _color.contrast.Override(12f);
            _vignette = _profile.Add<Vignette>(true);
            _vignette.smoothness.Override(0.8f);
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        void LateUpdate()
        {
            if (host == null || !host.Ready || _profile == null) return;
            ITripState s = host.State;
            TrippyCoasterConfig cfg = s.Config;
            float target = cfg.worlds[s.WorldIndex].intensity;
            _intensity = Mathf.Lerp(_intensity, target, 1f - Mathf.Exp(-0.6f * Time.deltaTime));

            float t = s.SimTime;
            float speed01 = Mathf.Clamp01(s.Speed / Mathf.Max(1f, cfg.maxSpeed));
            float k = _intensity;

            _bloom.intensity.Override(Mathf.Lerp(1.2f, 3.2f, k));
            _chroma.intensity.Override(Mathf.Clamp01(0.12f + 0.55f * k * (0.4f + 0.6f * speed01)));
            _lens.intensity.Override(Mathf.Sin(t * 0.35f) * 0.28f * k - 0.05f * k);
            _lens.scale.Override(1f);
            _color.hueShift.Override(Mathf.Sin(t * 0.1f) * 45f * k);
            _vignette.intensity.Override(0.2f + 0.25f * k);
        }
    }
}
