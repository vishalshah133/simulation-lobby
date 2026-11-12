using System.Globalization;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.PerfectFit.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit.View
{
    /// <summary>
    /// Draws <see cref="PerfectFitSimulation"/>: two stations (a wall with its glow lamp, and the shape) that take
    /// turns, the incoming one sliding in while the outgoing one slides away; the camera push; the caption; the sounds.
    /// Reads the simulation's pose and never writes to it.
    /// </summary>
    /// <remarks>
    /// Meshes are built here from the config's outlines at the first frame and thrown away on destroy: the picture is
    /// always the numbers the simulation measured, never a stale saved asset. Frame-rate independence comes from the
    /// pose being a closed-form function of seconds, which this extrapolates by the sub-tick remainder.
    /// </remarks>
    public sealed class PerfectFitPresenter : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Station
        {
            public Transform root;
            public Transform wall;
            public Transform shape;
            public MeshFilter wallFilter;
            public MeshFilter shapeFilter;

            [System.NonSerialized] public int assignedRound = -1;
        }

        PerfectFitSimulation _simulation;
        CaptionHud _hud;
        Camera _camera;
        PerfectFitAudio _audio;
        Station[] _stations;

        Mesh[] _wallMeshes;
        Mesh[] _shapeMeshes;

        int _lastRound = -1;
        bool _launched;
        bool _locked;
        bool _cleared;
        bool _begun;
        string _caption;
        string _subline;

        public void Bind(PerfectFitSimulation simulation, CaptionHud hud, Camera camera, PerfectFitAudio audio, Station a, Station b)
        {
            _simulation = simulation;
            _hud = hud;
            _camera = camera;
            _audio = audio;
            _stations = new[] { a, b };
        }

        void Update()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            PerfectFitConfig config = _simulation.Config;
            if (_wallMeshes == null)
            {
                BuildMeshes(config);
            }

            if (!_begun)
            {
                _begun = true;
                _audio?.Begin(config.RoundCount);
            }

            int round = _simulation.RoundIndex;
            float seconds = _simulation.RoundSeconds;
            if (!_simulation.IsComplete)
            {
                seconds += Mathf.Clamp(Time.time - Time.fixedTime, 0f, _simulation.FixedStep);
            }

            if (round != _lastRound)
            {
                _lastRound = round;
                _launched = false;
                _locked = false;
                _cleared = false;
            }

            Station current = _stations[round & 1];
            Station other = _stations[(round + 1) & 1];
            Assign(current, round);

            FitPose pose = _simulation.PoseAt(round, seconds);
            float slideIn = round > 0 && seconds < config.slideSeconds ? Smooth(seconds / config.slideSeconds) : 1f;
            Place(current, pose, (1f - slideIn) * config.slideDistance);

            if (round > 0 && seconds < config.slideSeconds)
            {
                Assign(other, round - 1);
                FitPose outgoing = _simulation.PoseAt(round - 1, _simulation.RoundDuration(round - 1) + seconds);
                Place(other, outgoing, -slideIn * config.slideDistance);
                other.root.gameObject.SetActive(true);
            }
            else
            {
                other.root.gameObject.SetActive(false);
            }

            current.root.gameObject.SetActive(true);

            PlaceCamera(config, pose.focus);
            FireSounds(round, pose);
            UpdateText(config, round, pose);
        }

        void OnDestroy()
        {
            DestroyAll(_wallMeshes);
            DestroyAll(_shapeMeshes);
        }

        static void DestroyAll(Mesh[] meshes)
        {
            if (meshes == null)
            {
                return;
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                {
                    Destroy(meshes[i]);
                }
            }
        }

        void BuildMeshes(PerfectFitConfig config)
        {
            int n = config.RoundCount;
            _wallMeshes = new Mesh[n];
            _shapeMeshes = new Mesh[n];
            for (int i = 0; i < n; i++)
            {
                PerfectFitRound r = config.rounds[i];
                Vector2[] outline = FitProfile.Create(r.kind, config.shapeRadius);
                Vector2[] hole = FitProfile.Offset(outline, r.gapMillimetres * 0.001f);
                bool smooth = FitProfile.IsSmooth(r.kind);
                _shapeMeshes[i] = ProfileMeshBuilder.BuildPrism(outline, config.shapeDepth, smooth, $"Shape_{r.shapeName}");
                _wallMeshes[i] = ProfileMeshBuilder.BuildWall(hole, config.wallRadius, config.wallThickness, smooth, $"Wall_{r.shapeName}");
            }
        }

        void Assign(Station station, int round)
        {
            if (station.assignedRound == round)
            {
                return;
            }

            station.assignedRound = round;
            station.wallFilter.sharedMesh = _wallMeshes[round];
            station.shapeFilter.sharedMesh = _shapeMeshes[round];
        }

        static void Place(Station station, FitPose pose, float x)
        {
            station.root.localPosition = new Vector3(x, 0f, 0f);
            station.wall.localRotation = Quaternion.Euler(0f, 0f, pose.wallAngle);
            station.shape.localPosition = new Vector3(0f, 0f, pose.shapeZ);
            station.shape.localRotation = Quaternion.Euler(pose.tiltX, pose.tiltY, 0f) * Quaternion.Euler(0f, 0f, pose.shapeAngle);
        }

        void PlaceCamera(PerfectFitConfig config, float focus)
        {
            if (_camera == null)
            {
                return;
            }

            Transform t = _camera.transform;
            t.position = Vector3.Lerp(config.widePosition, config.closePosition, focus);
            Vector3 target = Vector3.Lerp(config.wideTarget, config.closeTarget, focus);
            t.rotation = Quaternion.LookRotation(target - t.position, Vector3.up);
        }

        void FireSounds(int round, FitPose pose)
        {
            if (_audio == null)
            {
                return;
            }

            if (!_launched && pose.flightProgress > 0f)
            {
                _launched = true;
                _audio.PlayRiser(round, _simulation.ContactSeconds);
            }

            if (!_locked && pose.aligned)
            {
                _locked = true;
                _audio.PlayLock();
            }

            if (!_cleared && pose.cleared)
            {
                _cleared = true;
                _audio.PlayThrough(round);
            }
        }

        void UpdateText(PerfectFitConfig config, int round, FitPose pose)
        {
            if (_hud == null)
            {
                return;
            }

            PerfectFitRound r = config.rounds[round];
            string caption;
            string subline;
            if (_simulation.Phase == PerfectFitPhase.EndCard || _simulation.Phase == PerfectFitPhase.Done)
            {
                caption = config.endCard.Replace("{n}", config.RoundCount.ToString(CultureInfo.InvariantCulture));
                subline = config.endLine;
            }
            else
            {
                caption = pose.phase == PerfectFitPhase.Result ? r.result : $"WILL THE {r.shapeName} FIT?";
                subline = $"GAP {FormatGap(r.gapMillimetres)} MM";
            }

            // Pushed on change only: the HUD punches the caption on every SetCaption call.
            if (caption != _caption)
            {
                _caption = caption;
                _hud.SetCaption(caption);
            }

            if (subline != _subline)
            {
                _subline = subline;
                _hud.SetSubline(subline);
            }
        }

        static string FormatGap(float millimetres)
        {
            return millimetres >= 10f
                ? millimetres.ToString("0", CultureInfo.InvariantCulture)
                : millimetres.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
