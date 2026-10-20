using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// One world's pads: one mesh per hit, placed under the marble's contact point and oriented to the pad
    /// normal. A pad is invisible until <c>padLeadSeconds</c> before its hit, fades and grows in (anticipation),
    /// flashes HDR on contact, then lingers lit. Reads the schedule only. All colour goes through one shared
    /// <see cref="MaterialPropertyBlock"/>; nothing is instanced per pad.
    /// </summary>
    internal sealed class WorldPads
    {
        sealed class Pad
        {
            public Transform transform;
            public MeshRenderer renderer;
            public float hitTime;
            public float y;
            public float radius;
            public bool glass;
            public Color hue;
            public float appliedScale = -1f;
            public float appliedBrightness = -1f;
            public float appliedAlpha = -1f;
        }

        // Emission multipliers: flash on contact, resting glow (35% of the flash), and the faint pre-hit glow.
        const float FlashPeak = 3.5f;
        const float Linger = FlashPeak * 0.35f;
        const float PreHit = 1.1f;
        const float FlashSeconds = 0.6f;
        const float GlassAlpha = 0.58f;

        static MaterialPropertyBlock _block;
        // Lazy: Unity forbids creating one while a MonoBehaviour is being constructed (static init runs then).
        static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        readonly List<Pad> _pads;
        readonly GameObject _root;
        int _visible;

        public WorldPads(Transform parent, FreefallWorld world, IReadOnlyList<FreefallHit> hits, int firstHit, int hitCount,
                         FreefallConfig config, FreefallMaterials materials, System.Random rng)
        {
            _root = new GameObject("Pads");
            _root.transform.SetParent(parent, false);
            _pads = new List<Pad>(hitCount);

            Mesh mesh = FreefallMeshes.Pad(world.padShape);
            bool glass = world.padShape == PadShape.Disc || world.padShape == PadShape.Petal ||
                         world.padShape == PadShape.Lily || world.padShape == PadShape.Crystal;
            Material material = glass ? materials.padGlass : materials.padOpaque;
            float radius = world.padRadius * (world.padShape == PadShape.Lily ? 1.15f : 1f);

            for (int i = 0; i < hitCount; i++)
            {
                FreefallHit hit = hits[firstHit + i];
                var go = new GameObject("Pad");
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                // Marble centre at contact: the pad's top face sits one marble radius under it along the normal.
                Vector3 normal = hit.normal.sqrMagnitude > 1e-6f ? hit.normal.normalized : Vector3.up;
                Vector3 centre = hit.position - normal * (config.marbleRadius + FreefallMeshes.PadHalfThickness * radius);

                // Long axis (petal) lies along the tangent of the spiral; the rest spin by a seeded angle.
                Vector3 radial = new Vector3(hit.position.x, 0f, hit.position.z);
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                if (tangent.sqrMagnitude < 1e-4f)
                {
                    tangent = Vector3.forward;
                }

                Vector3 forward = Vector3.ProjectOnPlane(tangent, normal);
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
                }

                Quaternion rotation = Quaternion.LookRotation(forward.normalized, normal);
                if (world.padShape != PadShape.Petal)
                {
                    rotation = Quaternion.AngleAxis((float)rng.NextDouble() * 360f, normal) * rotation;
                }

                go.transform.SetPositionAndRotation(centre, rotation);
                go.transform.localScale = Vector3.zero;

                _pads.Add(new Pad
                {
                    transform = go.transform,
                    renderer = renderer,
                    hitTime = hit.time,
                    y = hit.position.y,
                    radius = radius,
                    glass = glass,
                    hue = FreefallColors.Hue(world, hit.pitch01)
                });
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        /// <param name="heroY">Height of the hero marble, used to dim passed pads that hang above the camera's line of sight.</param>
        /// <param name="reveal">0 on the descent, 1 during the pull-back, when the whole sculpture shows at full glow.</param>
        public void Update(float time, float heroY, float reveal, float leadSeconds)
        {
            float lead = Mathf.Max(0.05f, leadSeconds);
            while (_visible < _pads.Count && _pads[_visible].hitTime - lead <= time)
            {
                _pads[_visible].renderer.enabled = true;
                _visible++;
            }

            for (int i = 0; i < _visible; i++)
            {
                Pad pad = _pads[i];
                float since = time - pad.hitTime;
                float u = Mathf.Clamp01((time - (pad.hitTime - lead)) / lead);
                float e = FreefallColors.EaseOut(u);

                float brightness;
                float fade = 1f;
                if (since < 0f)
                {
                    brightness = PreHit * e;
                }
                else
                {
                    float k = Mathf.Clamp01(1f - since / FlashSeconds);
                    brightness = Mathf.Lerp(Linger, FlashPeak, k * k);

                    // Passed pads above the hero hang between camera and marble: dim them on the descent only.
                    float above = FreefallColors.Smooth(0.3f, 1.4f, pad.y - heroY);
                    fade = Mathf.Lerp(Mathf.Lerp(1f, 0.18f, above), 1f, reveal);
                    brightness *= fade;
                }

                float alpha = pad.glass ? e * GlassAlpha * Mathf.Lerp(0.4f, 1f, fade) : 1f;
                float scale = Mathf.Lerp(0.25f, 1f, e);

                if (Mathf.Abs(scale - pad.appliedScale) < 0.002f &&
                    Mathf.Abs(brightness - pad.appliedBrightness) < 0.004f &&
                    Mathf.Abs(alpha - pad.appliedAlpha) < 0.002f)
                {
                    continue;
                }

                pad.appliedScale = scale;
                pad.appliedBrightness = brightness;
                pad.appliedAlpha = alpha;

                pad.transform.localScale = Vector3.one * (pad.radius * scale);

                // The flash stays mostly the note's hue: only the marble is white, so it never vanishes into a flash.
                float hot = since >= 0f ? Mathf.Clamp01(1f - since / FlashSeconds) : 0f;
                Color emission = Color.Lerp(pad.hue, Color.white, 0.25f * hot * hot) * brightness;
                emission.a = 1f;
                Color body = pad.glass
                    ? new Color(pad.hue.r * 0.55f, pad.hue.g * 0.55f, pad.hue.b * 0.55f, alpha)
                    : new Color(pad.hue.r * 0.22f, pad.hue.g * 0.22f, pad.hue.b * 0.22f, 1f);

                Block.Clear();
                Block.SetColor(FreefallColors.BaseColor, body);
                Block.SetColor(FreefallColors.EmissionColor, emission);
                pad.renderer.SetPropertyBlock(Block);
            }
        }
    }
}
