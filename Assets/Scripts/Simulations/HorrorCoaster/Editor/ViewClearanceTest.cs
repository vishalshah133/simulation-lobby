using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Checks the viewer's line of sight. With the scene open, poses the cart at rail distance 5 m, puts the eye where the ride camera
    /// puts it (cart + up 1.3 + right 0.4 - forward 0.3) and fires a fan of rays through the central corridor: azimuth within +/-10 degrees of the
    /// track direction, elevation -20 to +12 degrees. Cars, bars and riders have no colliders, so temporary mesh colliders are put on them
    /// for the test and removed again. The track itself, the set dressing and the scare entities are not counted: only what the
    /// train and the viewer's own rig put in the way.
    /// </summary>
    static class ViewClearanceTest
    {
        const float RailDistance = 5f;
        const float AzimuthLimit = 10f;
        const float ElevationMin = -20f;
        const float ElevationMax = 12f;
        /// <summary>Near field: the viewer's own car, the first car and its rider. Must be 0 % blocked.</summary>
        const float NearRange = 3.2f;
        const float FarRange = 45f;

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Haunted: View Clearance Test", false, 234)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[coast] Exit play mode before running the view clearance test.");
                return;
            }

            var host = Object.FindAnyObjectByType<HorrorCoasterHost>();
            var roster = Object.FindAnyObjectByType<RiderRoster>();
            Camera cam = Camera.main;
            if (host == null || host.Config == null || roster == null || cam == null)
            {
                Debug.LogError("[coast] Open the Haunted Coaster scene first (need the host, the Riders and the main camera).");
                return;
            }

            CoasterRail rail = HorrorCoasterRail.FromConfig(host.Config);
            rail.Sample(RailDistance, out Vector3 cartPos, out Vector3 fwd, out Vector3 up);
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            Vector3 eye = cartPos + up * 1.3f + right * 0.4f - fwd * 0.3f;

            Transform camT = cam.transform;
            Vector3 savedPos = camT.position;
            Quaternion savedRot = camT.rotation;
            roster.PoseCars(rail, RailDistance, Vector3.zero);
            camT.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd, up));

            var temp = new List<MeshCollider>();
            var owners = new Dictionary<Collider, string>();
            AddColliders(roster.transform, temp, owners, "train");
            Transform rig = camT.Find("Viewer Lap Bar And Hands");
            if (rig != null)
            {
                AddColliders(rig, temp, owners, "viewer rig");
            }

            Physics.SyncTransforms();

            int total = 0, nearBlocked = 0, farBlocked = 0;
            var nearNames = new Dictionary<string, int>();
            var farNames = new Dictionary<string, int>();
            for (float az = -AzimuthLimit; az <= AzimuthLimit + 0.01f; az += 1f)
            {
                for (float el = ElevationMin; el <= ElevationMax + 0.01f; el += 2f)
                {
                    Vector3 dir = Quaternion.AngleAxis(az, up) * Quaternion.AngleAxis(-el, right) * fwd;
                    total++;
                    if (!Physics.Raycast(eye, dir, out RaycastHit hit, FarRange))
                    {
                        continue;
                    }

                    string label = Describe(hit.collider, owners);
                    if (hit.distance <= NearRange)
                    {
                        nearBlocked++;
                        Bump(nearNames, label);
                    }
                    else
                    {
                        farBlocked++;
                        Bump(farNames, label);
                    }
                }
            }

            // The viewer's own rig must sit below -25 degrees of elevation in camera space.
            float rigTop = -90f;
            string rigWorst = "";
            if (rig != null)
            {
                foreach (MeshRenderer r in rig.GetComponentsInChildren<MeshRenderer>())
                {
                    Bounds b = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 local = Quaternion.Inverse(camT.rotation) * (corner - camT.position);
                        if (local.z < 0.15f)
                        {
                            continue;
                        }

                        float elev = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                        if (elev > rigTop)
                        {
                            rigTop = elev;
                            rigWorst = r.name;
                        }
                    }
                }
            }

            foreach (MeshCollider c in temp)
            {
                if (c != null)
                {
                    Object.DestroyImmediate(c);
                }
            }

            camT.SetPositionAndRotation(savedPos, savedRot);
            roster.PoseCars(rail, 0f, Vector3.zero);

            float nearPct = 100f * nearBlocked / Mathf.Max(1, total);
            float farPct = 100f * farBlocked / Mathf.Max(1, total);
            bool nearOk = nearBlocked == 0;
            bool rigOk = rigTop <= -25f;
            var sb = new StringBuilder();
            sb.AppendLine($"[coast] View clearance at rail {RailDistance:F0} m, eye {eye}: {total} rays, azimuth +/-{AzimuthLimit:F0} deg, elevation {ElevationMin:F0}..{ElevationMax:F0} deg.");
            sb.AppendLine($"  near field (<= {NearRange:F1} m: own car, first car, first rider): {nearBlocked}/{total} blocked = {nearPct:F1}% -> {(nearOk ? "PASS" : "FAIL")}{Format(nearNames)}");
            sb.AppendLine($"  far field ({NearRange:F1}-{FarRange:F0} m: the riders ahead converge on the vanishing point, unavoidable): {farBlocked}/{total} = {farPct:F1}%{Format(farNames)}");
            sb.AppendLine($"  viewer rig highest edge: {rigTop:F1} deg (limit -25) at '{rigWorst}' -> {(rigOk ? "PASS" : "FAIL")}");
            if (nearOk && rigOk)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogError(sb.ToString());
            }
        }

        static void AddColliders(Transform root, List<MeshCollider> temp, Dictionary<Collider, string> owners, string group)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.gameObject.activeInHierarchy || mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }

                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
                Transform car = mf.transform;
                while (car.parent != null && car.parent != root)
                {
                    car = car.parent;
                }

                owners[mc] = group + "/" + car.name + "/" + mf.name;
            }
        }

        static string Describe(Collider c, Dictionary<Collider, string> owners)
        {
            return owners.TryGetValue(c, out string s) ? s : c.name;
        }

        static void Bump(Dictionary<string, int> d, string key)
        {
            d.TryGetValue(key, out int n);
            d[key] = n + 1;
        }

        static string Format(Dictionary<string, int> d)
        {
            if (d.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder(" [blockers: ");
            bool first = true;
            foreach (KeyValuePair<string, int> kv in d)
            {
                sb.Append(first ? "" : ", ").Append(kv.Key).Append(" x").Append(kv.Value);
                first = false;
            }

            return sb.Append(']').ToString();
        }
    }
}
