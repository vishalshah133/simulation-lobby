using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A recorded (or synthetic) lap race: the course header plus every fixed tick's racer state. Lets the camera be replayed
    /// offline at any frame rate and measured, with no scene and no physics. Data and IO only.
    /// </summary>
    public sealed class RaceTrace
    {
        public const int FileVersion = 1;

        [Serializable]
        public struct StationInfo
        {
            public string name;
            public Vector3 forward;
            public Vector3 focus;
            public Vector3 cameraOffset;
            public bool hasFocus;
            public float s0;
            public float s1;
        }

        // Header.
        public float length;
        public Vector3[] vertices = new Vector3[0];
        public StationInfo[] stations = new StationInfo[0];
        public Vector3[] gridSlots = new Vector3[0];
        public int laps = 2;
        public int racerCount;
        public float fixedDt = 0.01f;

        // Ticks (positions, progress, rank, recycles, finished are tick * racerCount + racer).
        public int tickCount;
        public float[] simTime = new float[0];
        public byte[] phase = new byte[0];
        public short[] leader = new short[0];
        public short[] winner = new short[0];
        public Vector3[] pos = new Vector3[0];
        public float[] progress = new float[0];
        public byte[] rank = new byte[0];
        public byte[] recycles = new byte[0];
        public bool[] finished = new bool[0];

        public float End => tickCount > 0 ? simTime[tickCount - 1] : 0f;

        public void AllocateTicks(int ticks)
        {
            tickCount = ticks;
            simTime = new float[ticks];
            phase = new byte[ticks];
            leader = new short[ticks];
            winner = new short[ticks];
            pos = new Vector3[ticks * racerCount];
            progress = new float[ticks * racerCount];
            rank = new byte[ticks * racerCount];
            recycles = new byte[ticks * racerCount];
            finished = new bool[ticks * racerCount];
        }

        /// <summary>A fresh input struct with arrays sized for this trace.</summary>
        public RaceCameraInput NewInput()
        {
            return new RaceCameraInput
            {
                count = racerCount,
                pos = new Vector3[racerCount],
                progress = new float[racerCount],
                recycles = new int[racerCount],
                finished = new bool[racerCount],
                laps = laps,
                winner = -1,
            };
        }

        /// <summary>
        /// Fills <paramref name="into"/> as the presentation would see the race at render time <paramref name="t"/>: discrete fields
        /// from tick k = floor(t / fixedDt), positions interpolated between ticks k-1 and k like a Rigidbody with interpolation on.
        /// </summary>
        public void Sample(float t, ref RaceCameraInput into)
        {
            int k = Mathf.Clamp(Mathf.FloorToInt(t / fixedDt), 0, tickCount - 1);
            float frac = Mathf.Clamp01(t / fixedDt - k);
            int k0 = Mathf.Max(0, k - 1);
            into.simTime = simTime[k];
            into.phase = (RacePhase)phase[k];
            into.winner = winner[k];
            into.laps = laps;
            into.count = racerCount;
            for (int i = 0; i < racerCount; i++)
            {
                int a = k0 * racerCount + i;
                int b = k * racerCount + i;
                into.pos[i] = Vector3.Lerp(pos[a], pos[b], frac);
                into.progress[i] = progress[b];
                into.recycles[i] = recycles[b];
                into.finished[i] = finished[b];
            }
        }

        public Vector3 GridCentre()
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < gridSlots.Length; i++)
            {
                c += gridSlots[i];
            }

            return gridSlots.Length > 0 ? c / gridSlots.Length : Vector3.zero;
        }

        public void ComputeOpening(float fovDeg, float aspect, float margin, out Vector3 centre, out Vector3 dir, out float distance)
        {
            StationInfo gate = stations.Length > 0 ? stations[0] : default;
            RaceCameraCore.ComputeOpening(gridSlots, gate.forward, gate.cameraOffset, gate.hasFocus, fovDeg, aspect, margin, out centre, out dir, out distance);
        }

        public ZoneSource[] ZoneSources()
        {
            var list = new ZoneSource[stations.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                list[i] = new ZoneSource { name = stations[i].name, forward = stations[i].forward, focus = stations[i].focus, s0 = stations[i].s0, s1 = stations[i].s1 };
            }

            return list;
        }

        // Header from the live scene (read only) --------------------------------------------------------------------

        /// <summary>Builds the header (course, stations, grid) from a built scene without touching it.</summary>
        public static RaceTrace HeaderFromScene(MachineRaceSimulation sim)
        {
            IRaceState state = sim;
            IReadOnlyList<MachineStation> stations = state.Stations;
            var progress = new RaceProgress(stations);
            var verts = new Vector3[progress.SegmentCount];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = progress.Vertex(i);
            }

            var track = new CourseTrack(verts);
            var trace = new RaceTrace
            {
                length = progress.TotalLength,
                vertices = verts,
                laps = state.Laps > 0 ? state.Laps : 2,
                racerCount = state.RacerCount > 0 ? state.RacerCount : 16,
                fixedDt = 0.01f,
            };

            trace.stations = new StationInfo[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                MachineStation st = stations[i];
                Vector3 f = st != null ? st.transform.forward : Vector3.forward;
                f.y = 0f;
                trace.stations[i] = new StationInfo
                {
                    name = st != null && !string.IsNullOrEmpty(st.displayName) ? st.displayName : (st != null ? st.name : ""),
                    forward = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward,
                    focus = st != null ? (st.cameraFocus != null ? st.cameraFocus.position : st.activityBounds.center) : Vector3.zero,
                    cameraOffset = st != null ? st.cameraOffset : Vector3.zero,
                    hasFocus = st != null && st.cameraFocus != null,
                    s0 = track.VertexDistance(2 * i),
                    s1 = track.VertexDistance(2 * i + 1),
                };
            }

            var gate = stations.Count > 0 ? stations[0] as IStartGate : null;
            int slots = gate != null ? gate.SlotCount : 0;
            trace.gridSlots = new Vector3[slots];
            for (int i = 0; i < slots; i++)
            {
                trace.gridSlots[i] = gate.GridSlot(i);
            }

            return trace;
        }

        // IO --------------------------------------------------------------------------------------------------------

        public static string TraceDirectory => Path.Combine(Directory.GetCurrentDirectory(), "Temp", "RaceCamTraces");

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(FileVersion);
                w.Write(length);
                w.Write(laps);
                w.Write(racerCount);
                w.Write(fixedDt);
                WriteArray(w, vertices);
                w.Write(stations.Length);
                foreach (StationInfo s in stations)
                {
                    w.Write(s.name ?? "");
                    Write(w, s.forward);
                    Write(w, s.focus);
                    Write(w, s.cameraOffset);
                    w.Write(s.hasFocus);
                    w.Write(s.s0);
                    w.Write(s.s1);
                }

                WriteArray(w, gridSlots);
                w.Write(tickCount);
                for (int k = 0; k < tickCount; k++)
                {
                    w.Write(simTime[k]);
                    w.Write(phase[k]);
                    w.Write(leader[k]);
                    w.Write(winner[k]);
                }

                for (int i = 0; i < pos.Length; i++)
                {
                    Write(w, pos[i]);
                    w.Write(progress[i]);
                    w.Write(rank[i]);
                    w.Write(recycles[i]);
                    w.Write(finished[i]);
                }
            }
        }

        public static RaceTrace Load(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                int version = r.ReadInt32();
                if (version != FileVersion)
                {
                    throw new InvalidDataException("Unsupported race trace version " + version);
                }

                var t = new RaceTrace
                {
                    length = r.ReadSingle(),
                    laps = r.ReadInt32(),
                    racerCount = r.ReadInt32(),
                    fixedDt = r.ReadSingle(),
                };
                t.vertices = ReadArray(r);
                t.stations = new StationInfo[r.ReadInt32()];
                for (int i = 0; i < t.stations.Length; i++)
                {
                    t.stations[i] = new StationInfo
                    {
                        name = r.ReadString(),
                        forward = ReadVec(r),
                        focus = ReadVec(r),
                        cameraOffset = ReadVec(r),
                        hasFocus = r.ReadBoolean(),
                        s0 = r.ReadSingle(),
                        s1 = r.ReadSingle(),
                    };
                }

                t.gridSlots = ReadArray(r);
                t.AllocateTicks(r.ReadInt32());
                for (int k = 0; k < t.tickCount; k++)
                {
                    t.simTime[k] = r.ReadSingle();
                    t.phase[k] = r.ReadByte();
                    t.leader[k] = r.ReadInt16();
                    t.winner[k] = r.ReadInt16();
                }

                for (int i = 0; i < t.pos.Length; i++)
                {
                    t.pos[i] = ReadVec(r);
                    t.progress[i] = r.ReadSingle();
                    t.rank[i] = r.ReadByte();
                    t.recycles[i] = r.ReadByte();
                    t.finished[i] = r.ReadBoolean();
                }

                return t;
            }
        }

        static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        static Vector3 ReadVec(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        static void WriteArray(BinaryWriter w, Vector3[] a)
        {
            w.Write(a.Length);
            foreach (Vector3 v in a)
            {
                Write(w, v);
            }
        }

        static Vector3[] ReadArray(BinaryReader r)
        {
            var a = new Vector3[r.ReadInt32()];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = ReadVec(r);
            }

            return a;
        }
    }
}
