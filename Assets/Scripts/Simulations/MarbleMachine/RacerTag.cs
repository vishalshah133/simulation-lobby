using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A floating number above one racer, plus a short coloured trail behind it. The number is a world-space TextMeshPro decal
    /// that always faces the camera and keeps a roughly constant size on screen (clamped), with a dark outline so it reads on the
    /// light-grey set. Spawned by <see cref="RacePresenter"/>, one per racer. Reads <see cref="IRaceState"/> in
    /// <c>LateUpdate</c> and never writes it.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the camera director has posed the camera this frame
    public sealed class RacerTag : MonoBehaviour
    {
        [Header("Number")]
        [Tooltip("Floating number above the ball. Off by default: it distracts from the marbles, and the board shows each number with its colour.")]
        public bool showNumber = false;

        [Header("Placement")]
        [Tooltip("World metres above the ball centre.")]
        [Min(0f)] public float heightAboveBall = 0.55f;

        [Header("Size")]
        [Tooltip("Tag height as a fraction of the screen height. 0.045 is about 48 px on a 1080 frame, readable at 480 px wide.")]
        [Range(0.01f, 0.15f)] public float screenHeightFraction = 0.045f;
        [Min(0.05f)] public float minScale = 0.45f;
        [Min(0.1f)] public float maxScale = 2.6f;
        [Tooltip("The leader's tag is this much larger.")]
        [Min(1f)] public float leaderBoost = 1.3f;
        [Min(1f)] public float hideBeyondMetres = 90f;

        [Header("Look")]
        [Tooltip("Draw over the set (tags are never hidden behind chutes or walls).")]
        public bool alwaysOnTop = true;
        [Range(0f, 1f)] public float outlineWidth = 0.28f;
        public Color outlineColour = new Color(0.04f, 0.04f, 0.07f, 1f);

        [Header("Trail")]
        public bool showTrail = true;
        [Min(0.05f)] public float trailSeconds = 0.9f;
        [Min(0.01f)] public float trailWidth = 0.22f;
        [Tooltip("A one-frame move longer than this (the recycle cannon) clears the trail instead of drawing a streak.")]
        [Min(0.5f)] public float teleportMetres = 3f;

        IRaceState _state;
        int _racer;
        Camera _camera;
        Transform _label;
        TextMeshPro _text;
        MeshRenderer _textRenderer;
        TrailRenderer _trail;
        Material _trailMaterial;
        Vector3 _lastPosition;
        bool _hasLast;
        float _boost = 1f;

        public int Racer => _racer;

        /// <summary>Set up the tag for one racer. Safe to call once, right after adding the component.</summary>
        public void Bind(IRaceState state, int racer, Camera camera)
        {
            _state = state;
            _racer = racer;
            _camera = camera;
            gameObject.name = "Tag " + state.RacerNumber(racer);
            Build();
        }

        void Build()
        {
            Color colour = _state.RacerColour(_racer);

            if (!showNumber)
            {
                if (showTrail)
                {
                    BuildTrail(colour);
                }

                Place(true);
                return;
            }

            var labelObject = new GameObject("Number");
            labelObject.transform.SetParent(transform, false);
            _text = labelObject.AddComponent<TextMeshPro>();
            // Adding TextMeshPro swaps the Transform for a RectTransform, so a Transform cached before it is destroyed.
            _label = _text.rectTransform;
            _text.text = _state.RacerNumber(_racer).ToString();
            _text.fontSize = 10f;
            _text.fontStyle = FontStyles.Bold;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.raycastTarget = false;
            _text.rectTransform.sizeDelta = new Vector2(6f, 2f);
            _text.color = Color.white; // white reads on every ball colour; the ball itself carries the colour

            if (alwaysOnTop)
            {
                Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
                if (overlay != null)
                {
                    Material instance = _text.fontMaterial; // per-tag instance
                    instance.shader = overlay;
                }
            }

            _text.outlineColor = outlineColour;
            _text.outlineWidth = outlineWidth;

            _textRenderer = _text.GetComponent<MeshRenderer>();
            if (_textRenderer != null)
            {
                _textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _textRenderer.receiveShadows = false;
            }

            if (showTrail)
            {
                BuildTrail(colour);
            }

            Place(true);
        }

        void BuildTrail(Color colour)
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = trailSeconds;
            _trail.minVertexDistance = 0.06f;
            _trail.numCapVertices = 3;
            _trail.alignment = LineAlignment.View;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // head (x=0) thick, tail thin
            _trail.widthMultiplier = trailWidth;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }); // head opaque, tail fades out
            _trail.colorGradient = gradient;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                _trailMaterial = new Material(shader) { name = "RacerTrail " + _racer };
                _trail.sharedMaterial = _trailMaterial;
            }
        }

        void OnDestroy()
        {
            if (_trailMaterial != null)
            {
                Destroy(_trailMaterial);
            }
        }

        void LateUpdate()
        {
            if (_state == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Place(false);
        }

        void Place(bool snap)
        {
            Vector3 position = _state.RacerPosition(_racer);
            if (_hasLast && _trail != null && (position - _lastPosition).sqrMagnitude > teleportMetres * teleportMetres)
            {
                _trail.Clear();
            }

            _lastPosition = position;
            _hasLast = true;
            transform.position = position;

            if (_camera == null || _label == null)
            {
                return;
            }

            Transform cam = _camera.transform;
            Vector3 toTag = position + Vector3.up * heightAboveBall - cam.position;
            float depth = Vector3.Dot(toTag, cam.forward);
            bool visible = depth > 0.5f && depth < hideBeyondMetres;
            if (_textRenderer != null && _textRenderer.enabled != visible)
            {
                _textRenderer.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            float wantBoost = _state.RankOf(_racer) == 1 && _state.Phase >= RacePhase.Racing ? leaderBoost : 1f;
            _boost = snap ? wantBoost : Mathf.MoveTowards(_boost, wantBoost, Time.deltaTime * 1.5f);

            // Constant on-screen size: world height that spans screenHeightFraction of the view at this depth.
            float viewHeight = 2f * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = Mathf.Clamp(viewHeight * screenHeightFraction / 0.7f, minScale, maxScale) * _boost;

            _label.position = position + Vector3.up * heightAboveBall * Mathf.Max(1f, scale * 0.7f);
            _label.rotation = cam.rotation;
            _label.localScale = Vector3.one * scale; // fontSize 10 is about one world unit tall
        }
    }
}
