using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Picks and varies the backdrop at the start of each run, so the same scene doesn't look identical
    /// video after video. The variance comes from the run's seed, so a take re-rendered from its logged
    /// seed gets the same backdrop.
    /// </summary>
    /// <remarks>
    /// Reads the runner's seed and never writes to the run. It draws from its own salted generator, so
    /// the simulation's draw sequence is untouched. Background and motes only: the arena, ball and HUD
    /// keep their tuned colours.
    /// </remarks>
    public sealed class BackdropStyler : MonoBehaviour
    {
        [SerializeField] SimulationRunner _runner;
        [SerializeField] Camera _camera;
        [SerializeField] AmbientDriftField _field;

        [Header("Themes")]
        [Tooltip("Looks this scene may use.")]
        public BackdropTheme[] themes;

        [Tooltip("Pick a theme from the seed each run. Off always uses Fixed Theme.")]
        public bool pickThemeFromSeed = true;

        [Min(0)] public int fixedTheme;

        [Header("Variance on top of the theme (0 = none)")]
        [Tooltip("Shift the background and mote hue by up to this many degrees either way.")]
        [Range(0f, 60f)] public float hueJitterDegrees = 12f;

        [Tooltip("Turn the theme's current by up to this many degrees either way.")]
        [Range(0f, 180f)] public float flowJitterDegrees = 25f;

        [Tooltip("Scale mote count by a random factor in this range.")]
        public Vector2 countScale = new Vector2(0.8f, 1.25f);

        [Tooltip("Scale drift and current speed by a random factor in this range.")]
        public Vector2 speedScale = new Vector2(0.8f, 1.3f);

        /// <summary>What this run got, for the console line and the retro.</summary>
        public string Description { get; private set; }

        void Start()
        {
            if (themes == null || themes.Length == 0 || _field == null)
            {
                return;
            }

            // Start runs after the runner's Awake, so this is the seed actually drawn for this Play.
            int seed = _runner != null ? _runner.seed : 0;
            var random = new System.Random(unchecked(seed * 31 + 0x0BAC));

            int index = pickThemeFromSeed
                ? random.Next(themes.Length)
                : Mathf.Clamp(fixedTheme, 0, themes.Length - 1);
            BackdropTheme theme = themes[index];
            if (theme == null)
            {
                return;
            }

            // Fixed draw order (hue, turn, count, speed), each drawn even when its range is zero, so
            // changing one range never reshuffles the others.
            float hue = Range(random, -hueJitterDegrees, hueJitterDegrees);
            float turn = Range(random, -flowJitterDegrees, flowJitterDegrees);
            float count = Range(random, countScale.x, countScale.y);
            float speed = Range(random, speedScale.x, speedScale.y);

            if (_camera != null)
            {
                _camera.backgroundColor = ShiftHue(theme.background, hue);
            }

            _field.tint = ShiftHue(theme.moteTint, hue);
            _field.maxAlpha = theme.moteAlpha;
            _field.sizeRange = theme.moteSize;
            _field.particleCount = Mathf.Clamp(Mathf.RoundToInt(theme.moteCount * count), 4, 400);
            _field.driftSpeed = Mathf.Clamp(theme.driftSpeed * speed, 0.01f, 1f);
            _field.flow = Rotate(theme.flow, turn) * speed;
            _field.spinSpeed = theme.spinSpeed;
            _field.randomSeed = seed;

            Texture texture = theme.moteTexture;
            if (texture == null)
            {
                var renderer = _field.GetComponent<ParticleSystemRenderer>();
                texture = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
            }

            _field.Apply(texture);

            Description = $"{theme.displayName} (hue {hue:+0;-0;0}°, {_field.particleCount} motes, speed x{speed:0.00})";
            Debug.Log($"[Backdrop] {Description}", this);
        }

        static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static Color ShiftHue(Color color, float degrees)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            Color shifted = Color.HSVToRGB(Mathf.Repeat(h + degrees / 360f, 1f), s, v);
            shifted.a = color.a;
            return shifted;
        }

        static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        /// <summary>Assigned by a scene builder.</summary>
        public void Bind(SimulationRunner runner, Camera camera, AmbientDriftField field)
        {
            _runner = runner;
            _camera = camera;
            _field = field;
        }
    }
}
