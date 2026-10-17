using System.Collections.Generic;
using System.IO;
using SimulationLobby.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.Territory.EditorTools
{
    /// <summary>
    /// The "toy table" look for <c>terr</c> (plans/videos/terr-001, Look v2 + v3): one charcoal table carrying the
    /// grid, four flush boards and the drum stand, on a floor of raised purple slabs, under one soft key and
    /// bright flat ambient, with its own post profile. Deliberately independent of the dark-studio kit.
    /// </summary>
    static class TerritoryArenaBuilder
    {
        public const string MaterialFolder = "Assets/Settings/Studio";
        const string ConfigFolder = "Assets/Settings/Configs/Territory";

        // --- Table dimensions (metres). ---
        public const float TableTop = 0.5f;
        public const float TableMargin = 0.5f;
        public const float TableCornerRadius = 0.5f;
        public const float GridRim = 0.35f;
        public const float GridRimHeight = 0.2f;
        public const float BoardGap = 0.12f;

        // --- Drum and stand (metres). ---
        public const float DrumRadius = 1.1f;
        public const float DrumWidth = 3.2f;
        const float DrumLift = 2.2f;          // drum centre above the table top
        const float StandPostRadius = 0.2f;
        const float StandBarRadius = 0.17f;
        const float DrumBehindGrid = 1.4f;    // drum centre this far behind the frame's far edge
        const float TableFarMargin = 1.5f;    // table continues this far past the drum

        // --- Camera (Look v3): low, close, perspective; fitted by projecting points. ---
        public const float CameraPitch = 40f;
        public const float CameraFov = 45f;
        const float NearEdgeV = TerritoryHudBuilder.ArenaFloorV;   // grid's near edge: just above the caption band
        const float DrumTopV = 0.95f;         // stand top this far up the frame (5% below the top)

        // --- Floor slabs (RaisedSlabs) and hex tiles (HexTiles). The palette lives in the TerritoryTheme. ---
        const float SlabSize = 1.2f;
        const float SlabGap = 0.08f;
        const int SlabSeed = 20261011;

        /// <summary>Hex cell circumradius (flat-top): the pitch the honeycomb is laid out on.</summary>
        const float HexCell = 0.7f;
        const float HexGap = 0.06f;
        const float HexThickness = 0.6f;

        public struct Layout
        {
            public float gridWidth, gridDepth;
            public Vector3 gridOrigin;
            public float boardCentreX;
            public float boardBottomZ, boardTopZ;
            public float boardInnerX, boardOuterX, boardNearZ;
            public float tableHalfWidth, tableMinZ, tableMaxZ;
            public Vector3 tableCentre;
            public Vector3 drumCentre;
            public float standTopY;
        }

        public struct CameraPose
        {
            public Vector3 position, lookAt, forward, up;
            public float tanHalf, aspect;
            public float distance;
            public float nearEdgeV, drumTopV, boardCrop;
        }

        public struct DrumRig
        {
            public DrumReelView drum;
            public Transform headerAnchor, resultAnchor;
        }

        public static Layout ComputeLayout(TerritoryConfig config)
        {
            var layout = new Layout();
            layout.gridWidth = config.gridWidth * config.cellSize;
            layout.gridDepth = config.gridHeight * config.cellSize;
            layout.gridOrigin = new Vector3(-layout.gridWidth * 0.5f, TableTop, 0f);
            layout.boardCentreX = layout.gridWidth * 0.5f + GridRim + BoardGap + config.boardWidth * 0.5f;
            layout.boardInnerX = layout.boardCentreX - config.boardWidth * 0.5f;
            layout.boardOuterX = layout.boardCentreX + config.boardWidth * 0.5f;

            float quadrant = layout.gridDepth * 0.5f;
            layout.boardBottomZ = quadrant * 0.5f;
            layout.boardTopZ = layout.gridDepth - quadrant * 0.5f;
            layout.boardNearZ = layout.boardBottomZ - config.boardLength * 0.5f;
            float boardDepth = config.boardLength + 2f * TerritoryBoardBuilder.WallThickness;
            if (boardDepth > quadrant)
            {
                Debug.LogWarning($"[terr] A board is {boardDepth:0.00}m deep but a quadrant is {quadrant:0.00}m: boards will overlap. " +
                                 "Lower boardLength or raise gridHeight.");
            }

            float drumZ = layout.gridDepth + GridRim + DrumBehindGrid;
            layout.drumCentre = new Vector3(0f, TableTop + DrumLift, drumZ);
            layout.standTopY = layout.drumCentre.y + DrumRadius + 0.3f + StandBarRadius;

            layout.tableHalfWidth = layout.boardCentreX + config.boardWidth * 0.5f + TableMargin;
            layout.tableMinZ = -TableMargin;
            layout.tableMaxZ = drumZ + DrumRadius + TableFarMargin;
            layout.tableCentre = new Vector3(0f, TableTop * 0.5f, (layout.tableMinZ + layout.tableMaxZ) * 0.5f);
            return layout;
        }

        // ------------------------------------------------------------------ table

        public static void BuildTable(Layout layout, Material charcoal)
        {
            var root = new GameObject("Table").transform;
            float width = layout.tableHalfWidth * 2f;
            float depth = layout.tableMaxZ - layout.tableMinZ;
            Vector3 centre = new Vector3(0f, TableTop * 0.5f, (layout.tableMinZ + layout.tableMaxZ) * 0.5f);
            RoundedBlock(root, "Table Top", centre, width, depth, TableTop, TableCornerRadius, charcoal);
        }

        /// <summary>A solid slab with rounded plan corners: two crossed cubes and four corner cylinders.</summary>
        static void RoundedBlock(Transform parent, string name, Vector3 centre, float width, float depth, float height,
            float radius, Material material)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = centre;
            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Core X", root, Vector3.zero,
                new Vector3(width - 2f * radius, height, depth), material, false, null);
            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Core Z", root, Vector3.zero,
                new Vector3(width, height, depth - 2f * radius), material, false, null);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    TerritoryBoardBuilder.Prim(PrimitiveType.Cylinder, "Corner", root,
                        new Vector3(sx * (width * 0.5f - radius), 0f, sz * (depth * 0.5f - radius)),
                        new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, false, null);
                }
            }
        }

        /// <summary>The thick rounded charcoal rim around the tile grid.</summary>
        public static void BuildGridFrame(Layout layout, Material charcoal)
        {
            var root = new GameObject("Grid Frame").transform;
            float outerW = layout.gridWidth + 2f * GridRim;
            float outerD = layout.gridDepth + 2f * GridRim;
            float z0 = layout.gridDepth * 0.5f;
            float y = TableTop + GridRimHeight * 0.5f;
            float r = GridRim * 0.5f;

            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Frame Near", root, new Vector3(0f, y, z0 - outerD * 0.5f + r),
                new Vector3(outerW - GridRim, GridRimHeight, GridRim), charcoal, false, null);
            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Frame Far", root, new Vector3(0f, y, z0 + outerD * 0.5f - r),
                new Vector3(outerW - GridRim, GridRimHeight, GridRim), charcoal, false, null);
            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Frame Left", root, new Vector3(-outerW * 0.5f + r, y, z0),
                new Vector3(GridRim, GridRimHeight, outerD - GridRim), charcoal, false, null);
            TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Frame Right", root, new Vector3(outerW * 0.5f - r, y, z0),
                new Vector3(GridRim, GridRimHeight, outerD - GridRim), charcoal, false, null);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    TerritoryBoardBuilder.Prim(PrimitiveType.Cylinder, "Frame Corner", root,
                        new Vector3(sx * (outerW * 0.5f - r), y, z0 + sz * (outerD * 0.5f - r)),
                        new Vector3(GridRim, GridRimHeight * 0.5f, GridRim), charcoal, false, null);
                }
            }
        }

        // ------------------------------------------------------------------ drum

        /// <summary>
        /// The physical drum behind the grid's far edge: a charcoal arch (two rounded posts and a top bar), the
        /// 3D prism with its decal labels, a yellow arrow pointing at its face from the left, and two anchors
        /// (left and right of the drum, at face height) for the screen labels.
        /// </summary>
        public static DrumRig BuildDrum(Layout layout, TerritoryConfig config, Material charcoal)
        {
            Vector3 c = layout.drumCentre;
            var root = new GameObject("Drum Stand").transform;

            // Arch: posts stand on the table either side of the drum, the bar spans their tops.
            float postX = DrumWidth * 0.5f + 0.15f + StandPostRadius;
            float postTop = layout.standTopY - StandBarRadius;
            float postHeight = postTop - TableTop;
            var accents = new List<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                accents.Add(TerritoryBoardBuilder.Prim(PrimitiveType.Cylinder, "Post", root,
                    new Vector3(side * postX, TableTop + postHeight * 0.5f, c.z),
                    new Vector3(StandPostRadius * 2f, postHeight * 0.5f, StandPostRadius * 2f), charcoal, false, null)
                    .GetComponent<Renderer>());
                accents.Add(TerritoryBoardBuilder.Prim(PrimitiveType.Sphere, "Post Foot", root,
                    new Vector3(side * postX, TableTop, c.z), Vector3.one * (StandPostRadius * 2.4f), charcoal, false, null)
                    .GetComponent<Renderer>());
                accents.Add(TerritoryBoardBuilder.Prim(PrimitiveType.Sphere, "Post Knuckle", root,
                    new Vector3(side * postX, postTop, c.z), Vector3.one * (StandPostRadius * 2f), charcoal, false, null)
                    .GetComponent<Renderer>());
            }

            GameObject bar = TerritoryBoardBuilder.Prim(PrimitiveType.Cylinder, "Top Bar", root,
                new Vector3(0f, postTop, c.z), new Vector3(StandBarRadius * 2f, postX, StandBarRadius * 2f), charcoal, false, null);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            accents.Add(bar.GetComponent<Renderer>());

            // The drum: local -Z faces the camera (the camera sits at -Z), so the default rotation is right.
            var drumObject = new GameObject("Drum");
            drumObject.transform.position = c;
            DrumReelView drum = drumObject.AddComponent<DrumReelView>();
            drum.accentRenderers = accents.ToArray();
            drum.Build(config.spinFaces.Length, DrumRadius, DrumWidth);
            float faceHeight = 2f * DrumRadius * Mathf.Sin(Mathf.PI / Mathf.Max(3, config.spinFaces.Length));
            Debug.Log($"[terr] Drum: {config.spinFaces.Length} faces, radius {DrumRadius:0.00}m, face {DrumWidth:0.00}m wide x {faceHeight:0.000}m tall.");
            if (faceHeight < 0.75f)
            {
                Debug.LogWarning($"[terr] Drum faces are {faceHeight:0.000}m tall (< 0.75m): raise DrumRadius for {config.spinFaces.Length} faces.");
            }

            TerritoryTheme theme = config.Theme;
            drum.Body.sharedMaterial = ThemedLit(theme, "DrumBody", theme.drumBody, 0.8f);
            var labels = new string[config.spinFaces.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = config.spinFaces[i].Label;
            }

            drum.SetFaces(labels);
            drum.SetIdle(0);

            // Arrow: flat yellow, in front of the post, tip just left of the drum, pointing along +X at the face.
            Material yellow = ThemedLit(theme, "Arrow", theme.drumArrow, 0.5f);
            float arrowZ = c.z - DrumRadius - 0.25f;
            float tipX = -DrumWidth * 0.5f - 0.1f;
            const float headLength = 0.8f;
            const float shaftLength = 1.3f;
            const float depth = 0.28f;
            ArrowPiece(root, "Arrow Shaft", yellow, new Vector3(tipX - headLength - shaftLength * 0.5f, c.y, arrowZ),
                new[]
                {
                    new Vector2(-shaftLength * 0.5f, -0.17f), new Vector2(shaftLength * 0.5f, -0.17f),
                    new Vector2(shaftLength * 0.5f, 0.17f), new Vector2(-shaftLength * 0.5f, 0.17f)
                }, depth);
            ArrowPiece(root, "Arrow Head", yellow, new Vector3(tipX - headLength * 0.5f, c.y, arrowZ),
                new[]
                {
                    new Vector2(-headLength * 0.5f, -0.5f), new Vector2(headLength * 0.5f, 0f),
                    new Vector2(-headLength * 0.5f, 0.5f)
                }, depth);

            // Screen-label anchors (edge-aligned labels): header right edge 0.3 m left of the arrow tail, result left edge 0.4 m right of the drum end cap.
            var header = new GameObject("Drum Header Anchor").transform;
            header.position = new Vector3(tipX - headLength - shaftLength - 0.3f, c.y, arrowZ);
            var result = new GameObject("Drum Result Anchor").transform;
            // Left-aligned at this point, so it must clear the right stand post, not just the drum's end cap.
            result.position = new Vector3(c.x + postX + StandPostRadius * 1.2f + 0.35f, c.y, arrowZ);

            return new DrumRig { drum = drum, headerAnchor = header, resultAnchor = result };
        }

        static void ArrowPiece(Transform parent, string name, Material material, Vector3 position, Vector2[] outline, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = PrismMesh(outline, depth, name);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>A convex outline (XY plane) extruded along Z into a flat-shaded prism. Lives in the scene, not as an asset.</summary>
        static Mesh PrismMesh(Vector2[] outline, float depth, string name)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float h = depth * 0.5f;
            int n = outline.Length;

            void Face(Vector3 normal, params Vector3[] poly)
            {
                int start = vertices.Count;
                vertices.AddRange(poly);
                for (int i = 1; i < poly.Length - 1; i++)
                {
                    // Unity front faces are clockwise: flip any triangle whose winding disagrees with the intended normal.
                    Vector3 cross = Vector3.Cross(poly[i] - poly[0], poly[i + 1] - poly[0]);
                    if (Vector3.Dot(cross, normal) >= 0f)
                    {
                        triangles.Add(start);
                        triangles.Add(start + i);
                        triangles.Add(start + i + 1);
                    }
                    else
                    {
                        triangles.Add(start);
                        triangles.Add(start + i + 1);
                        triangles.Add(start + i);
                    }
                }
            }

            var front = new Vector3[n];
            var back = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = new Vector3(outline[i].x, outline[i].y, -h);
                back[i] = new Vector3(outline[i].x, outline[i].y, h);
            }

            Face(Vector3.back, front);
            Face(Vector3.forward, back);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 edge = outline[j] - outline[i];
                Face(new Vector3(edge.y, -edge.x, 0f), front[i], front[j], back[j], back[i]);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment(Layout layout, CameraPose pose, Camera camera, TerritoryTheme theme)
        {
            Color far = Color.Lerp(theme.floorBase, theme.floorAlt, 0.5f);
            BuildFloor(layout, pose, theme);

            // Flat, bright ambient and subtle linear fog that blends into the camera background.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = far;
            RenderSettings.fogStartDistance = pose.distance * 1.3f;
            RenderSettings.fogEndDistance = pose.distance * 4.5f;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = far;
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            // One soft key from above, front and left (camera side is -Z). No other lights.
            var lights = new GameObject("Territory Lights").transform;
            var keyObject = new GameObject("Key");
            keyObject.transform.SetParent(lights, false);
            keyObject.transform.rotation = Quaternion.Euler(52f, 32f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = theme.keyColor;
            key.intensity = theme.keyIntensity;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = theme.keyShadowStrength;

            if (theme.rimIntensity > 0f)
            {
                // A cool rim from behind and to the right (the camera sits at -Z): edges the cannons and boards against the floor. No shadows.
                var rimObject = new GameObject("Rim");
                rimObject.transform.SetParent(lights, false);
                rimObject.transform.rotation = Quaternion.Euler(35f, 215f, 0f);
                Light rim = rimObject.AddComponent<Light>();
                rim.type = LightType.Directional;
                rim.color = theme.rimColor;
                rim.intensity = theme.rimIntensity;
                rim.shadows = LightShadows.None;
            }

            if (theme.ambientDust)
            {
                BuildDust(layout, theme);
            }

            BuildPost(theme);
        }

        /// <summary>
        /// Slow motes hanging over the table (the 3D depth cue, reused from esc): a flat field laid horizontally above the
        /// table, tinted like the key light at a low alpha so it never competes with the cannons or the balls.
        /// </summary>
        static void BuildDust(Layout layout, TerritoryTheme theme)
        {
            var dustObject = new GameObject("Ambient Dust");
            dustObject.transform.position = new Vector3(0f, TableTop + 2.5f, layout.tableCentre.z);
            dustObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // the field is XY: lay it flat on XZ
            dustObject.AddComponent<ParticleSystem>();
            AmbientDriftField dust = dustObject.AddComponent<AmbientDriftField>();
            dust.fieldSize = new Vector2(layout.tableHalfWidth * 2f, layout.tableMaxZ - layout.tableMinZ);
            dust.particleCount = 110;
            dust.maxAlpha = 0.1f;
            dust.sizeRange = new Vector2(0.06f, 0.2f);
            dust.tint = theme.keyColor;
            dust.Apply(EnsureDustTexture(theme));
            Debug.Log($"[terr] Ambient dust: {dust.fieldSize.x:0.#} x {dust.fieldSize.y:0.#} m field, {dust.particleCount} motes, alpha {dust.maxAlpha:0.00}.");
        }

        /// <summary>A soft round mote texture saved under the material folder (an asset, so the scene keeps its reference).</summary>
        static Texture2D EnsureDustTexture(TerritoryTheme theme)
        {
            string path = $"{MaterialFolder}/{theme.materialPrefix}DustDot_64.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(MaterialFolder);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float a = Mathf.Clamp01(1f - d);
                    byte alpha = (byte)Mathf.RoundToInt(a * a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Dispatches on the theme's floor pattern: raised square slabs (terr-001) or raised hexagonal tiles (terr-002).
        /// Both sit on a dark grout plane.
        /// </summary>
        static void BuildFloor(Layout layout, CameraPose pose, TerritoryTheme theme)
        {
            Material grout = ThemedLit(theme, "FloorGrout", theme.floorGrout, 0.05f);
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Floor Grout";
            plane.transform.position = new Vector3(0f, -0.02f, layout.tableCentre.z);
            plane.transform.localScale = Vector3.one * 30f;
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.GetComponent<MeshRenderer>().sharedMaterial = grout;

            Material[] shades =
            {
                ThemedLit(theme, "FloorSlab_A", theme.floorBase, 0.1f),
                ThemedLit(theme, "FloorSlab_B", theme.floorAlt, 0.1f),
                ThemedLit(theme, "FloorSlab_C", theme.floorLight, 0.1f)
            };

            if (theme.floorPattern == TerritoryFloorPattern.HexTiles)
            {
                BuildHexFloor(layout, pose, shades);
            }
            else
            {
                BuildSlabFloor(layout, pose, shades);
            }
        }

        /// <summary>
        /// Raised purple slabs on the grout (1.2 m, 0.08 m gaps, small seeded height variance, three shades). Only slabs
        /// that can reach the frame, and are not wholly under the table, are created.
        /// </summary>
        static void BuildSlabFloor(Layout layout, CameraPose pose, Material[] shades)
        {
            var root = new GameObject("Floor Slabs").transform;
            var rng = new System.Random(SlabSeed);
            float pitch = SlabSize + SlabGap;
            const float half = SlabSize * 0.5f;
            const float thickness = 0.6f;
            int made = 0;
            for (int ix = -56; ix <= 56; ix++)
            {
                for (int iz = -30; iz <= 70; iz++)
                {
                    // Draw before culling so a slab's look never depends on the camera.
                    int shade = rng.Next(shades.Length);
                    float top = 0.02f + (float)rng.NextDouble() * 0.16f;

                    float x = ix * pitch;
                    float z = iz * pitch;
                    bool underTable = Mathf.Abs(x) + half <= layout.tableHalfWidth &&
                                      z - half >= layout.tableMinZ && z + half <= layout.tableMaxZ;
                    if (underTable || !InFrame(pose, x, z, half))
                    {
                        continue;
                    }

                    GameObject slab = TerritoryBoardBuilder.Prim(PrimitiveType.Cube, "Slab", root,
                        new Vector3(x, top - thickness * 0.5f, z), new Vector3(SlabSize, thickness, SlabSize),
                        shades[shade], false, null);
                    slab.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(slab, StaticEditorFlags.BatchingStatic);
                    made++;
                }
            }

            Debug.Log($"[terr] Floor: {made} slabs ({SlabSize:0.0} m, {SlabGap:0.00} m gaps), culled to the camera frame.");
        }

        /// <summary>
        /// Flat-top hexagonal tiles in a honeycomb (cell circumradius <see cref="HexCell"/>, <see cref="HexGap"/> gaps):
        /// columns 1.5 R apart, every odd column half a row down. One shared in-scene prism mesh (never an asset), three
        /// shades, seeded height variance drawn before culling so a tile's look never depends on the camera.
        /// </summary>
        static void BuildHexFloor(Layout layout, CameraPose pose, Material[] shades)
        {
            float rowHeight = Mathf.Sqrt(3f) * HexCell;
            float apothem = rowHeight * 0.5f - HexGap * 0.5f;
            float tileRadius = apothem * 2f / Mathf.Sqrt(3f);
            Mesh mesh = HexPrismMesh(tileRadius);

            var root = new GameObject("Floor Hex Tiles").transform;
            var rng = new System.Random(SlabSeed);
            int made = 0;
            for (int ix = -72; ix <= 72; ix++)
            {
                for (int iz = -34; iz <= 78; iz++)
                {
                    int shade = rng.Next(shades.Length);
                    float top = 0.02f + (float)rng.NextDouble() * 0.16f;

                    float x = ix * 1.5f * HexCell;
                    float z = iz * rowHeight + ((ix & 1) != 0 ? rowHeight * 0.5f : 0f);
                    bool underTable = Mathf.Abs(x) + HexCell <= layout.tableHalfWidth &&
                                      z - HexCell >= layout.tableMinZ && z + HexCell <= layout.tableMaxZ;
                    if (underTable || !InFrame(pose, x, z, HexCell))
                    {
                        continue;
                    }

                    var tile = new GameObject("Hex");
                    tile.transform.SetParent(root, false);
                    tile.transform.position = new Vector3(x, top, z);
                    tile.transform.localScale = new Vector3(1f, HexThickness, 1f);
                    tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = tile.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = shades[shade];
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(tile, StaticEditorFlags.BatchingStatic);
                    made++;
                }
            }

            Debug.Log($"[terr] Floor: {made} hex tiles (cell {HexCell:0.0} m, tile circumradius {tileRadius:0.000} m, {HexGap:0.00} m gaps), culled to the camera frame.");
        }

        /// <summary>
        /// A flat-top hexagonal prism, top face at local y = 0 and sides down to y = -1 (scale Y by the thickness). Flat
        /// shaded, no bottom face. Lives in the scene, shared by every tile.
        /// </summary>
        static Mesh HexPrismMesh(float circumradius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            void Face(Vector3 normal, params Vector3[] poly)
            {
                int start = vertices.Count;
                for (int i = 0; i < poly.Length; i++)
                {
                    vertices.Add(poly[i]);
                    normals.Add(normal);
                }

                for (int i = 1; i < poly.Length - 1; i++)
                {
                    // Unity front faces are clockwise: flip any triangle whose winding disagrees with the intended normal.
                    Vector3 cross = Vector3.Cross(poly[i] - poly[0], poly[i + 1] - poly[0]);
                    if (Vector3.Dot(cross, normal) >= 0f)
                    {
                        triangles.Add(start);
                        triangles.Add(start + i);
                        triangles.Add(start + i + 1);
                    }
                    else
                    {
                        triangles.Add(start);
                        triangles.Add(start + i + 1);
                        triangles.Add(start + i);
                    }
                }
            }

            var top = new Vector3[6];
            var bottom = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;   // flat-top: corners at 0, 60, ... degrees about Y
                top[i] = new Vector3(Mathf.Cos(angle) * circumradius, 0f, Mathf.Sin(angle) * circumradius);
                bottom[i] = new Vector3(top[i].x, -1f, top[i].z);
            }

            Face(Vector3.up, top);
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                Vector3 mid = (top[i] + top[j]) * 0.5f;
                Face(new Vector3(mid.x, 0f, mid.z).normalized, top[i], top[j], bottom[j], bottom[i]);
            }

            var mesh = new Mesh { name = "Hex Prism" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>True when any corner of the slab's top projects into the frame (with a margin).</summary>
        static bool InFrame(CameraPose pose, float x, float z, float half)
        {
            const float margin = 0.12f;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int dz = -1; dz <= 1; dz += 2)
                {
                    var p = new Vector3(x + dx * half, 0.1f, z + dz * half);
                    Project(pose.position, pose.forward, pose.up, pose.tanHalf, pose.aspect, p, out float u, out float v, out float depth);
                    if (depth > 0.1f && u > -margin && u < 1f + margin && v > -margin && v < 1f + margin)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static void BuildPost(TerritoryTheme theme)
        {
            Directory.CreateDirectory(ConfigFolder);
            string postPath = $"{ConfigFolder}/{theme.postProfileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(postPath);
            bool created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, postPath);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(theme.bloomIntensity);
            bloom.scatter.Override(0.5f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(theme.vignetteIntensity);
            vignette.smoothness.Override(0.5f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.saturation.Override(theme.saturation);

            EditorUtility.SetDirty(profile);

            var volumeObject = new GameObject("Territory Post");
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

        // ------------------------------------------------------------------ camera

        /// <summary>
        /// Solves a camera position at a fixed pitch and FOV, centred on X, so that the grid frame's near edge
        /// projects <see cref="NearEdgeV"/> above the frame bottom and the stand's top <see cref="DrumTopV"/> up
        /// the frame (Newton's method on the two projected heights). The side-board crop is measured, not solved.
        /// </summary>
        public static CameraPose FitCamera(Layout layout, float aspect)
        {
            float pitch = CameraPitch * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            Vector3 up = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            float tanHalf = Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);

            var nearEdge = new Vector3(0f, TableTop + GridRimHeight, -GridRim);
            var standTop = new Vector3(0f, layout.standTopY, layout.drumCentre.z);

            Vector2 Residual(float cy, float cz)
            {
                var position = new Vector3(0f, cy, cz);
                Project(position, forward, up, tanHalf, aspect, nearEdge, out _, out float v0, out _);
                Project(position, forward, up, tanHalf, aspect, standTop, out _, out float v1, out _);
                return new Vector2(v0 - NearEdgeV, v1 - DrumTopV);
            }

            float y = 10f;
            float z = -12f;
            for (int iteration = 0; iteration < 60; iteration++)
            {
                Vector2 e = Residual(y, z);
                const float h = 1e-3f;
                Vector2 ey = (Residual(y + h, z) - e) / h;
                Vector2 ez = (Residual(y, z + h) - e) / h;
                float det = ey.x * ez.y - ez.x * ey.y;
                if (Mathf.Abs(det) < 1e-9f)
                {
                    break;
                }

                y -= (ez.y * e.x - ez.x * e.y) / det;
                z -= (-ey.y * e.x + ey.x * e.y) / det;
            }

            var pose = new CameraPose
            {
                position = new Vector3(0f, y, z),
                forward = forward,
                up = up,
                tanHalf = tanHalf,
                aspect = aspect
            };
            pose.lookAt = pose.position + forward * Vector3.Dot(layout.tableCentre - pose.position, forward);
            pose.distance = Vector3.Distance(pose.position, pose.lookAt);

            Project(pose.position, forward, up, tanHalf, aspect, nearEdge, out _, out pose.nearEdgeV, out _);
            Project(pose.position, forward, up, tanHalf, aspect, standTop, out _, out pose.drumTopV, out _);
            Project(pose.position, forward, up, tanHalf, aspect, new Vector3(layout.boardOuterX, TableTop, layout.boardNearZ),
                out float uOuter, out _, out _);
            Project(pose.position, forward, up, tanHalf, aspect, new Vector3(layout.boardInnerX, TableTop, layout.boardNearZ),
                out float uInner, out _, out _);
            pose.boardCrop = Mathf.Clamp01((uOuter - 1f) / Mathf.Max(0.0001f, uOuter - uInner));
            return pose;
        }

        /// <summary>Pinhole projection to 0..1 screen coordinates (u right, v up); depth is along the view axis.</summary>
        static void Project(Vector3 position, Vector3 forward, Vector3 up, float tanHalf, float aspect, Vector3 point,
            out float u, out float v, out float depth)
        {
            Vector3 rel = point - position;
            depth = Vector3.Dot(rel, forward);
            float safe = Mathf.Max(depth, 0.0001f);
            u = 0.5f + 0.5f * rel.x / (safe * tanHalf * aspect);
            v = 0.5f + 0.5f * Vector3.Dot(rel, up) / (safe * tanHalf);
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Creates or loads <c>Territory*.mat</c> and re-applies <paramref name="setup"/> so the look stays in code.</summary>
        public static Material EnsureMaterial(string name, System.Action<Material> setup)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError($"[terr] URP Lit shader not found for {name}.");
                }

                material = new Material(shader) { name = name };
                setup(material);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                setup(material);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        public static Material LitMaterial(string name, Color color, float smoothness)
        {
            return EnsureMaterial(name, m =>
            {
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", smoothness);
            });
        }

        /// <summary>A lit material named <c>theme.materialPrefix + suffix</c> (the classic prefix "Territory" keeps terr-001's names).</summary>
        public static Material ThemedLit(TerritoryTheme theme, string suffix, Color color, float smoothness)
        {
            return LitMaterial(theme.materialPrefix + suffix, color, smoothness);
        }

        public static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
