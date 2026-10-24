using System.Collections.Generic;
using SimulationLobby.Shared;
using SimulationLobby.Shared.Tracks;
using UnityEngine;

namespace SimulationLobby.Shared.EditorTools
{
    /// <summary>
    /// Turns the layout's obstacle list into scene objects: chrome pegs, brass rotating arms, lacquer walls
    /// and baffles. Everything is a primitive, positioned in the track's own frame at the obstacle's
    /// distance, so it follows the slope and banking of whatever section it sits on.
    /// </summary>
    public static class ObstacleBuilder
    {
        public struct Kit
        {
            public Material chrome;
            public Material brass;
            public Material lacquer;
            public PhysicsMaterial peg;
            public PhysicsMaterial surface;
        }

        /// <summary>Gap kept between an obstacle and the rails, narrower than a marble so nothing can wedge there.</summary>
        const float WallMargin = 0.12f;

        const float ObstacleHeight = 0.3f;
        const float DividerHalfThickness = 0.06f;

        public static Transform Build(Transform parent, TrackLayout layout, List<TrackMeshBuilder.Sample> samples, Kit kit)
        {
            var root = new GameObject("Obstacles").transform;
            root.SetParent(parent, false);

            float[] sectionStart = SectionStarts(layout, samples);
            foreach (TrackObstacle obstacle in layout.obstacles)
            {
                if (obstacle.section < 0 || obstacle.section >= layout.sections.Length)
                {
                    Debug.LogWarning($"[track] Obstacle '{obstacle.label}' points at section {obstacle.section}, which does not exist. Skipped.");
                    continue;
                }

                float distance = sectionStart[obstacle.section] + obstacle.offset;
                var group = new GameObject($"{obstacle.label} ({obstacle.kind})").transform;
                group.SetParent(root, false);

                switch (obstacle.kind)
                {
                    case ObstacleKind.PegPyramid: BuildPegs(group, obstacle, samples, distance, kit, layout.pegWallClearance, pyramid: true); break;
                    case ObstacleKind.PegGrid: BuildPegs(group, obstacle, samples, distance, kit, layout.pegWallClearance, pyramid: false); break;
                    case ObstacleKind.Pinwheel: BuildPinwheel(group, obstacle, samples, distance, kit); break;
                    case ObstacleKind.Divider: BuildDivider(group, obstacle, samples, distance, kit); break;
                    case ObstacleKind.Bottleneck: BuildBottleneck(group, obstacle, samples, distance, kit); break;
                    case ObstacleKind.Funnel: BuildFunnel(group, obstacle, samples, distance, kit); break;
                    case ObstacleKind.Chicane: BuildChicane(group, obstacle, samples, distance, kit); break;
                    case ObstacleKind.Bumps: BuildBumps(group, obstacle, samples, distance, kit); break;
                }
            }

            return root;
        }

        /// <summary>Distance along the path at which each section begins.</summary>
        static float[] SectionStarts(TrackLayout layout, List<TrackMeshBuilder.Sample> samples)
        {
            var starts = new float[layout.sections.Length];
            for (int i = 1; i < samples.Count; i++)
            {
                int section = samples[i].section;
                if (section > 0 && samples[i - 1].section < section)
                {
                    starts[section] = samples[i - 1].distance;
                }
            }

            return starts;
        }

        // ------------------------------------------------------------------ pegs

        static void BuildPegs(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit, float wallClearance, bool pyramid)
        {
            TrackMeshBuilder.Sample middle = TrackMeshBuilder.At(samples, distance + 1f);
            float radius = o.size > 0f ? o.size : 0.05f;
            float spacing = Mathf.Max(0.3f, o.spacing > 0f ? o.spacing : 0.46f);
            LaneRange(middle, o.lane, out float min, out float max);
            if (wallClearance > 0f)
            {
                // Peg centres stay clearance + radius from the rail. A gap narrower than a marble next to the wall is
                // a trap: the marble presses into the peg and the rail and can't get past either.
                float inset = Mathf.Max(0f, wallClearance + radius - WallMargin);
                min += inset;
                max -= inset;
                if (max <= min)
                {
                    Debug.LogWarning($"[track] '{o.label}': the lane is too narrow for {wallClearance:0.00}m peg clearance. Skipped.");
                    return;
                }
            }
            float span = max - min;
            float centre = (min + max) * 0.5f;
            float pitch = spacing * 0.85f;

            int fit = Mathf.FloorToInt(span / spacing) + 1;
            int rows = Mathf.Max(1, o.count);
            if (pyramid && rows > fit)
            {
                Debug.LogWarning($"[track] '{o.label}': a {rows}-row pyramid does not fit {span:0.0}m of lane. Trimmed to {fit} rows.");
                rows = fit;
            }

            for (int row = 0; row < rows; row++)
            {
                // With clearance on (plinko mode), rows are placed at exact distances. The legacy snap to the next
                // sample bunches rows where the sample spacing doesn't match the row pitch, and a bunched row is a wall.
                float rowDistance = distance + row * pitch;
                TrackMeshBuilder.Sample frame = wallClearance > 0f
                    ? TrackMeshBuilder.AtExact(samples, rowDistance)
                    : TrackMeshBuilder.At(samples, rowDistance);
                int pegs;
                float first;
                if (pyramid)
                {
                    pegs = row + 1;
                    first = -(pegs - 1) * 0.5f * spacing;
                }
                else
                {
                    bool wide = row % 2 == 0;
                    pegs = wide ? fit : fit - 1;
                    first = -(pegs - 1) * 0.5f * spacing;
                }

                for (int i = 0; i < pegs; i++)
                {
                    AddPeg(parent, frame, centre + first + i * spacing, radius, kit);
                }
            }
        }

