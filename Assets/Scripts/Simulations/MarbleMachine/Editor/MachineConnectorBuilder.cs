using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the U-channel chutes that carry balls between stations: asphalt floor, wood or brick side walls,
    /// chrome posts down to the studio floor. Rounded corners come from smoothing the authored polyline.
    /// </summary>
    public static class MachineConnectorBuilder
    {
        public const float MinSlopeDegrees = 5f;
        /// <summary>Shortest chute wall height for a ball radius: 5.5 r, never under 0.34 m.</summary>
        public static float WallHeightFor(float radius) => Mathf.Max(0.7f, radius * 5.5f);
        public const float MaxSlopeDegrees = 30f;
        /// <summary>Walls grow outward from the ball channel, so the inner width is unchanged. A ball at 12 m/s covers 0.12 m per physics step: thin walls let it through.</summary>
        public const float WallThickness = 0.16f;
        public const float FloorThickness = 0.16f;
        const float SampleSpacing = 0.25f;
        const float PostSpacing = 2.5f;
        /// <summary>Station exit anchors sit 5 cm above the lip floor; starting the chute that much lower removes the step a ball would have to climb.</summary>
        const float StartDip = 0.06f;
        /// <summary>The chute reaches back under the previous station and forward into the next, so its walls overlap theirs and leave no seam.</summary>
        const float OverlapBack = 0.35f;
        const float OverlapForward = 0.25f;


        /// <summary>Warnings logged by the most recent <see cref="Build"/> calls; the scene builder resets and reads it.</summary>
        public static int WarningCount;

        /// <summary>Resets the wall-material alternation and warning count at the start of a scene build.</summary>
        public static void BeginScene()
        {
            WarningCount = 0;
        }

        /// <summary>
        /// Horizontal length of the smoothed chute from <paramref name="from"/> to <paramref name="to"/> via the optional
        /// polyline (heights ignored). The scene builder uses it to set the drop for a target slope.
        /// </summary>
        public static float HorizontalLength(Vector3 from, Vector3 to, IList<Vector3> via = null)
        {
            List<Vector2> smooth = Smooth(RawPath(from, to, via));
            float length = 0f;
            for (int i = 1; i < smooth.Count; i++)
            {
                length += Vector2.Distance(smooth[i], smooth[i - 1]);
            }

            return length;
        }

        public static GameObject Build(Transform parent, string name, Vector3 from, Vector3 to, MachineKit kit,
            MachineConfig config, IList<Vector3> via = null, float[] boostFractions = null)
        {
            var root = new GameObject(name);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            List<Vector2> smooth = Smooth(RawPath(from, to, via));
            float total = 0f;
            var cumulative = new List<float> { 0f };
            for (int i = 1; i < smooth.Count; i++)
            {
                total += Vector2.Distance(smooth[i], smooth[i - 1]);
                cumulative.Add(total);
            }

            // Resample to even spacing and lay the heights down linearly along the run: a constant slope.
            int count = Mathf.Max(2, Mathf.CeilToInt(total / SampleSpacing) + 1);
            var centre = new List<Vector3>(count);
            int seg = 1;
            for (int i = 0; i < count; i++)
            {
                float s = total * i / (count - 1);
                while (seg < smooth.Count - 1 && cumulative[seg] < s)
                {
                    seg++;
                }

                float span = Mathf.Max(1e-5f, cumulative[seg] - cumulative[seg - 1]);
                Vector2 p = Vector2.Lerp(smooth[seg - 1], smooth[seg], Mathf.Clamp01((s - cumulative[seg - 1]) / span));
                float y = Mathf.Lerp(from.y - StartDip, to.y, total > 1e-4f ? s / total : 0f);
                centre.Add(root.transform.InverseTransformPoint(new Vector3(p.x, y, p.y)));
            }

            if (centre.Count >= 2)
            {
                Vector3 dirIn = centre[1] - centre[0];
                dirIn.y = 0f;
                Vector3 dirOut = centre[centre.Count - 1] - centre[centre.Count - 2];
                dirOut.y = 0f;
                // The overlaps carry the chute's own slope on (a flat tail would stall the balls), within a few cm of the stations.
                float slopeIn = dirIn.sqrMagnitude > 1e-6f ? (centre[1].y - centre[0].y) / dirIn.magnitude : 0f;
                float slopeOut = dirOut.sqrMagnitude > 1e-6f
                    ? (centre[centre.Count - 1].y - centre[centre.Count - 2].y) / dirOut.magnitude : 0f;
                if (dirIn.sqrMagnitude > 1e-6f)
                {
                    Vector3 start = centre[0];
                    centre.Insert(0, start - dirIn.normalized * OverlapBack + Vector3.up * Mathf.Min(-slopeIn * OverlapBack, 0.04f));
                }

                if (dirOut.sqrMagnitude > 1e-6f)
                {
                    Vector3 end = centre[centre.Count - 1];
                    centre.Add(end + dirOut.normalized * OverlapForward + Vector3.up * Mathf.Max(slopeOut * OverlapForward, -0.04f));
                }
            }

            CheckSlope(name, from, to, total);

            float width = config != null ? config.chuteWidth : BaselineWidth;
            float radius = config != null ? config.ballRadius : 0.065f;
            // Flood: dense streams ride up the walls on bends, so walls stand at least 5.5 r (0.36 m at r 0.065).
            float wallHeight = WallHeightFor(radius);
            // The chute right under the silo takes the first rush of the whole pile, which overshoots a normal wall.
            if (name.IndexOf("Silo to", System.StringComparison.OrdinalIgnoreCase) >= 0) wallHeight = Mathf.Max(wallHeight, 1.3f);
            // Steep runs carry fast balls up the walls on bends: stand them taller.
            float runSlope = Mathf.Atan2(Mathf.Max(0f, from.y - to.y), Mathf.Max(0.01f, total)) * Mathf.Rad2Deg;
            if (runSlope > 10f) wallHeight = Mathf.Max(wallHeight, 1.0f);
            AddChannel(root.transform, "Conn", Safe(name), centre, width, wallHeight, kit, null, null);

            // Speed-boost pads at the given fractions of the run, lying on the floor along the local direction of travel.
            if (boostFractions != null && config != null && centre.Count > 4)
            {
                for (int k = 0; k < boostFractions.Length; k++)
                {
                    float fraction = boostFractions[k];
                    int i = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fraction) * (centre.Count - 1)), 2, centre.Count - 3);
                    Vector3 fwd = (centre[i + 1] - centre[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 up = Vector3.Cross(fwd, side).normalized;
                    BoostPadBuilder.Build(root.transform, "Boost Pad " + (k + 1), centre[i], Quaternion.LookRotation(fwd, up), 0.8f, kit, config);
                }
            }

            // Chrome posts to the studio floor (y = 0), every ~2.5 m of run.
            int posts = Mathf.Max(1, Mathf.FloorToInt(total / PostSpacing));
            for (int k = 0; k < posts; k++)
            {
                float s = (k + 0.5f) * total / posts;
                int index = Mathf.Clamp(Mathf.RoundToInt(s / total * (count - 1)), 0, count - 1);
                Vector3 world = root.transform.TransformPoint(centre[Mathf.Clamp(index + 1, 0, centre.Count - 1)]);
                float top = world.y - FloorThickness;
                if (top < 0.2f)
                {
                    continue;
                }

                MachineGeometry.Cylinder(root.transform, $"Post {k + 1}", root.transform.InverseTransformPoint(new Vector3(world.x, top * 0.5f, world.z)),
                    Quaternion.identity, 0.05f, top, kit.chrome, false);
            }

            return root;
        }

        /// <summary>The chute width the station builders were authored against, before the 2x widening (they scale their lateral sizes by <see cref="WidthScale"/>).</summary>
        public const float BaselineWidth = 0.9f;

        /// <summary>How much wider the track is than the authored baseline: 2 at <c>chuteWidth</c> 1.8.</summary>
        public static float WidthScale(MachineConfig config) => config != null ? config.chuteWidth / BaselineWidth : 1f;

        /// <summary>
        /// A station's own entry or exit stretch, swept with exactly the profile a connector has (width, wall height, wall and floor
        /// thickness, materials). Points are in <paramref name="parent"/>'s local space and the floor's top surface follows them, so the
        /// joint with the chute that meets it has no step, no gap and no change of width. Pass <paramref name="bankDegrees"/> (one per
        /// point, no resampling) for a banked bend, and <paramref name="floorPhysics"/> where the floor must keep its speed.
        /// </summary>
        public static GameObject BuildPort(Transform parent, string name, IList<Vector3> centreLocal, MachineKit kit, MachineConfig config,
            float wallHeight = 0f, IList<float> bankDegrees = null, PhysicsMaterial floorPhysics = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            List<Vector3> centre = bankDegrees == null ? Subdivide(centreLocal) : new List<Vector3>(centreLocal);
            float wh = wallHeight > 0f ? wallHeight : WallHeightFor(config.ballRadius);
            AddChannel(root.transform, "Port", Safe(HierarchyName(parent) + "_" + name), centre, config.chuteWidth, wh, kit, bankDegrees, floorPhysics);
            return root;
        }

        /// <summary>
        /// The parent chain as one name, so every port saves its meshes under its own asset name. Two stations both have a group called
        /// "Hopper" (screw lift and sorter): with the parent's name alone their entry ports overwrote each other's mesh asset and the
        /// screw lift's port came out empty, which let the balls fall through at the end of its chute.
        /// </summary>
        static string HierarchyName(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent)
            {
                if (c.name == "Stations" || c.name == "Machine" || c.name == "__probe") continue;
                parts.Insert(0, c.name);
            }

            return string.Join("_", parts);
        }

        /// <summary>Splits each segment into pieces no longer than the sample spacing, keeping every authored vertex.</summary>
        static List<Vector3> Subdivide(IList<Vector3> pts)
        {
            var result = new List<Vector3> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(pts[i], pts[i - 1]) / SampleSpacing));
                for (int k = 1; k <= pieces; k++)
                {
                    result.Add(Vector3.Lerp(pts[i - 1], pts[i], k / (float)pieces));
                }
            }

            return result;
        }

        static void AddChannel(Transform root, string prefix, string safe, List<Vector3> centre, float width, float wallHeight, MachineKit kit,
            IList<float> bank, PhysicsMaterial floorPhysics)
        {
            Mesh floorMesh = Sweep(centre, new[]
            {
                Loop(new Vector2(-width * 0.5f - WallThickness, -FloorThickness), new Vector2(width * 0.5f + WallThickness, 0f))
            }, bank);
            var floor = MachineGeometry.MeshObject(root, "Floor", floorMesh, $"{prefix}_{safe}_Floor", kit.asphalt);
            if (floorPhysics != null)
            {
                floor.GetComponent<MeshCollider>().sharedMaterial = floorPhysics;
            }

            Mesh wallMesh = Sweep(centre, new[]
            {
                Loop(new Vector2(-width * 0.5f - WallThickness, 0f), new Vector2(-width * 0.5f, wallHeight)),
                Loop(new Vector2(width * 0.5f, 0f), new Vector2(width * 0.5f + WallThickness, wallHeight))
            }, bank);
            MachineGeometry.MeshObject(root, "Walls", wallMesh, $"{prefix}_{safe}_Walls", kit.glass);
        }

        // ------------------------------------------------------------------ path

        static List<Vector2> RawPath(Vector3 from, Vector3 to, IList<Vector3> via)
        {
            var points = new List<Vector2> { new Vector2(from.x, from.z) };
            if (via != null)
            {
                for (int i = 0; i < via.Count; i++)
                {
                    points.Add(new Vector2(via[i].x, via[i].z));
                }
            }

            points.Add(new Vector2(to.x, to.z));
            for (int i = points.Count - 1; i > 0; i--)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude < 1e-4f)
                {
                    points.RemoveAt(i);
                }
            }

            if (points.Count < 2)
            {
                points.Add(points[0] + Vector2.right * 0.1f);
            }

            return points;
        }

        /// <summary>Chaikin corner cutting that keeps both end points, so the chute still meets the lip and the entry exactly.</summary>
        /// <summary>When above zero, corners are true arcs of this radius (see <see cref="ChutePath"/>) instead of Chaikin cuts. mach-002 sets it; mach-001 leaves it 0.</summary>
        public static float FilletRadius = 0f;

        static List<Vector2> Smooth(List<Vector2> input)
        {
            if (FilletRadius > 0f && input.Count >= 3)
            {
                return ChutePath.Fillet(input, FilletRadius);
            }

            List<Vector2> pts = input;
            for (int pass = 0; pass < 3 && pts.Count > 2; pass++)
            {
                var next = new List<Vector2> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[i + 1];
                    if (i > 0)
                    {
                        next.Add(Vector2.Lerp(a, b, 0.25f));
                    }

                    if (i < pts.Count - 2)
                    {
                        next.Add(Vector2.Lerp(a, b, 0.75f));
                    }
                }

                next.Add(pts[pts.Count - 1]);
                pts = next;
            }

            return pts;
        }

        static void CheckSlope(string name, Vector3 from, Vector3 to, float horizontal)
        {
            float drop = from.y - to.y;
            if (drop <= 0.01f)
            {
                Debug.LogWarning($"[mach] Connector '{name}' runs uphill or flat (drop {drop:0.00} m over {horizontal:0.0} m).");
                WarningCount++;
                return;
            }

            float degrees = Mathf.Atan2(drop, Mathf.Max(0.01f, horizontal)) * Mathf.Rad2Deg;
            if (degrees < MinSlopeDegrees || degrees > MaxSlopeDegrees)
            {
                Debug.LogWarning($"[mach] Connector '{name}' slope {degrees:0.0} deg is outside {MinSlopeDegrees}-{MaxSlopeDegrees} " +
                                 $"(drop {drop:0.00} m over {horizontal:0.0} m).");
                WarningCount++;
            }
        }

        static string Safe(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        // ------------------------------------------------------------------ mesh

        static Vector2[] Loop(Vector2 min, Vector2 max)
        {
            return new[] { new Vector2(min.x, min.y), new Vector2(max.x, min.y), new Vector2(max.x, max.y), new Vector2(min.x, max.y) };
        }

        /// <summary>Sweeps closed (side, up) profile loops along the centreline: a thick extruded channel part with outward normals.</summary>
        static Mesh Sweep(IList<Vector3> centre, IList<Vector2[]> loops, IList<float> bank = null)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            var sides = new Vector3[centre.Count];
            var ups = new Vector3[centre.Count];
            var run = new float[centre.Count];
            for (int i = 0; i < centre.Count; i++)
            {
                Vector3 fwd = (i < centre.Count - 1 ? centre[i + 1] - centre[i] : centre[i] - centre[i - 1]).normalized;
                if (i > 0 && i < centre.Count - 1)
                {
                    fwd = (centre[i + 1] - centre[i - 1]).normalized;
                }

                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                if (side.sqrMagnitude < 1e-6f)
                {
                    side = Vector3.right;
                }

                side.Normalize();
                if (bank != null && i < bank.Count)
                {
                    side = Quaternion.AngleAxis(bank[i], fwd) * side;
                }

                sides[i] = side;
                ups[i] = Vector3.Cross(fwd, side).normalized;
                run[i] = i == 0 ? 0f : run[i - 1] + Vector3.Distance(centre[i], centre[i - 1]);
            }

            foreach (Vector2[] loop in loops)
            {
                Vector2 middle = Vector2.zero;
                foreach (Vector2 p in loop)
                {
                    middle += p;
                }

                middle /= loop.Length;

                float perimeter = 0f;
                for (int e = 0; e < loop.Length; e++)
                {
                    int e0 = e;
                    int e1 = (e + 1) % loop.Length;
                    Vector2 edge = loop[e1] - loop[e0];
                    Vector2 outward2 = new Vector2(edge.y, -edge.x).normalized;
                    if (Vector2.Dot(outward2, (loop[e0] + loop[e1]) * 0.5f - middle) < 0f)
                    {
                        outward2 = -outward2;
                    }

                    int start = verts.Count;
                    for (int i = 0; i < centre.Count; i++)
                    {
                        Vector3 a = centre[i] + sides[i] * loop[e0].x + ups[i] * loop[e0].y;
                        Vector3 b = centre[i] + sides[i] * loop[e1].x + ups[i] * loop[e1].y;
                        Vector3 n = sides[i] * outward2.x + ups[i] * outward2.y;
                        verts.Add(a); verts.Add(b);
                        normals.Add(n); normals.Add(n);
                        uvs.Add(new Vector2(perimeter, run[i]));
                        uvs.Add(new Vector2(perimeter + edge.magnitude, run[i]));
                    }

                    for (int i = 0; i < centre.Count - 1; i++)
                    {
                        int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                        // Pick the winding whose geometric normal agrees with the outward normal.
                        Vector3 geometric = Vector3.Cross(verts[a1] - verts[a0], verts[b0] - verts[a0]);
                        bool flip = Vector3.Dot(geometric, normals[a0]) < 0f;
                        if (!flip)
                        {
                            tris.Add(a0); tris.Add(a1); tris.Add(b0);
                            tris.Add(b0); tris.Add(a1); tris.Add(b1);
                        }
                        else
                        {
                            tris.Add(a0); tris.Add(b0); tris.Add(a1);
                            tris.Add(b0); tris.Add(b1); tris.Add(a1);
                        }
                    }

                    perimeter += edge.magnitude;
                }
            }

            var mesh = new Mesh();
            if (verts.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
