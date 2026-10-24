using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the seven glass tubes under the sorter's drop points. Each tube stands on the floor (world y=0) with a
    /// colour band, a chrome fill-line ring at the predicted capacity height, a plinth and a world-space TMP name decal.
    /// </summary>
    public static class TubeRackBuilder
    {
        public const float InnerRadius = 0.6f;
        /// <summary>Top of the white floor disk, metres above the floor.</summary>
        public const float FloorTop = 0.09f;
        const float GlassBottom = 0.05f;

        /// <summary>Packing fraction of the settled pile: random close pack is ~0.64 in bulk, the glass wall (tube is only 9 balls wide) pulls it to ~0.60.</summary>
        public const float PackingFraction = 0.60f;
        /// <summary>Clear glass above the fill line: room for the pile cone under the falling stream and the post-win outro.</summary>
        public const float Headroom = 0.6f;

        /// <summary>
        /// Fill line height (world y): where <c>tubeCapacity</c> balls settle. Ball volume V = 4/3 pi r^3 (r 0.065: 1.150 L),
        /// pile volume = capacity * V / PackingFraction, tube area A = pi * InnerRadius^2 (1.131 m^2):
        /// 1000 balls -> 1.150 m^3 / 0.60 = 1.917 m^3 -> 1.695 m of pile + the floor disk top (0.09) = 1.785 m.
        /// </summary>
        public static float FillHeight(MachineConfig config)
        {
            float r = config.ballRadius;
            float ballVolume = 4f / 3f * Mathf.PI * r * r * r;
            float area = Mathf.PI * InnerRadius * InnerRadius;
            return FloorTop + config.tubeCapacity * ballVolume / PackingFraction / area;
        }

        /// <summary>Rim height (world y) = fill line + <see cref="Headroom"/>.</summary>
        public static float TubeHeight(MachineConfig config)
        {
            return FillHeight(config) + Headroom;
        }

        public static TubeRack Build(Transform parent, SorterStation sorter, MachineKit kit, MachineConfig config)
        {
            int n = config.ColourCount;
            float rimY = TubeHeight(config);
            float fillY = FillHeight(config);

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < n; i++) centre += sorter.dropPoints[i].position;
            centre /= n;
            centre.y = 0f;

            // Rack local +X runs along the flow, so local -Z is the sorter's right-hand (camera) side.
            float yaw = sorter.transform.eulerAngles.y - 90f;
            var root = new GameObject("Tube Rack");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            var rack = root.AddComponent<TubeRack>();
            var t = root.transform;

            Mesh glassMesh = null, bandMesh = null, ringMesh = null;
            var centres = new Vector3[n];
            var floors = new float[n];
            var fills = new float[n];
            var rims = new float[n];
            var rings = new Renderer[n];
            var colours = new Color[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 dp = sorter.dropPoints[i].position;
                Vector3 c = t.InverseTransformPoint(new Vector3(dp.x, 0f, dp.z));
                centres[i] = c;
                floors[i] = FloorTop;
                fills[i] = fillY;
                rims[i] = rimY;
                colours[i] = config.colours[i].color;
                var tube = MachineGeometry.Group(t, "Tube " + i + " " + config.colours[i].name);
                tube.transform.localPosition = c;
                var tt = tube.transform;

                MachineGeometry.Cylinder(tt, "Base Ring", new Vector3(0f, 0.025f, 0f), Quaternion.identity,
                    InnerRadius + 0.1f, 0.05f, kit.castIron, false);
                var disk = MachineGeometry.Cylinder(tt, "Floor Disk", new Vector3(0f, 0.07f, 0f), Quaternion.identity,
                    InnerRadius + 0.02f, 0.04f, kit.whiteLacquer, false);
                var floorBox = disk.AddComponent<BoxCollider>();
                floorBox.size = new Vector3(0.99f, 2f, 0.99f);
                floorBox.sharedMaterial = MachineGeometry.WallPhysics;

                // Glass wall: static non-convex mesh collider, the one mesh shared by all seven tubes.
                GameObject glass;
                if (glassMesh == null)
                {
                    glass = MachineGeometry.MeshObject(tt, "Glass", MachineGeometry.OpenCylinder(InnerRadius, rimY - GlassBottom, 48, true),
                        "TubeRack_Glass", kit.glass, true, false);
                    glassMesh = glass.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    glass = SharedMeshObject(tt, "Glass", glassMesh, kit.glass, true);
                }

                glass.transform.localPosition = new Vector3(0f, GlassBottom, 0f);

                GameObject band;
                if (bandMesh == null)
                {
                    band = MachineGeometry.MeshObject(tt, "Colour Band", MachineGeometry.OpenCylinder(InnerRadius + 0.012f, 0.4f, 48, true),
                        "TubeRack_Band", kit.ballMaterials[i], false);
                    bandMesh = band.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    band = SharedMeshObject(tt, "Colour Band", bandMesh, kit.ballMaterials[i], false);
                }

                band.transform.localPosition = new Vector3(0f, FloorTop, 0f);

                GameObject fill;
                if (ringMesh == null)
                {
                    fill = MachineGeometry.MeshObject(tt, "Fill Line Ring", MachineGeometry.OpenCylinder(InnerRadius + 0.03f, 0.05f, 48, true),
                        "TubeRack_Ring", kit.chrome, false);
                    ringMesh = fill.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    fill = SharedMeshObject(tt, "Fill Line Ring", ringMesh, kit.chrome, false);
                }

                fill.transform.localPosition = new Vector3(0f, fillY - 0.025f, 0f);
                rings[i] = fill.GetComponent<Renderer>();
                var rim = SharedMeshObject(tt, "Rim Ring", ringMesh, kit.chrome, false);
                rim.transform.localPosition = new Vector3(0f, rimY - 0.05f, 0f);

                // Name decal on a plinth in front of the tube (rack -Z, the camera side).
                float plinthZ = -(InnerRadius + 0.55f);
                MachineGeometry.Box(tt, "Plinth", new Vector3(0f, 0.275f, plinthZ), Quaternion.identity,
                    new Vector3(1.3f, 0.55f, 0.14f), kit.whiteLacquer, false);
                var label = new GameObject("Name Decal");
                label.transform.SetParent(tt, false);
                label.transform.localPosition = new Vector3(0f, 0.275f, plinthZ - 0.075f);
                var tmp = label.AddComponent<TextMeshPro>();
                tmp.font = TMP_Settings.defaultFontAsset;
                tmp.text = config.colours[i].name;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.16f, 0.16f, 0.17f);
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 1.5f;
                tmp.fontSizeMax = 4f; // 3D TMP: em = size * 0.1 m, so cap height is about 0.28 m
                tmp.fontSize = 4f;
                tmp.rectTransform.sizeDelta = new Vector2(1.25f, 0.45f);
            }

            rack.tubeLocalCentres = centres;
            rack.innerRadius = InnerRadius;
            rack.floorHeights = floors;
            rack.fillHeights = fills;
            rack.rimHeights = rims;
            rack.fillRings = rings;
            rack.glowMaterial = kit.emissive;
            rack.tubeColours = colours;

            rack.displayName = "Tube Rack";
            var top = MachineGeometry.Anchor(t, "Entry", new Vector3(centres[0].x, rimY, centres[0].z));
            rack.entry = top;
            rack.exit = top;
            rack.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, rimY * 0.5f, 0f));
            rack.cameraOffset = t.rotation * new Vector3(0f, 1.5f, -9f);
            rack.tourSeconds = 8f;

            var bounds = new Bounds(t.TransformPoint(centres[0]), Vector3.zero);
            for (int i = 0; i < n; i++)
            {
                Vector3 w = t.TransformPoint(centres[i]);
                bounds.Encapsulate(new Vector3(w.x - InnerRadius, 0f, w.z - InnerRadius));
                bounds.Encapsulate(new Vector3(w.x + InnerRadius, rimY, w.z + InnerRadius));
            }

            rack.activityBounds = bounds;
            return rack;
        }

        static GameObject SharedMeshObject(Transform parent, string name, Mesh mesh, Material material, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.sharedMaterial = MachineGeometry.WallPhysics;
            }

            return go;
        }
    }
}
