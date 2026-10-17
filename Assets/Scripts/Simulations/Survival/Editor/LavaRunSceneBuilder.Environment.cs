using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using SimulationLobby.Simulations.Survival.LavaRun;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Sample = SimulationLobby.Shared.EditorTools.TrackMeshBuilder.Sample;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    // Camera, volcano, lake, lighting, HUD and presentation wiring.
    static partial class LavaRunSceneBuilder
    {
        // ------------------------------------------------------------------ camera

        static Camera BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return camera;
        }

        // ------------------------------------------------------------------ volcano

        static void BuildTerrain(Transform environment)
        {
            _terrain = new VolcanoTerrain(_rimY, ConeSlope, _stages);
            float half = _coneBaseRadius + 12f;
            Mesh mesh = LavaRunGeometry.SaveMeshFresh(
                LavaRunGeometry.BuildHeightfield("LavaRun_Volcano", half, 3.5f, _terrain.Height, -0.5f, 1f / 48f), "LavaRun_Volcano");

            var volcano = new GameObject("Volcano");
            volcano.transform.SetParent(environment, false);
            volcano.AddComponent<MeshFilter>().sharedMesh = mesh;
            volcano.AddComponent<MeshRenderer>().sharedMaterial = _kit.basalt;
        }

        static void BuildCrater(Transform environment, out Transform craterPoint)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "Crater Lava";
            pool.transform.SetParent(environment, false);
            pool.transform.position = new Vector3(0f, _rimY - 3f, 0f);
            pool.transform.localScale = new Vector3(14f, 0.02f, 14f);
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            var renderer = pool.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _kit.lavaCrater;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            craterPoint = new GameObject("Crater Point").transform;
            craterPoint.SetParent(environment, false);
            craterPoint.position = new Vector3(0f, _rimY - 2.5f, 0f);
            _craterPoint = craterPoint;

            var glow = new GameObject("Crater Glow").AddComponent<Light>();
            glow.transform.SetParent(environment, false);
            glow.transform.position = new Vector3(0f, _rimY + 3f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.45f, 0.12f);
            glow.range = 120f;
            glow.intensity = 8f;
            glow.shadows = LightShadows.None;
        }

        static void BuildLake(Transform environment, LavaRunRig rig)
        {
            var lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lava Lake";
            lake.transform.SetParent(environment, false);
            lake.transform.position = Vector3.zero;
            lake.transform.localScale = new Vector3(240f, 1f, 240f);
            Object.DestroyImmediate(lake.GetComponent<Collider>());
            var renderer = lake.GetComponent<MeshRenderer>();
            _lakeRenderer = renderer;
            renderer.sharedMaterial = _kit.lavaLake;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Just above the surface: anything that reaches it is swallowed. The island and the bridge's end sit above its top.
            var zoneObject = new GameObject("Lava Lake Zone");
            zoneObject.transform.SetParent(environment, false);
            zoneObject.transform.position = new Vector3(0f, 0.15f, 0f);
            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2400f, 1.3f, 2400f);
            rig.lavaLakeZone = zoneObject.AddComponent<TriggerZone3D>();
        }

        // ------------------------------------------------------------------ light + atmosphere

        static void BuildLighting(Transform environment, Camera camera, out Vector3 widePosition, out Vector3 wideTarget)
        {
            SolveWideShot(camera, out widePosition, out wideTarget, out float distance);
            camera.farClipPlane = Mathf.Max(600f, distance * 3.2f);

            var lights = new GameObject("Lights").transform;
            lights.SetParent(environment, false);

            // Warm key from the front: the glow of the lava on the stages' faces.
            Light key = NewDirectional(lights, "Key (lava glow)", new Color(1f, 0.52f, 0.24f), 1.15f, Quaternion.Euler(22f, 15f, 0f));
            key.shadows = LightShadows.None;

            // Cool rim from above and behind, the only shadow caster: it separates the dark cone from the sky.
            Light rim = NewDirectional(lights, "Rim (moon)", new Color(0.55f, 0.68f, 1f), 1.0f, Quaternion.Euler(42f, 190f, 0f));
            rim.shadows = LightShadows.Soft;
            rim.shadowStrength = 0.6f;

            // Dim violet fill so the shadow side keeps a shape.
            NewDirectional(lights, "Fill", new Color(0.55f, 0.4f, 0.75f), 0.3f, Quaternion.Euler(60f, -40f, 0f));

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.08f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.07f, 0.06f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.09f, 0.02f);
            RenderSettings.skybox = EnsureSky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.10f, 0.045f, 0.055f);
            RenderSettings.fogStartDistance = distance * 0.5f;
            RenderSettings.fogEndDistance = distance * 2.6f;
            RenderSettings.reflectionIntensity = 0.6f;

            // One box-projected probe over the whole scene so chrome and lacquer reflect lava and sky, not the default cubemap.
            var probeObject = new GameObject("Scene Reflection Probe");
            probeObject.transform.SetParent(environment, false);
            probeObject.transform.position = new Vector3(0f, _rimY * 0.5f, -120f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = new Vector3(_coneBaseRadius * 3f, _rimY * 3f, 900f);
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.farClipPlane = 900f;
            probe.importance = 10;
            probe.intensity = 0.55f;   // calmer reflections on the course; the marbles keep their clear coat

            BuildPost();
        }

        static Light NewDirectional(Transform parent, string name, Color color, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        static Material EnsureSky()
        {
            return LavaRunMaterials.EnsureMaterial("LavaRunSky", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunSize", 0f);
                m.EnableKeyword("_SUNDISK_NONE");
                m.SetFloat("_AtmosphereThickness", 0.75f);
                m.SetColor("_SkyTint", new Color(0.20f, 0.12f, 0.30f));
                m.SetColor("_GroundColor", new Color(0.20f, 0.07f, 0.05f));
                m.SetFloat("_Exposure", 0.8f);
            });
        }

        static void BuildPost()
        {
            string path = $"{LavaRunMaterials.Folder}/LavaRunPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            {

                Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
                tonemapping.mode.Override(TonemappingMode.ACES);

                // The lava is HDR (values above 1); a low threshold lets it bloom without blowing out the gloss.
                Bloom bloom = GetOrAdd<Bloom>(profile);
                bloom.threshold.Override(1.1f);
                bloom.intensity.Override(1.2f);
                bloom.scatter.Override(0.7f);

                Vignette vignette = GetOrAdd<Vignette>(profile);
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.5f);

                ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
                color.contrast.Override(8f);
                color.saturation.Override(6f);
                EditorUtility.SetDirty(profile);
            }

            var volumeObject = new GameObject("Lava Run Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                return existing;
            }

            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>Pulls the camera back along a fixed view direction until the whole volcano and the island fit a 16:9 frame.</summary>
        static void SolveWideShot(Camera camera, out Vector3 position, out Vector3 target, out float distance)
        {
            var points = new List<Vector3> { new Vector3(0f, _rimY, 0f) };
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                points.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _coneBaseRadius);
            }

            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                points.Add(_islandCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * IslandRadius);
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points)
            {
                bounds.Encapsulate(point);
            }

            target = bounds.center;
            Vector3 direction = new Vector3(0.12f, 0.26f, -1f).normalized;

            camera.aspect = 16f / 9f;
            position = target + direction * 1500f;
            distance = 1500f;
            for (float d = 100f; d <= 2000f; d += 8f)
            {
                Vector3 candidate = target + direction * d;
                camera.transform.SetPositionAndRotation(candidate, Quaternion.LookRotation(target - candidate, Vector3.up));
                bool fits = true;
                foreach (Vector3 point in points)
                {
                    Vector3 v = camera.WorldToViewportPoint(point);
                    if (v.z <= 0.1f || v.x < 0.05f || v.x > 0.95f || v.y < 0.17f || v.y > 0.85f)
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits)
                {
                    position = candidate;
                    distance = d;
                    break;
                }
            }

            camera.ResetAspect();
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        }

        // ------------------------------------------------------------------ embers

        static void BuildEmbers(Transform environment)
        {
            Texture2D dot = LavaRunGeometry.EnsureEmberDot();
            var root = new GameObject("Ambient Embers").transform;
            root.SetParent(environment, false);

            // Vertical sheets at three depths so the embers have parallax in a 3D scene.
            float[] depths = { 20f, -50f, -130f };
            for (int i = 0; i < depths.Length; i++)
            {
                var sheet = new GameObject($"Embers {i + 1}");
                sheet.transform.SetParent(root, false);
                sheet.transform.position = new Vector3(0f, _rimY * 0.55f, depths[i]);
                sheet.AddComponent<ParticleSystem>();
                AmbientDriftField field = sheet.AddComponent<AmbientDriftField>();
                field.fieldSize = new Vector2(_coneBaseRadius * 2.2f, _rimY * 1.3f);
                field.particleCount = 90;
                field.randomSeed = 7717 + i * 101;
                field.tint = new Color(1f, 0.5f, 0.12f, 1f);
                field.maxAlpha = 0.55f;
                field.sizeRange = new Vector2(0.15f, 0.5f);
                field.driftSpeed = 0.2f;
                field.flow = new Vector2(0.15f, 0.7f);
                field.sortingOrder = -50;
                field.Apply(dot);
            }
        }

        // ------------------------------------------------------------------ HUD

        static void BuildHud(out CaptionHud caption, out ShareBarHud bar, out TMP_Text question)
        {
            caption = CaptionCanvasBuilder.Build(
                "PICK YOUR COLOR",
                "×2 doubles you  ·  lava kills  ·  smallest color each stage is OUT",
                "STAGE 1 / 4  ·  THE SLOPES",
                CaptionCanvasBuilder.Style.Dark);
            var canvasRect = (RectTransform)caption.transform;

            // The question across the top, the four-colour counter bar under it, the stage footer under that.
            question = ScoreboardCanvasBuilder.CreateText(canvasRect, "Question", "WHICH COLOR SURVIVES THE LAVA?", 84f, Color.white,
                FontStyles.Bold);
            RectTransform questionRect = question.rectTransform;
            questionRect.anchorMin = new Vector2(0f, 1f);
            questionRect.anchorMax = new Vector2(1f, 1f);
            questionRect.pivot = new Vector2(0.5f, 1f);
            questionRect.anchoredPosition = new Vector2(0f, -24f);
            questionRect.sizeDelta = new Vector2(-80f, 120f);
            question.textWrappingMode = TextWrappingModes.NoWrap;
            question.outlineWidth = 0.2f;
            question.outlineColor = new Color32(0, 0, 0, 255);

            bar = ShareBarCanvasBuilder.BuildFramed(canvasRect, LavaRunConfig.TeamCount, new Color32(0x1B, 0x14, 0x16, 255), 10f, 6f, 50f);
            bar.showNames = false;
            bar.eliminatedShowsZero = true;
            var colors = new Color[LavaRunConfig.TeamCount];
            var names = new string[LavaRunConfig.TeamCount];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = _config.teams[i].color;
                names[i] = _config.teams[i].Short;
            }

            bar.Configure(LavaRunConfig.TeamCount, colors, names);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0.22f, 1f);
            barRect.anchorMax = new Vector2(0.78f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -152f);
            barRect.sizeDelta = new Vector2(0f, 92f);

            Transform top = canvasRect.Find("TopPanel");
            Transform bottom = canvasRect.Find("BottomPanel");
            if (top != null)
            {
                // Beat caption and rules sit along the bottom, clear of the volcano's active area.
                var rect = (RectTransform)top;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 36f);
                rect.sizeDelta = new Vector2(-80f, 230f);
                StyleText(top.Find("Caption"), 96f, true);
                StyleText(top.Find("Subline"), 44f, false);
            }

            if (bottom != null)
            {
                var rect = (RectTransform)bottom;
                rect.anchorMin = new Vector2(0.25f, 1f);
                rect.anchorMax = new Vector2(0.75f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -252f);
                rect.sizeDelta = new Vector2(0f, 64f);
                StyleText(bottom.Find("Footer"), 46f, false);
            }
        }

        static void StyleText(Transform text, float size, bool outline)
        {
            if (text == null || !text.TryGetComponent(out TMP_Text tmp))
            {
                return;
            }

            tmp.fontSize = size;
            if (outline)
            {
                tmp.fontStyle = FontStyles.Bold;
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = new Color32(0, 0, 0, 255);
            }
        }

        // ------------------------------------------------------------------ presentation

        static void BuildPresentation(LavaRunSimulation simulation, FocusCameraRig focus, Transform[] poses, Transform widePose,
            CaptionHud caption, ShareBarHud counterBar, TMP_Text question, Transform craterPoint, GameObject[] ghosts, Transform hookPose)
        {
            var presentationObject = new GameObject("Presentation");
            LavaRunPresenter presenter = presentationObject.AddComponent<LavaRunPresenter>();
            presenter.simulation = simulation;
            presenter.cameraRig = focus;
            presenter.stageCameraPoses = poses;
            presenter.poseLookDistance = 50f;
            presenter.wideCameraPose = widePose;
            presenter.hookCameraPose = hookPose;
            presenter.lakeRenderer = _lakeRenderer;
            presenter.lakeMaterial = _kit.lavaLake;
            presenter.caption = caption;
            presenter.counterBar = counterBar;
            presenter.questionText = question;
            presenter.craterPoint = craterPoint;
            presenter.ghostTemplates = ghosts;

            // One ribbon object per stage path, each with its own MeshFilter + MeshRenderer.
            var ribbonRoot = new GameObject("Lava Ribbons").transform;
            ribbonRoot.SetParent(presentationObject.transform, false);
            presenter.lavaRibbons = new PathRibbonView[LavaRunConfig.StageCount];
            for (int i = 0; i < presenter.lavaRibbons.Length; i++)
            {
                var ribbon = new GameObject($"Lava Ribbon Stage {i + 1}");
                ribbon.transform.SetParent(ribbonRoot, false);
                presenter.lavaRibbons[i] = ribbon.AddComponent<PathRibbonView>();
            }

            presenter.lavaMaterial = _kit.lavaRibbon;
            presenter.lavaRibbonWidth = LavaRunLayouts.TrackWidth - 0.4f;

            presenter.blastFx = presentationObject.AddComponent<BlastFx>();
            presenter.smokeFx = presentationObject.AddComponent<SmokePlumeFx>();

            var embers = new GameObject("Ember Burst");
            embers.transform.SetParent(presentationObject.transform, false);
            presenter.embers = embers.AddComponent<EmberBurst>();

            var confetti = new GameObject("Confetti Burst");
            confetti.transform.SetParent(presentationObject.transform, false);
            presenter.confetti = confetti.AddComponent<ConfettiBurst3D>();

            presenter.audioKit = presentationObject.AddComponent<LavaRunAudioKit>();
        }

        // ------------------------------------------------------------------ report

        static void Report(TrackLayout[] layouts)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"[surv-lava] Built {ScenePath}");
            text.AppendLine($"  volcano: rim y {_rimY:0.0} m, cone base radius {_coneBaseRadius:0} m, island centre ({_islandCentre.x:0}, {_islandCentre.z:0}), " +
                            $"island radius {IslandRadius:0} m, {RingEdges.Length - 1} rings + disc, bridge width {LavaRunLayouts.BridgeWidth:0.00} m " +
                            $"(marble diameter {LavaRunLayouts.MarbleRadius * 2f:0.00} m)");

            for (int i = 0; i < _stages.Length; i++)
            {
                StageData s = _stages[i];
                float lengthToEnd = s.basinSection >= 0 ? s.basinStartDistance : s.world[s.world.Count - 1].distance;
                float drop = s.world[0].position.y - s.world[s.boundary].position.y;
                float estimate = TrackMeshBuilder.EstimateSeconds(s.world, lengthToEnd);
                var gateText = new System.Text.StringBuilder();
                foreach (LavaGateSpec spec in LavaRunLayouts.Gates(i))
                {
                    float d = SectionStart(s.world, spec.section) + spec.offset;
                    gateText.Append($"{spec.label}@{d:0}m ");
                }

                float entry = s.basinSection >= 0 ? s.basinStartDistance + 0.4f : SectionStart(s.world, LavaRunLayouts.LandingSection);
                float damped = DampedSeconds(s.world, lengthToEnd, MarbleLinearDamping);
                text.AppendLine($"  stage {i + 1} {s.title}: {lengthToEnd:0} m to {(s.basinSection >= 0 ? "basin" : "island")}, drop {drop:0.0} m, " +
                                $"est. ~{damped:0} s with linearDamping {MarbleLinearDamping:0.00} (free-roll ~{estimate:0} s; pegs/baffles add more, a real Play decides), " +
                                $"pens floor y {s.world[0].position.y:0.0}, basinEntryDistance {entry:0.0} m");
                text.AppendLine($"    gates: {gateText}");

                int crossings = CountRealCrossings(s.local, out string where);
                if (crossings > 0)
                {
                    _warnings++;
                    text.AppendLine($"    WARNING: stage {i + 1} passes through itself {crossings} times, first {where}.");
                }
            }

            AppendObstacleChecks(text);
            AppendTiltChecks(text);

            // Stage vs stage: close in plan and close in height means they intersect.
            for (int a = 0; a < _stages.Length; a++)
            {
                for (int b = a + 1; b < _stages.Length; b++)
                {
                    int hits = 0;
                    string first = null;
                    for (int i = 0; i < _stages[a].world.Count; i += 4)
                    {
                        Sample sa = _stages[a].world[i];
                        for (int j = 0; j < _stages[b].world.Count; j += 4)
                        {
                            Sample sb = _stages[b].world[j];
                            Vector3 delta = sa.position - sb.position;
                            if (Mathf.Abs(delta.y) < 4.5f && new Vector2(delta.x, delta.z).magnitude < (sa.width + sb.width) * 0.5f + 0.8f)
                            {
                                hits++;
                                first ??= $"({sa.position.x:0},{sa.position.y:0},{sa.position.z:0}) stage {a + 1} vs ({sb.position.x:0},{sb.position.y:0},{sb.position.z:0}) stage {b + 1}";
                            }
                        }
                    }

                    if (hits > 0)
                    {
                        _warnings++;
                        text.AppendLine($"  WARNING: stage {a + 1} and stage {b + 1} come within 4.5 m of each other in {hits} places, first {first}.");
                    }
                }
            }

            float islandFromAxis = new Vector2(_islandCentre.x, _islandCentre.z).magnitude;
            if (islandFromAxis - IslandRadius < _coneBaseRadius + 8f)
            {
                _warnings++;
                text.AppendLine($"  WARNING: the island ({islandFromAxis:0} m from the axis) is too close to the cone base ({_coneBaseRadius:0} m).");
            }

            text.AppendLine(_warnings == 0 ? "  no layout warnings." : $"  {_warnings} warning(s) above.");
            text.AppendLine("  Pinwheels are KinematicSpinners under each stage's Obstacles; LavaRunSimulation does not step them (static unless it does).");
            Debug.Log(text.ToString());
            if (_obstacleErrors > 0)
            {
                Debug.LogError($"[surv-lava] {_obstacleErrors} obstacle(s) leave a rail gap under 2.5 marble diameters (marbles will wedge). " +
                               "See the ERROR lines in the build report above; an older layout asset needs Simulation Lobby > 3D Formats > Survival (surv) > " +
                               "Reset Lava Run Layouts (default stages), then rebuild.");
            }
            foreach (TrackLayout layout in layouts)
            {
                EditorUtility.SetDirty(layout);
            }
        }

        /// <summary>
        /// Like <c>TrackMeshBuilder.CountCrossings</c>, but pairs closer along the path than (width + 2 m) are ignored, so a wide
        /// flat pen area matching its own neighbours is not reported as a crossing.
        /// </summary>
        static int CountRealCrossings(List<Sample> samples, out string description)
        {
            description = null;
            int crossings = 0;
            for (int i = 0; i < samples.Count; i += 2)
            {
                for (int j = i + 1; j < samples.Count; j += 2)
                {
                    float widest = Mathf.Max(samples[i].width, samples[j].width);
                    if (samples[j].distance - samples[i].distance < Mathf.Max(8f, widest + 2f))
                    {
                        continue;
                    }

                    Vector3 delta = samples[j].position - samples[i].position;
                    float reach = (samples[i].width + samples[j].width) * 0.5f + 0.4f;
                    if (new Vector2(delta.x, delta.z).magnitude < reach && Mathf.Abs(delta.y) < 1.1f)
                    {
                        crossings++;
                        description ??= $"at {samples[i].distance:0}m and {samples[j].distance:0}m (vertical gap {Mathf.Abs(delta.y):0.00}m)";
                    }
                }
            }

            return crossings;
        }

        /// <summary>
        /// For every peg field and baffle set: the smallest gap between pegs and between the outermost peg (or baffle tip) and the
        /// rail, using the same lane maths as ObstacleBuilder. A gap must be wide enough to pass (2.5 marble diameters) or too narrow to
        /// trap one (0.7 diameters); anything between wedges a marble. Also warns when an obstacle sits on a section under 4 degrees.
        /// </summary>
        static void AppendObstacleChecks(System.Text.StringBuilder text)
        {
            float diameter = LavaRunLayouts.MarbleRadius * 2f;
            float pass = diameter * 2.5f - 0.01f;

            // A pinwheel tip sweeps past a rail or divider rather than sitting still, so it only needs 1.5 diameters (0.33 m) to
            // keep a marble from pinching, not the 2.5 a static peg needs.
            float spinPass = diameter * 1.5f;
                        foreach (StageData s in _stages)
            {
                foreach (TrackObstacle o in s.layout.obstacles)
                {
                    if (o.section < 0 || o.section >= s.layout.sections.Length)
                    {
                        continue;
                    }

                    TrackSection section = s.layout.sections[o.section];
                    float distance = SectionStart(s.world, o.section) + o.offset;
                    Sample frame = TrackMeshBuilder.At(s.world, distance + 1f);
                    float half = frame.width * 0.5f;
                    string prefix = $"    obstacle '{o.label}' ({o.kind}, stage {s.index + 1}):";

                    if (section.slopeDegrees < 4f && o.kind != ObstacleKind.Bumps)
                    {
                        _warnings++;
                        text.AppendLine($"  WARNING:{prefix} sits on a {section.slopeDegrees:0.0} degree section, under 4.");
                    }

                    if (o.kind == ObstacleKind.PegGrid || o.kind == ObstacleKind.PegPyramid)
                    {
                        float radius = o.size > 0f ? o.size : 0.05f;
                        float spacing = Mathf.Max(0.3f, o.spacing > 0f ? o.spacing : 0.46f);
                        float span = (half - 0.12f) - (-half + 0.12f);
                        int fit = Mathf.FloorToInt(span / spacing) + 1;
                        int rows = Mathf.Max(1, o.count);
                        bool pyramid = o.kind == ObstacleKind.PegPyramid;
                        if (pyramid)
                        {
                            rows = Mathf.Min(rows, fit);
                        }

                        float pegGap = spacing - radius * 2f;
                        float minRail = float.MaxValue;
                        var railGaps = new System.Collections.Generic.List<float>();
                        for (int row = 0; row < rows; row++)
                        {
                            int pegs = pyramid ? row + 1 : (row % 2 == 0 ? fit : fit - 1);
                            if (pegs < 1)
                            {
                                continue;
                            }

                            float outer = (pegs - 1) * 0.5f * spacing;
                            float railGap = half - outer - radius;
                            railGaps.Add(railGap);
                            minRail = Mathf.Min(minRail, railGap);
                        }

                        bool pegOk = pegGap >= pass;
                        bool railOk = true;
                        foreach (float gap in railGaps)
                        {
                            railOk &= gap >= pass; // hard rule: nothing narrower than 2.5 diameters beside a rail
                        }

                        text.AppendLine($"{prefix} {rows} rows, {fit} pegs wide, slope {section.slopeDegrees:0.#} deg, peg gap {pegGap:0.00} m, " +
                                        $"rail gaps {FormatGaps(railGaps)} m (min {minRail:0.00}); minimum {pass:0.00}");
                        if (!pegOk)
                        {
                            _warnings++;
                            text.AppendLine($"  WARNING:{prefix} the peg-to-peg gap {pegGap:0.00} m is under {pass:0.00} m.");
                        }

                        if (!railOk)
                        {
                            ObstacleError(text, $"{prefix} a peg-to-rail gap is under {pass:0.00} m (min {minRail:0.00} m) at this section's width. " +
                                                "Use a pyramid of 3 rows, a wider track or a smaller spacing, then Reset Lava Run Layouts.");
                        }
                    }
                    else if (o.kind == ObstacleKind.Divider)
                    {
                        float thickness = o.size > 0f ? o.size : 0.08f;
                        Sample mid = TrackMeshBuilder.At(s.world, distance + Mathf.Max(1f, o.length) * 0.5f);
                        float midHalf = mid.width * 0.5f;
                        float left = midHalf + (o.lateral - thickness * 0.5f);
                        float rightLane = midHalf - (o.lateral + thickness * 0.5f);
                        text.AppendLine($"{prefix} track {mid.width:0.00} m, lanes {left:0.00} m (left) and {rightLane:0.00} m (right), slope {section.slopeDegrees:0.#} deg");
                        if (left < 0.6f || rightLane < 0.6f)
                        {
                            ObstacleError(text, $"{prefix} a lane is under 0.60 m (left {left:0.00}, right {rightLane:0.00}): two marbles abreast can arch and jam.");
                        }

                        foreach (TrackObstacle other in s.layout.obstacles)
                        {
                            if (other.kind != ObstacleKind.Pinwheel || other.section != o.section ||
                                other.offset < o.offset - 1f || other.offset > o.offset + o.length + 1f)
                            {
                                continue;
                            }

                            float arm = other.size > 0f ? other.size : midHalf - 0.045f;
                            float tip = (o.lateral - thickness * 0.5f) - arm;
                            text.AppendLine($"    pinwheel '{other.label}' arm {arm:0.00} m, tip-to-divider clearance {tip:0.00} m");
                            if (tip < spinPass)
                            {
                                ObstacleError(text, $"{prefix} pinwheel '{other.label}' tip is {tip:0.00} m from the divider, under {spinPass:0.00} m.");
                            }
                        }
                    }
                    else if (o.kind == ObstacleKind.Pinwheel)
                    {
                        float arm = o.size > 0f ? o.size : half - 0.045f;
                        float railGap = half - arm;
                        text.AppendLine($"{prefix} arm {arm:0.00} m, tip-to-rail gap {railGap:0.00} m, slope {section.slopeDegrees:0.#} deg");
                        if (railGap < spinPass && railGap > 0.02f)
                        {
                            ObstacleError(text, $"{prefix} the arm tip is {railGap:0.00} m from the rail: it must be at least {spinPass:0.00} m, or span wall to wall (under 0.02 m).");
                        }
                    }
                    else if (o.kind == ObstacleKind.Bottleneck)
                    {
                        float length = Mathf.Max(2f, o.length > 0f ? o.length : 4f);
                        float gapWidth = Mathf.Max(0.3f, o.size > 0f ? o.size : 0.5f);
                        float taper = Mathf.Max(0.5f, (length - 0.5f) * 0.5f);
                        float reach = half - gapWidth * 0.5f;
                        float angle = Mathf.Atan2(reach, taper) * Mathf.Rad2Deg;
                        text.AppendLine($"{prefix} taper {taper:0.0} m, wall angle to the flow {angle:0.#} deg, neck {gapWidth:0.00} m, slope {section.slopeDegrees:0.#} deg");
                        if (angle > 35f)
                        {
                            ObstacleError(text, $"{prefix} the wall meets the flow at {angle:0} deg (over 35): marbles pin against it. Lengthen the taper.");
                        }

                        if (gapWidth < pass)
                        {
                            ObstacleError(text, $"{prefix} neck {gapWidth:0.00} m is under {pass:0.00} m.");
                        }
                    }
                    else if (o.kind == ObstacleKind.Funnel)
                    {
                        float taper = Mathf.Max(1f, o.length > 0f ? o.length : 3f);
                        float gapWidth = Mathf.Max(0.3f, o.size > 0f ? o.size : 1.4f);
                        float reach = half - gapWidth * 0.5f;
                        float angle = Mathf.Atan2(reach, taper) * Mathf.Rad2Deg;
                        text.AppendLine($"{prefix} taper {taper:0.0} m, wall angle to the flow {angle:0.#} deg, exit gap {gapWidth:0.00} m, slope {section.slopeDegrees:0.#} deg");
                        if (angle > 35f)
                        {
                            ObstacleError(text, $"{prefix} the wall meets the flow at {angle:0} deg (over 35): marbles pin against it. Lengthen the taper.");
                        }

                        if (gapWidth < pass * 2f)
                        {
                            ObstacleError(text, $"{prefix} exit gap {gapWidth:0.00} m is under {pass * 2f:0.00} m: a big pack arches in it.");
                        }
                    }
                    else if (o.kind == ObstacleKind.Chicane)
                    {
                        ObstacleError(text, $"{prefix} chicane baffles face the flow flat and pin the pack. Remove them; use a pinwheel or a long tapered bottleneck.");
                        float fraction = Mathf.Clamp(o.size > 0f ? o.size : 0.6f, 0.2f, 0.8f);
                        float gap = frame.width - frame.width * fraction;
                        text.AppendLine($"{prefix} {o.count} baffles, slope {section.slopeDegrees:0.#} deg, tip-to-rail gap {gap:0.00} m");
                        if (gap < pass)
                        {
                            ObstacleError(text, $"{prefix} tip-to-rail gap {gap:0.00} m is under {pass:0.00} m.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lateral floor tilt per section (the sample right vector against world up). A tilted floor rolls the whole pack onto the low wall,
        /// so anything marbles run on must be level, hairpins excepted (up to 1.5 degrees). For a hairpin the log also shows the bank
        /// that v = 3 m/s would need, atan(v^2 / (r g)), which we deliberately do not build: the outer rail holds the pack there.
        /// </summary>
        static void AppendTiltChecks(System.Text.StringBuilder text)
        {
            foreach (StageData s in _stages)
            {
                for (int k = 0; k < s.layout.sections.Length; k++)
                {
                    TrackSection section = s.layout.sections[k];
                    float maxTilt = 0f;
                    foreach (Sample sample in s.world)
                    {
                        if (sample.section == k)
                        {
                            maxTilt = Mathf.Max(maxTilt, Mathf.Abs(Mathf.Asin(Mathf.Clamp(sample.right.y, -1f, 1f))) * Mathf.Rad2Deg);
                        }
                    }

                    bool hairpin = section.label != null && section.label.Contains("Hairpin");
                    float limit = hairpin ? 1.55f : 2f;
                    string need = string.Empty;
                    if (hairpin && section.radius > 0.1f)
                    {
                        float needed = Mathf.Atan(9f / (section.radius * 9.81f)) * Mathf.Rad2Deg;
                        need = $" (v=3 m/s would need {needed:0.0} deg; not built)";
                    }

                    text.AppendLine($"    tilt stage {s.index + 1} '{section.label}': max lateral tilt {maxTilt:0.0} deg, slope {section.slopeDegrees:0.#} deg{need}");
                    if (maxTilt > limit)
                    {
                        ObstacleError(text, $"    stage {s.index + 1} '{section.label}' tilts {maxTilt:0.0} deg sideways (limit {limit:0.0}): the pack will ride the low wall.");
                    }
                }
            }
        }

        static void ObstacleError(System.Text.StringBuilder text, string message)
        {
            _obstacleErrors++;
            text.AppendLine($"  ERROR:{message}");
        }

        static string FormatGaps(System.Collections.Generic.List<float> gaps)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (float gap in gaps)
            {
                string value = gap.ToString("0.00");
                if (!parts.Contains(value))
                {
                    parts.Add(value);
                }
            }

            return string.Join("/", parts);
        }

        /// <summary>Rolling-sphere run time with linear damping: v' = a - c v. Optimistic: ignores pegs, baffles and jams.</summary>
        static float DampedSeconds(List<Sample> samples, float untilDistance, float damping)
        {
            float v = 0f;
            float t = 0f;
            for (int i = 1; i < samples.Count && samples[i].distance <= untilDistance; i++)
            {
                float step = samples[i].distance - samples[i - 1].distance;
                float slope = Mathf.Clamp((samples[i - 1].position.y - samples[i].position.y) / Mathf.Max(1e-4f, step), 0f, 1f);
                float a = 0.714f * 9.81f * slope * 0.85f;
                float dt = step / Mathf.Max(0.3f, v);
                v = Mathf.Max(0f, v + (a - damping * v) * dt);
                t += step / Mathf.Max(0.4f, v);
            }

            return t;
        }

        // ------------------------------------------------------------------ terrain function

        /// <summary>
        /// The volcano's height at (x, z): a cone with a crater, lumped by low-frequency noise, then pulled to a ledge under
        /// every nearby stretch of track so the tracks sit on terraces cut (or built up) into the face.
        /// </summary>
        sealed class VolcanoTerrain
        {
            const float CellSize = 12f;
            const float BlendDistance = 12f;

            readonly float _rimY;
            readonly float _slope;
            readonly List<Vector4> _samples = new List<Vector4>();
            readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public VolcanoTerrain(float rimY, float slope, StageData[] stages)
            {
                _rimY = rimY;
                _slope = slope;
                foreach (StageData stage in stages)
                {
                    for (int i = 0; i < stage.world.Count; i += 2)
                    {
                        Sample sample = stage.world[i];
                        // The bridge and its approach run out over the lake; only its pens, funnel and turn belong to the cone.
                        if (stage.basinSection < 0 && sample.section > LavaRunLayouts.LeadSection)
                        {
                            continue;
                        }

                        var entry = new Vector4(sample.position.x, sample.position.z, sample.position.y, sample.width * 0.5f + 0.12f);
                        int index = _samples.Count;
                        _samples.Add(entry);
                        long key = Key(Mathf.FloorToInt(entry.x / CellSize), Mathf.FloorToInt(entry.y / CellSize));
                        if (!_cells.TryGetValue(key, out List<int> list))
                        {
                            list = new List<int>();
                            _cells[key] = list;
                        }

                        list.Add(index);
                    }
                }
            }

            static long Key(int cx, int cz)
            {
                return ((long)(cx + 20000) << 24) | (long)(cz + 20000);
            }

            float Cone(float x, float z)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float floorY = _rimY - 3.6f;
                float h;
                if (r < 8f)
                {
                    h = floorY;
                }
                else if (r < 15f)
                {
                    h = Mathf.Lerp(floorY, _rimY, Mathf.SmoothStep(0f, 1f, (r - 8f) / 7f));
                }
                else if (r < 19f)
                {
                    h = _rimY;
                }
                else
                {
                    h = _rimY - (r - 19f) * _slope;
                    float lump = Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 9.7f) - 0.5f;
                    h += lump * 5f * Mathf.Clamp01((r - 19f) / 12f);
                }

                return Mathf.Max(h, -2.5f);
            }

            public float Height(float x, float z)
            {
                float cone = Cone(x, z);
                int cx = Mathf.FloorToInt(x / CellSize);
                int cz = Mathf.FloorToInt(z / CellSize);

                // Nearest track by distance to its edge (so a wide pen pad counts from its rim, not its centre).
                float best = float.MaxValue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out List<int> list))
                        {
                            continue;
                        }

                        foreach (int index in list)
                        {
                            Vector4 s = _samples[index];
                            float gap = Mathf.Sqrt((s.x - x) * (s.x - x) + (s.y - z) * (s.y - z)) - s.w;
                            if (gap < best)
                            {
                                best = gap;
                            }
                        }
                    }
                }

                if (best > BlendDistance + 1.5f)
                {
                    return cone;
                }

                // Where tracks stack, the lowest one governs the ground.
                float ledge = float.MaxValue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out List<int> list))
                        {
                            continue;
                        }

                        foreach (int index in list)
                        {
                            Vector4 s = _samples[index];
                            float gap = Mathf.Sqrt((s.x - x) * (s.x - x) + (s.y - z) * (s.y - z)) - s.w;
                            if (gap <= best + 3f && s.z < ledge)
                            {
                                ledge = s.z;
                            }
                        }
                    }
                }

                ledge -= 0.9f;
                float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((best - 1f) / BlendDistance));
                return Mathf.Lerp(cone, ledge, weight);
            }
        }
    }
}
