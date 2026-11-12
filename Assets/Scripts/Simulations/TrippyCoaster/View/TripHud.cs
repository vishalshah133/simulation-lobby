using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// On-screen text for the ride, on a Screen Space - Overlay canvas (1080x1920 reference, match height) so it reads at 9:16 and 16:9.
    /// Top: the hook question and the current world. Bottom: ride progress bar with a notch per world, plus speed.
    /// A title card announces each world; a warning card opens the video. The middle of the frame stays clear. Reads state only.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class TripHud : MonoBehaviour
    {
        public TrippyCoasterHost host;
        public TMP_Text hook, worldLabel, titleCard, warning, speedLabel, worldCount;
        public RectTransform barFill;
        public Image barFillImage;

        void LateUpdate()
        {
            if (host == null || !host.Ready) return;
            ITripState s = host.State;
            TrippyCoasterConfig cfg = s.Config;
            TripWorldDef w = cfg.worlds[s.WorldIndex];
            Color c = TripPalette.Vivid(w, s.SimTime * w.hueSpeed);

            hook.text = cfg.hookQuestion;
            worldLabel.text = w.name;
            worldLabel.color = c;
            worldCount.text = "WORLD " + (s.WorldIndex + 1) + " / " + s.WorldCount;
            speedLabel.text = Mathf.RoundToInt(s.Speed * 3.6f) + " KM/H";

            barFill.anchorMax = new Vector2(Mathf.Clamp01(s.RideProgress), 1f);
            barFillImage.color = c;

            // world title card: fade in 0.5 s, hold, fade out by 3.4 s. No flashing: only a smooth alpha.
            bool atStart = s.WorldIndex == 0 && s.SimTime < cfg.warningSeconds;
            float a = Mathf.Clamp01(s.WorldTime / 0.5f) * (1f - Mathf.Clamp01((s.WorldTime - 2.6f) / 0.8f));
            titleCard.text = w.name;
            titleCard.color = new Color(c.r, c.g, c.b, atStart ? 0f : a);

            float wa = Mathf.Clamp01((cfg.warningSeconds - s.SimTime) / 0.6f);
            warning.color = new Color(1f, 1f, 1f, wa);
        }

        // ---------------------------------------------------------------- construction (runtime-safe, no editor APIs)

        public static TripHud Build(Transform parent, TrippyCoasterHost host)
        {
            var canvasGo = new GameObject("Trip HUD");
            canvasGo.transform.SetParent(parent, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var hud = canvasGo.AddComponent<TripHud>();
            hud.host = host;

            // top panel
            hud.hook = Text(canvasGo.transform, "Hook", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 130f), 66, Color.white, FontStyles.Bold);
            hud.worldLabel = Text(canvasGo.transform, "World", new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1000f, 80f), 52, Color.white, FontStyles.Bold);
            hud.worldCount = Text(canvasGo.transform, "WorldCount", new Vector2(0.5f, 1f), new Vector2(0f, -315f), new Vector2(1000f, 60f), 36, new Color(1f, 1f, 1f, 0.8f), FontStyles.Normal);

            // bottom panel: progress bar with a notch per world
            RectTransform track = Rect(canvasGo.transform, "Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(920f, 16f));
            Image trackImg = track.gameObject.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.18f);
            var fill = Rect(track, "Fill", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            hud.barFill = fill;
            hud.barFillImage = fill.gameObject.AddComponent<Image>();
            hud.speedLabel = Text(canvasGo.transform, "Speed", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 70f), 44, Color.white, FontStyles.Bold);

            // title card (upper third, clear of the track) and the opening warning
            hud.titleCard = Text(canvasGo.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 520f), new Vector2(1040f, 200f), 120, Color.white, FontStyles.Bold);
            hud.warning = Text(canvasGo.transform, "Warning", new Vector2(0.5f, 0.5f), new Vector2(0f, -420f), new Vector2(1000f, 160f), 40, Color.white, FontStyles.Bold);
            hud.warning.text = "WARNING: FLASHING COLOURS\nNOT SUITABLE FOR VIEWERS SENSITIVE TO LIGHT";
            return hud;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static TMP_Text Text(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style)
        {
            RectTransform rt = Rect(parent, name, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }
    }
}
