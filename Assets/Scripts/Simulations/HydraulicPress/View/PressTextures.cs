using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// Runtime-generated textures for the ball batch and the pop rings. No assets, so there is nothing to go
    /// stale: a shaded disc (darker rim, soft highlight, grey base so the vertex colour carries the hue) and a
    /// thin ring.
    /// </summary>
    public static class PressTextures
    {
        public static Texture2D CreateBall(int size = 128)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "PressBall",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half;
                    float v = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01((1f - r) * half / 1.5f);
                    float rim = Mathf.SmoothStep(0.55f, 1f, r);
                    float shade = Mathf.Lerp(0.95f, 0.55f, rim);
                    float hu = u + 0.32f;
                    float hv = v - 0.36f;
                    shade += 0.18f * Mathf.Exp(-(hu * hu + hv * hv) / 0.07f);
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
                    pixels[y * size + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        public static Texture2D CreateRing(int size = 128)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "PressRing",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - half) / half;
                    float v = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.12f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(band * band * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>A textured-quad material that multiplies vertex colour; present in every pipeline (as AmbientDriftField uses it).</summary>
        public static Material CreateMaterial(Texture2D texture, string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            return new Material(shader) { name = name, mainTexture = texture };
        }
    }
}