        static void AddPeg(Transform parent, TrackMeshBuilder.Sample frame, float lateral, float radius, Kit kit)
        {
            Quaternion rotation = Quaternion.LookRotation(frame.forward, frame.up);
            var peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peg.name = "Peg";
            peg.transform.SetParent(parent, false);
            peg.transform.rotation = rotation;
            peg.transform.position = frame.position + frame.right * lateral + frame.up * (ObstacleHeight * 0.5f);
            peg.transform.localScale = new Vector3(radius * 2f, ObstacleHeight * 0.5f, radius * 2f);
            peg.GetComponent<MeshRenderer>().sharedMaterial = kit.chrome;
            peg.GetComponent<Collider>().sharedMaterial = kit.peg;

            // A brass bead on top: the detail that makes a peg read as jewellery, not a post.
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Peg Cap";
            cap.transform.SetParent(peg.transform, true);
            cap.transform.position = frame.position + frame.right * lateral + frame.up * ObstacleHeight;
            cap.transform.rotation = rotation;
            cap.transform.localScale = Vector3.one * (radius * 2.6f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = kit.brass;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            RestoreUniformScale(cap.transform, radius * 2.6f);
        }

        /// <summary>A child of a non-uniformly scaled parent inherits the skew; set its world size back to a true sphere.</summary>
        static void RestoreUniformScale(Transform child, float worldDiameter)
        {
            Vector3 parentScale = child.parent.lossyScale;
            child.localScale = new Vector3(worldDiameter / parentScale.x, worldDiameter / parentScale.y,
                worldDiameter / parentScale.z);
        }

        // ------------------------------------------------------------------ pinwheel

        static void BuildPinwheel(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            TrackMeshBuilder.Sample frame = TrackMeshBuilder.At(samples, distance);
            float armLength = o.size > 0f ? o.size : frame.width * 0.5f - 0.045f;
            int arms = Mathf.Max(2, o.count);

            var wheel = new GameObject("Pinwheel");
            wheel.transform.SetParent(parent, false);
            wheel.transform.position = frame.position;
            wheel.transform.rotation = Quaternion.LookRotation(frame.forward, frame.up);
            var body = wheel.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var spinner = wheel.AddComponent<KinematicSpinner>();
            spinner.localAxis = Vector3.up;
            spinner.degreesPerSecond = o.speed != 0f ? o.speed : 110f;

            const float lift = 0.02f;
            for (int i = 0; i < arms; i++)
            {
                float angle = 360f * i / arms;
                Quaternion local = Quaternion.Euler(0f, angle, 0f);
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = $"Arm {i + 1}";
                arm.transform.SetParent(wheel.transform, false);
                arm.transform.localRotation = local;
                arm.transform.localPosition = local * Vector3.forward * (armLength * 0.5f) + Vector3.up * (lift + ObstacleHeight * 0.5f);
                arm.transform.localScale = new Vector3(0.055f, ObstacleHeight, armLength);
                arm.GetComponent<MeshRenderer>().sharedMaterial = kit.brass;
                arm.GetComponent<Collider>().sharedMaterial = kit.surface;
            }

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(wheel.transform, false);
            hub.transform.localPosition = Vector3.up * (lift + ObstacleHeight * 0.5f);
            hub.transform.localScale = new Vector3(0.2f, ObstacleHeight * 0.5f + 0.02f, 0.2f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = kit.chrome;
            hub.GetComponent<Collider>().sharedMaterial = kit.surface;
        }

        // ------------------------------------------------------------------ fork

        static void BuildDivider(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            float length = Mathf.Max(1f, o.length);
            float thickness = o.size > 0f ? o.size : 0.08f;
            TrackMeshBuilder.Sample middle = TrackMeshBuilder.At(samples, distance + length * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(middle.forward, middle.up);

            AddBar(parent, middle, o.lateral, 0f, 0f, length, thickness, rotation, kit);

            // A chrome nose, so the first thing a marble meets is a rounded edge and not a flat end.
            TrackMeshBuilder.Sample nose = TrackMeshBuilder.At(samples, distance);
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Divider Nose";
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.rotation = Quaternion.LookRotation(nose.forward, nose.up);
            cylinder.transform.position = nose.position + nose.right * o.lateral + nose.up * (ObstacleHeight * 0.5f);
            cylinder.transform.localScale = new Vector3(thickness * 1.6f, ObstacleHeight * 0.5f, thickness * 1.6f);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = kit.chrome;
            cylinder.GetComponent<Collider>().sharedMaterial = kit.surface;
        }

        // ------------------------------------------------------------------ bottleneck + chicane

        static void BuildBottleneck(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            float length = Mathf.Max(2f, o.length > 0f ? o.length : 4f);
            float gap = Mathf.Max(0.3f, o.size > 0f ? o.size : 0.5f);
            const float neck = 0.5f;
            float taper = Mathf.Max(0.5f, (length - neck) * 0.5f);

            TrackMeshBuilder.Sample start = TrackMeshBuilder.At(samples, distance);
            float halfWidth = start.width * 0.5f;
            float reach = halfWidth - gap * 0.5f;
            if (reach <= 0.05f)
            {
                Debug.LogWarning($"[track] '{o.label}': the track is already narrower than the {gap:0.00}m gap.");
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                // Closing in, then a short neck, then opening out. Each wing is a bar angled to the track.
                float inYaw = Mathf.Atan2(-side * reach, taper) * Mathf.Rad2Deg;
                float outYaw = Mathf.Atan2(side * reach, taper) * Mathf.Rad2Deg;
                float slant = Mathf.Sqrt(taper * taper + reach * reach);

                AddAngledBar(parent, samples, distance, side * (halfWidth - reach * 0.5f + 0.03f), taper * 0.5f, inYaw, slant, 0.09f, kit);
                AddAngledBar(parent, samples, distance, side * (gap * 0.5f + 0.045f), taper + neck * 0.5f, 0f, neck, 0.09f, kit);
                AddAngledBar(parent, samples, distance, side * (halfWidth - reach * 0.5f + 0.03f), taper + neck + taper * 0.5f, outYaw, slant, 0.09f, kit);
            }
        }

        /// <summary>Closing taper only: two walls run from the rails in to an exit gap and stop. The obstacle after it does the spreading.</summary>
        static void BuildFunnel(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            float taper = Mathf.Max(1f, o.length > 0f ? o.length : 3f);
            float gap = Mathf.Max(0.3f, o.size > 0f ? o.size : 1.4f);

            TrackMeshBuilder.Sample start = TrackMeshBuilder.At(samples, distance);
            float halfWidth = start.width * 0.5f;
            float reach = halfWidth - gap * 0.5f;
            if (reach <= 0.05f)
            {
                Debug.LogWarning($"[track] '{o.label}': the track is already narrower than the {gap:0.00}m gap.");
                return;
            }

            float slant = Mathf.Sqrt(taper * taper + reach * reach);
            for (int side = -1; side <= 1; side += 2)
            {
                float yaw = Mathf.Atan2(-side * reach, taper) * Mathf.Rad2Deg;

                // The +0.03 pushes the upstream end into the rail, so no marble can slip behind the wall.
                AddAngledBar(parent, samples, distance, side * (halfWidth - reach * 0.5f + 0.03f), taper * 0.5f, yaw, slant, 0.09f, kit);
            }
        }

        static void BuildChicane(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            int count = Mathf.Max(1, o.count);
            float spacing = Mathf.Max(1f, o.spacing > 0f ? o.spacing : 2.4f);
            float fraction = Mathf.Clamp(o.size > 0f ? o.size : 0.6f, 0.2f, 0.8f);

            for (int i = 0; i < count; i++)
            {
                float at = distance + i * spacing;
                TrackMeshBuilder.Sample frame = TrackMeshBuilder.At(samples, at);
                int side = i % 2 == 0 ? -1 : 1;
                float halfWidth = frame.width * 0.5f;
                float reach = frame.width * fraction;
                float run = reach * 0.55f;
                float yaw = Mathf.Atan2(-side * reach, run) * Mathf.Rad2Deg;
                float slant = Mathf.Sqrt(reach * reach + run * run);
                AddAngledBar(parent, samples, at, side * (halfWidth - reach * 0.5f + 0.03f), run * 0.5f, yaw, slant, 0.08f, kit);
            }
        }

        /// <summary>A bar placed in the frame found at <paramref name="distance"/>, with a lateral and forward offset and a yaw.</summary>
        static void AddAngledBar(Transform parent, List<TrackMeshBuilder.Sample> samples, float distance, float lateral,
            float forward, float yawDegrees, float length, float thickness, Kit kit)
        {
            TrackMeshBuilder.Sample frame = TrackMeshBuilder.At(samples, distance + forward);
            Quaternion rotation = Quaternion.LookRotation(frame.forward, frame.up);
            AddBar(parent, frame, lateral, 0f, yawDegrees, length, thickness, rotation, kit);
        }

        /// <summary>A lacquer wall segment with a thin brass cap along its top edge.</summary>
        static void AddBar(Transform parent, TrackMeshBuilder.Sample frame, float lateral, float forward, float yawDegrees,
            float length, float thickness, Quaternion frameRotation, Kit kit)
        {
            Quaternion rotation = frameRotation * Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 centre = frame.position + frame.right * lateral + frame.forward * forward;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.rotation = rotation;
            wall.transform.position = centre + frame.up * (ObstacleHeight * 0.5f);
            wall.transform.localScale = new Vector3(thickness, ObstacleHeight, length);
            wall.GetComponent<MeshRenderer>().sharedMaterial = kit.lacquer;
            wall.GetComponent<Collider>().sharedMaterial = kit.surface;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "Wall Cap";
            cap.transform.SetParent(parent, false);
            cap.transform.rotation = rotation;
            cap.transform.position = centre + frame.up * (ObstacleHeight + 0.006f);
            cap.transform.localScale = new Vector3(thickness + 0.012f, 0.014f, length + 0.01f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = kit.brass;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
        }

        // ------------------------------------------------------------------ bumps

        static void BuildBumps(Transform parent, TrackObstacle o, List<TrackMeshBuilder.Sample> samples, float distance,
            Kit kit)
        {
            int count = Mathf.Max(1, o.count);
            float spacing = Mathf.Max(0.4f, o.spacing > 0f ? o.spacing : 0.9f);

            // size is the hump's height. Measured headless (2026-10-09): a 6cm bar stops a marble dead at any
            // entry speed (mass changes nothing, a marble's speed is set by the slope), 4cm slows it hard, and a
            // 3cm hump cut from a wide arc lets it hop through at 1.5 to 5 m/s. The arc is a cylinder sunk into the
            // floor: radius 2.4x the height keeps the slope gentle, clamped so it never pokes out under the track.
            float height = Mathf.Clamp(o.size > 0f ? o.size : 0.03f, 0.01f, 0.05f);
            float radius = Mathf.Min(height * 2.4f, (TrackMeshBuilder.FloorThickness - 0.01f + height) * 0.5f);

            for (int i = 0; i < count; i++)
            {
                TrackMeshBuilder.Sample frame = TrackMeshBuilder.At(samples, distance + i * spacing);
                LaneRange(frame, o.lane, out float min, out float max);
                float length = max - min;
                if (length < 0.2f)
                {
                    continue;
                }

                // A cylinder laid across the track (its Y axis turned onto the track's right).
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = "Bump";
                bar.transform.SetParent(parent, false);
                bar.transform.rotation = Quaternion.LookRotation(frame.forward, frame.up) * Quaternion.Euler(0f, 0f, 90f);
                bar.transform.position = frame.position + frame.right * ((min + max) * 0.5f) + frame.up * (height - radius);
                bar.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = kit.brass;
                bar.GetComponent<Collider>().sharedMaterial = kit.surface;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Usable sideways range at a point: the whole floor, or one half of it for a fork lane.</summary>
        static void LaneRange(TrackMeshBuilder.Sample frame, int lane, out float min, out float max)
        {
            float half = frame.width * 0.5f;
            min = -half + WallMargin;
            max = half - WallMargin;
            if (lane < 0)
            {
                max = -DividerHalfThickness - WallMargin;
            }
            else if (lane > 0)
            {
                min = DividerHalfThickness + WallMargin;
            }
        }
    }
}
