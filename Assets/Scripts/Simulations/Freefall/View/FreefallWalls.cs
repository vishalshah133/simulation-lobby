using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// The decoration around one world's well, one subclass per <see cref="WallStyle"/>. Built once when the world
    /// is pre-built, ticked every frame with the display time. Every random draw comes from the
    /// <see cref="System.Random"/> the presenter seeds from the run seed and the world index, so a seed redraws the
    /// same walls. Read only: walls react to hits, they never touch the simulation.
    /// </summary>
    internal abstract class WallView
    {
        protected readonly GameObject Root;
        protected readonly FreefallWorld World;
        protected readonly FreefallWorldSpan Span;
        protected readonly FreefallMaterials Mats;
        protected readonly System.Random Rng;
        protected readonly float Radius;
        protected readonly float Y0;
        protected readonly float Y1;

        protected static MaterialPropertyBlock _block;
        // Lazy: Unity forbids creating one while a MonoBehaviour is being constructed (static init runs then).
        protected static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        protected WallView(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
        {
            Root = new GameObject(name);
            Root.transform.SetParent(parent, false);
            World = world;
            Span = span;
            Mats = mats;
            Rng = rng;
            Radius = world.wellRadius + 0.6f;
            Y0 = span.yBottom - 2f;
            Y1 = span.yTop + 2f;
        }

        /// <summary>Per-frame update. <paramref name="time"/> is the display time; <paramref name="hero"/> the hero marble.</summary>
        public abstract void Tick(float time, float dt, Vector3 hero);

        /// <summary>A note just landed. <paramref name="hue"/> is the pad's colour.</summary>
        public virtual void OnHit(in FreefallHit hit, Color hue, float time) { }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root);
            }
        }

        protected float Rand(float min, float max)
        {
            return min + (float)Rng.NextDouble() * (max - min);
        }

        protected MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>World progress 0..1 down the world's own band, for a height.</summary>
        protected static void SetBaseColor(Renderer renderer, Color colour)
        {
            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, colour);
            renderer.SetPropertyBlock(Block);
        }

        public static WallView Create(Transform parent, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
        {
            string name = "Wall " + world.wallStyle;
            switch (world.wallStyle)
            {
                case WallStyle.RingStack: return new RingStackWall(parent, name, world, span, mats, rng);
                case WallStyle.Aurora: return new AuroraWall(parent, name, world, span, mats, rng);
                case WallStyle.Mist: return new MistWall(parent, name, world, span, mats, rng);
                case WallStyle.CrystalShards: return new CrystalWall(parent, name, world, span, mats, rng);
                case WallStyle.Mandala: return new MandalaWall(parent, name, world, span, mats, rng);
                case WallStyle.Starfield: return new StarfieldWall(parent, name, world, span, mats, rng);
                case WallStyle.None: return new EmptyWall(parent, name, world, span, mats, rng);
                default: return new VortexWall(parent, name, world, span, mats, rng);
            }
        }
    }

    // ------------------------------------------------------------------ None

    /// <summary>No wall decoration: the sky, pads and marble carry the world on their own.</summary>
    internal sealed class EmptyWall : WallView
    {
        public EmptyWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng) { }

        public override void Tick(float time, float dt, Vector3 hero) { }
    }

    // ------------------------------------------------------------------ RingStack

    /// <summary>Thin emissive rings every half metre. A hit lights the nearest ring and the glow runs down the stack.</summary>
    internal sealed class RingStackWall : WallView
    {
        struct Pulse
        {
            public float y;
            public float t0;
            public Color colour;
        }

        const float PulseSeconds = 1.6f;
        const float PulseSpeed = 11f;

        readonly MeshRenderer[] _rings;
        readonly float[] _ringY;
        readonly float[] _applied;
        readonly Color _baseColour;
        readonly List<Pulse> _pulses = new List<Pulse>(16);

        public RingStackWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            float height = Y1 - Y0;
            int count = Mathf.Clamp(Mathf.CeilToInt(height / 0.5f), 2, 170);
            float spacing = height / count;
            _rings = new MeshRenderer[count];
            _ringY = new float[count];
            _applied = new float[count];
            _baseColour = world.wallColor;
            Mesh torus = FreefallMeshes.Torus(0.014f);
            for (int i = 0; i < count; i++)
            {
                MeshRenderer ring = AddMesh(Root.transform, "Ring", torus, mats.glowFlat);
                float y = Y0 + i * spacing;
                ring.transform.localPosition = new Vector3(0f, y, 0f);
                ring.transform.localScale = Vector3.one * Radius;
                _rings[i] = ring;
                _ringY[i] = y;
                _applied[i] = -1f;
            }
        }

        public override void OnHit(in FreefallHit hit, Color hue, float time)
        {
            if (_pulses.Count >= 24)
            {
                _pulses.RemoveAt(0);
            }

            _pulses.Add(new Pulse { y = hit.position.y, t0 = time, colour = hue });
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            for (int p = _pulses.Count - 1; p >= 0; p--)
            {
                if (time - _pulses[p].t0 > PulseSeconds)
                {
                    _pulses.RemoveAt(p);
                }
            }

            for (int i = 0; i < _rings.Length; i++)
            {
                float glow = 0f;
                Color tint = Color.black;
                for (int p = 0; p < _pulses.Count; p++)
                {
                    Pulse pulse = _pulses[p];
                    float age = time - pulse.t0;
                    float front = pulse.y - PulseSpeed * age;
                    float d = _ringY[i] - front;
                    // A bright head at the front that trails off above it: the glow travels down.
                    float g = d >= 0f ? Mathf.Exp(-d * d * 3.2f) : Mathf.Exp(-d * d * 0.4f) * 0.35f;
                    g *= 1f - age / PulseSeconds;
                    glow += g;
                    tint += pulse.colour * g;
                }

                // Capped near 2x: past that ACES bleaches the hue to white and the near rings glare.
                float level = 0.3f + Mathf.Min(glow, 1f) * 1.8f;
                if (Mathf.Abs(level - _applied[i]) < 0.015f)
                {
                    continue;
                }

                _applied[i] = level;
                Color colour = glow > 0.001f ? Color.Lerp(_baseColour, tint / glow, Mathf.Clamp01(glow)) : _baseColour;
                SetBaseColor(_rings[i], FreefallColors.Hdr(colour, level, 1f));
            }
        }
    }

    // ------------------------------------------------------------------ Aurora

    /// <summary>Soft vertical additive curtains on the wall, slowly waving.</summary>
    internal sealed class AuroraWall : WallView
    {
        const int Curtains = 7;
        const int Rows = 38;
        const int Cols = 12;

        sealed class Curtain
        {
            public Mesh mesh;
            public Vector3[] verts;
            public float centre;
            public float halfWidth;
            public float phase;
            public float speed;
            public float inset;
        }

        readonly Curtain[] _curtains = new Curtain[Curtains];
        float _pulse;

        public AuroraWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            for (int c = 0; c < Curtains; c++)
            {
                var curtain = new Curtain
                {
                    centre = (c + Rand(-0.25f, 0.25f)) / Curtains * Mathf.PI * 2f,
                    halfWidth = Rand(0.28f, 0.42f),
                    phase = Rand(0f, 6.28f),
                    speed = Rand(0.35f, 0.7f),
                    inset = Rand(0f, 0.5f)
                };

                var colours = new List<Color>(Rows * Cols);
                var uvs = new List<Vector2>(Rows * Cols);
                float hueShift = Rand(-0.07f, 0.07f);
                Color tint = Color.HSVToRGB(Mathf.Repeat(Mathf.Lerp(world.hueStart, world.hueStart + world.hueSpan, c / (float)Curtains) + hueShift, 1f), 0.65f, 1f);
                for (int r = 0; r < Rows; r++)
                {
                    float v = r / (float)(Rows - 1);
                    float vertical = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 1.4f);
                    for (int k = 0; k < Cols; k++)
                    {
                        float u = k / (float)(Cols - 1);
                        float edge = Mathf.Sin(u * Mathf.PI);
                        // Additive and seen near edge-on down the well, so layers stack: kept very low, a faint
                        // backdrop wash that never competes with the marble.
                        float a = vertical * edge * Rand(0.85f, 1f) * 0.14f;
                        colours.Add(new Color(tint.r, tint.g, tint.b, a));
                        uvs.Add(new Vector2(u, v));
                    }
                }

                var tris = new List<int>(Rows * Cols * 6);
                for (int r = 0; r < Rows - 1; r++)
                {
                    for (int k = 0; k < Cols - 1; k++)
                    {
                        int a = r * Cols + k;
                        tris.Add(a);
                        tris.Add(a + Cols);
                        tris.Add(a + 1);
                        tris.Add(a + 1);
                        tris.Add(a + Cols);
                        tris.Add(a + Cols + 1);
                    }
                }

                curtain.verts = new Vector3[Rows * Cols];
                curtain.mesh = new Mesh { name = "AuroraCurtain" };
                curtain.mesh.MarkDynamic();
                curtain.mesh.vertices = curtain.verts;
                curtain.mesh.SetColors(colours);
                curtain.mesh.SetUVs(0, uvs);
                curtain.mesh.SetTriangles(tris, 0);
                _curtains[c] = curtain;
                AddMesh(Root.transform, "Curtain", curtain.mesh, mats.glowFlat);
                Fill(curtain, 0f);
            }
        }

        void Fill(Curtain curtain, float time)
        {
            float height = Y1 - Y0;
            for (int r = 0; r < Rows; r++)
            {
                float v = r / (float)(Rows - 1);
                float y = Y0 + v * height;
                float wave = 0.16f * Mathf.Sin(y * 0.33f + time * curtain.speed + curtain.phase) +
                             0.07f * Mathf.Sin(y * 0.91f - time * curtain.speed * 1.7f);
                for (int k = 0; k < Cols; k++)
                {
                    float u = k / (float)(Cols - 1);
                    float angle = curtain.centre + (u - 0.5f) * 2f * curtain.halfWidth + wave;
                    float radius = Radius - curtain.inset * 0.4f + 0.18f * Mathf.Sin(y * 0.5f + curtain.phase + time * 0.3f);
                    curtain.verts[r * Cols + k] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }
            }

            curtain.mesh.vertices = curtain.verts;
            curtain.mesh.RecalculateBounds();
        }

        public override void OnHit(in FreefallHit hit, Color hue, float time)
        {
            _pulse = 1f;
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 1.6f);
            for (int c = 0; c < Curtains; c++)
            {
                Fill(_curtains[c], time);
            }
        }
    }

    // ------------------------------------------------------------------ Mist

    /// <summary>Sparse soft particles drifting in the well plus a faint cylinder. The ripples live on the pads.</summary>
    internal sealed class MistWall : WallView
    {
        readonly Mesh _tube;

        public MistWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            // Faint cylinder: darkest at the ends so the band fades in and out.
            _tube = BuildTube(Radius, Y0, Y1, world.wallColor);
            AddMesh(Root.transform, "Haze", _tube, mats.glowFlat);

            int count = Mathf.Clamp(Mathf.RoundToInt((Y1 - Y0) * 4.5f), 80, 320);
            ParticleSystem system = MakeSystem(Root.transform, "Motes", mats.glowSoft, FreefallColors.Hdr(Color.white, 1.4f), count);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.12f;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float angle = Rand(0f, 6.2832f);
                float radius = Mathf.Sqrt(Rand(0.04f, 0.88f)) * World.wellRadius * 1.05f;
                emit.position = new Vector3(Mathf.Cos(angle) * radius, Rand(Y0, Y1), Mathf.Sin(angle) * radius);
                emit.startSize = Rand(0.35f, 1.0f);
                Color c = Color.Lerp(world.wallColor, Color.white, Rand(0f, 0.4f));
                emit.startColor = new Color(c.r, c.g, c.b, Rand(0.1f, 0.26f));
                emit.startLifetime = 1e5f;
                emit.velocity = Vector3.zero;
                system.Emit(emit, 1);
            }
        }

        public override void Tick(float time, float dt, Vector3 hero) { }

        internal static Mesh BuildTube(float radius, float y0, float y1, Color colour)
        {
            const int segs = 40;
            const int rows = 8;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            for (int r = 0; r <= rows; r++)
            {
                float v = r / (float)rows;
                float y = Mathf.Lerp(y0, y1, v);
                float a = Mathf.Sin(v * Mathf.PI) * 0.07f;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(t) * radius, y, Mathf.Sin(t) * radius));
                    cols.Add(new Color(colour.r, colour.g, colour.b, a));
                }
            }

            var tris = new List<int>();
            for (int r = 0; r < rows; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int a = r * (segs + 1) + s;
                    int b = a + segs + 1;
                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(a + 1);
                    tris.Add(a + 1);
                    tris.Add(b);
                    tris.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = "FallTube" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A world-space particle system with emission off, so particles exist only where <c>Emit</c> puts them.</summary>
        internal static ParticleSystem MakeSystem(Transform parent, string name, Material material, Color intensity, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e5f;
            main.startSpeed = 0f;
            main.maxParticles = maxParticles + 8;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            system.useAutoRandomSeed = false;
            system.randomSeed = 1234u;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor(FreefallColors.BaseColor, intensity);
            renderer.SetPropertyBlock(block);
            system.Play();
            return system;
        }
    }

    // ------------------------------------------------------------------ CrystalShards

    /// <summary>Faceted crystal shards lining the wall, pointing inward. Shards near a hit flash.</summary>
    internal sealed class CrystalWall : WallView
    {
        readonly MeshRenderer[] _shards;
        readonly Vector3[] _positions;
        readonly Color[] _hues;
        readonly float[] _flash;
        readonly float[] _applied;
        readonly List<int> _flashing = new List<int>(32);

        public CrystalWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt((Y1 - Y0) * 3.2f), 40, 230);
            _shards = new MeshRenderer[count];
            _positions = new Vector3[count];
            _hues = new Color[count];
            _flash = new float[count];
            _applied = new float[count];
            Mesh mesh = FreefallMeshes.Shard();
            for (int i = 0; i < count; i++)
            {
                float angle = Rand(0f, 6.2832f);
                float y = Rand(Y0, Y1);
                float radius = Radius + Rand(-0.15f, 0.25f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Vector3 inward = new Vector3(-Mathf.Cos(angle), Rand(-0.45f, 0.1f), -Mathf.Sin(angle)).normalized;

                MeshRenderer shard = AddMesh(Root.transform, "Shard", mesh, mats.crystal);
                shard.transform.localPosition = position;
                shard.transform.localRotation = Quaternion.AngleAxis(Rand(0f, 360f), inward) * Quaternion.FromToRotation(Vector3.up, inward);
                float length = Rand(0.7f, 1.7f);
                shard.transform.localScale = new Vector3(Rand(0.5f, 1.1f), length, Rand(0.5f, 1.1f));
                _shards[i] = shard;
                _positions[i] = position;
                _hues[i] = FreefallColors.Hue(world, Rand(0f, 1f), 0.6f, 1f);
                _applied[i] = -1f;
                ApplyShard(i, 0f);
            }
        }

        void ApplyShard(int i, float flash)
        {
            if (Mathf.Abs(flash - _applied[i]) < 0.01f)
            {
                return;
            }

            _applied[i] = flash;
            Color tint = _hues[i];
            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 1f));
            Color emission = tint * (0.12f + flash * 2.2f);
            emission.a = 1f;
            Block.SetColor(FreefallColors.EmissionColor, emission);
            _shards[i].SetPropertyBlock(Block);
        }

        public override void OnHit(in FreefallHit hit, Color hue, float time)
        {
            for (int i = 0; i < _shards.Length; i++)
            {
                if ((_positions[i] - hit.position).sqrMagnitude < 9f)
                {
                    if (_flash[i] <= 0f)
                    {
                        _flashing.Add(i);
                    }

                    _flash[i] = 1f;
                    _hues[i] = Color.Lerp(_hues[i], hue, 0.6f);
                }
            }
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            for (int k = _flashing.Count - 1; k >= 0; k--)
            {
                int i = _flashing[k];
                _flash[i] = Mathf.Max(0f, _flash[i] - dt * 2.2f);
                ApplyShard(i, _flash[i]);
                if (_flash[i] <= 0f)
                {
                    _flashing.RemoveAt(k);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Mandala

    /// <summary>A six-fold symmetric lattice of petals on the wall, turning slowly.</summary>
    internal sealed class MandalaWall : WallView
    {
        readonly MeshRenderer _renderer;
        float _pulse;

        public MandalaWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            const int perRing = 12;
            float rowHeight = 1.15f;
            int rows = Mathf.Clamp(Mathf.CeilToInt((Y1 - Y0) / rowHeight), 3, 90);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            float halfArc = Mathf.PI * 2f / perRing * 0.5f * 0.92f;
            for (int r = 0; r < rows; r++)
            {
                float yMid = Y0 + (r + 0.5f) * (Y1 - Y0) / rows;
                float half = (Y1 - Y0) / rows * 0.62f;
                float offset = (r % 2 == 0) ? 0f : Mathf.PI * 2f / perRing * 0.5f;
                float rowHue = Mathf.Repeat(world.hueStart + r * 0.043f, 1f);
                for (int p = 0; p < perRing; p++)
                {
                    // Symmetry: the colour depends on the petal's index mod 6 only, never on a random draw per petal.
                    float hue = Mathf.Repeat(rowHue + (p % 6) / 6f * Mathf.Max(0.15f, world.hueSpan), 1f);
                    Color c = Color.HSVToRGB(hue, 0.7f, 1f);
                    float a = Mathf.PI * 2f * p / perRing + offset;
                    Vector3 left = Pos(a - halfArc, yMid);
                    Vector3 right = Pos(a + halfArc, yMid);
                    Vector3 top = Pos(a, yMid + half);
                    Vector3 bottom = Pos(a, yMid - half);
                    int i0 = verts.Count;
                    verts.Add(left);
                    verts.Add(top);
                    verts.Add(right);
                    verts.Add(bottom);
                    cols.Add(new Color(c.r, c.g, c.b, 0.08f));
                    cols.Add(new Color(c.r, c.g, c.b, 0.55f));
                    cols.Add(new Color(c.r, c.g, c.b, 0.08f));
                    cols.Add(new Color(c.r, c.g, c.b, 0.55f));
                    tris.Add(i0);
                    tris.Add(i0 + 1);
                    tris.Add(i0 + 2);
                    tris.Add(i0);
                    tris.Add(i0 + 2);
                    tris.Add(i0 + 3);
                }
            }

            var mesh = new Mesh { name = "FallMandala" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            _renderer = AddMesh(Root.transform, "Lattice", mesh, mats.glowFlat);
            SetBaseColor(_renderer, FreefallColors.Hdr(Color.white, 1.1f));
        }

        Vector3 Pos(float angle, float y)
        {
            return new Vector3(Mathf.Cos(angle) * Radius, y, Mathf.Sin(angle) * Radius);
        }

        public override void OnHit(in FreefallHit hit, Color hue, float time)
        {
            _pulse = 1f;
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            // Rotation is a function of the display time, so a re-render turns identically.
            Root.transform.localRotation = Quaternion.AngleAxis(time * 5f, Vector3.up);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 2.5f);
            SetBaseColor(_renderer, FreefallColors.Hdr(Color.white, 1.1f + _pulse * 0.9f));
        }
    }

    // ------------------------------------------------------------------ Starfield

    /// <summary>Stars on a large cylinder, and a single silver moonbeam that follows the marble.</summary>
    internal sealed class StarfieldWall : WallView
    {
        readonly Transform _beam;
        readonly Light _spot;
        readonly MeshRenderer _column;

        public StarfieldWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt((Y1 - Y0 + 20f) * 7f), 200, 700);
            ParticleSystem stars = MistWall.MakeSystem(Root.transform, "Stars", mats.glowSoft, FreefallColors.Hdr(Color.white, 2.2f), count);
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float angle = Rand(0f, 6.2832f);
                float radius = Rand(10f, 26f);
                emit.position = new Vector3(Mathf.Cos(angle) * radius, Rand(Y0 - 10f, Y1 + 10f), Mathf.Sin(angle) * radius);
                emit.startSize = Rand(0.07f, 0.26f);
                float warm = Rand(0f, 1f);
                Color c = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.92f, 0.8f), warm);
                emit.startColor = new Color(c.r, c.g, c.b, Rand(0.35f, 1f));
                emit.startLifetime = 1e5f;
                emit.velocity = Vector3.zero;
                stars.Emit(emit, 1);
            }

            // The moonbeam: a cool spotlight from above and a faint column, both following the hero.
            var beamObject = new GameObject("Moonbeam");
            beamObject.transform.SetParent(Root.transform, false);
            _beam = beamObject.transform;
            _spot = beamObject.AddComponent<Light>();
            _spot.type = LightType.Spot;
            _spot.color = new Color(0.78f, 0.86f, 1f);
            _spot.intensity = 6f;
            _spot.range = 14f;
            _spot.spotAngle = 38f;
            _spot.innerSpotAngle = 12f;
            _spot.shadows = LightShadows.None;

            _column = AddMesh(Root.transform, "Beam Column", FreefallMeshes.Quad(), mats.glowSoft);
            SetBaseColor(_column, new Color(0.7f, 0.82f, 1f, 0.12f) * 1.0f);
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            _beam.position = hero + new Vector3(1.6f, 7f, -1.6f);
            _beam.rotation = Quaternion.LookRotation((hero - _beam.position).normalized, Vector3.up);

            // A tall soft column that always turns its face to the Y axis through the hero, never between camera and marble.
            Vector3 axisToCamera = Camera.main != null ? Camera.main.transform.position - hero : Vector3.back;
            axisToCamera.y = 0f;
            if (axisToCamera.sqrMagnitude < 1e-4f)
            {
                axisToCamera = Vector3.back;
            }

            // Placed behind the marble relative to the camera so it never covers it.
            Vector3 away = -axisToCamera.normalized;
            _column.transform.position = hero + away * 0.9f + Vector3.up * 3.5f;
            _column.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            _column.transform.localScale = new Vector3(1.6f, 9f, 1f);
        }
    }

    // ------------------------------------------------------------------ Vortex

    /// <summary>
    /// Spiral strips up the well. They turn at a rate that follows the world's tempo, so the vortex spins up
    /// with the accelerando. The angle is the closed-form integral of the tempo, a pure function of the time.
    /// </summary>
    internal sealed class VortexWall : WallView
    {
        readonly float _degreesAtBase;
        float _pulse;
        readonly MeshRenderer _renderer;

        public VortexWall(Transform parent, string name, FreefallWorld world, FreefallWorldSpan span, FreefallMaterials mats, System.Random rng)
            : base(parent, name, world, span, mats, rng)
        {
            _degreesAtBase = Mathf.Max(10f, world.degreesPerSecond * 0.8f);
            const int strips = 7;
            const int rows = 70;
            float twist = Rand(0.30f, 0.42f);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int s = 0; s < strips; s++)
            {
                float a0 = s / (float)strips * Mathf.PI * 2f;
                float width = Rand(0.06f, 0.13f);
                Color c = Color.HSVToRGB(Mathf.Repeat(world.hueStart + world.hueSpan * (s / (float)strips), 1f), 0.85f, 1f);
                int first = verts.Count;
                for (int r = 0; r < rows; r++)
                {
                    float v = r / (float)(rows - 1);
                    float y = Mathf.Lerp(Y0, Y1, v);
                    float angle = a0 + (y - Y0) * twist;
                    float alpha = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.8f) * 0.7f;
                    verts.Add(new Vector3(Mathf.Cos(angle - width) * Radius, y, Mathf.Sin(angle - width) * Radius));
                    verts.Add(new Vector3(Mathf.Cos(angle + width) * Radius, y, Mathf.Sin(angle + width) * Radius));
                    cols.Add(new Color(c.r, c.g, c.b, alpha * 0.25f));
                    cols.Add(new Color(c.r, c.g, c.b, alpha * 0.25f));
                }

                for (int r = 0; r < rows - 1; r++)
                {
                    int i = first + r * 2;
                    tris.Add(i);
                    tris.Add(i + 2);
                    tris.Add(i + 1);
                    tris.Add(i + 1);
                    tris.Add(i + 2);
                    tris.Add(i + 3);
                }
            }

            var mesh = new Mesh { name = "FallVortex" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            _renderer = AddMesh(Root.transform, "Spirals", mesh, mats.glowFlat);
            SetBaseColor(_renderer, FreefallColors.Hdr(Color.white, 3f));
        }

        public override void OnHit(in FreefallHit hit, Color hue, float time)
        {
            _pulse = 1f;
        }

        float AngleAt(float time)
        {
            float t0 = Span.firstHitTime;
            float length = Mathf.Max(0.01f, Span.lastHitTime - t0);
            float b0 = World.bpm;
            float b1 = World.bpmEnd;
            float x = time - t0;
            // Integral of tempo(t)/tempo0, with a linear ramp across the world and a constant rate outside it.
            float scale;
            if (x <= 0f)
            {
                scale = x;
            }
            else if (x <= length)
            {
                scale = x + (b1 - b0) / b0 * x * x / (2f * length);
            }
            else
            {
                float atEnd = length + (b1 - b0) / b0 * length * 0.5f;
                scale = atEnd + (x - length) * (b1 / b0);
            }

            return scale * _degreesAtBase;
        }

        public override void Tick(float time, float dt, Vector3 hero)
        {
            Root.transform.localRotation = Quaternion.AngleAxis(AngleAt(time), Vector3.up);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 3f);
            SetBaseColor(_renderer, FreefallColors.Hdr(Color.white, 3f + _pulse * 2.5f));
        }
    }
}
