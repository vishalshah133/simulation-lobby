using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The silo: a glass cylinder over a terrazzo cone. A star feeder (rotary valve) meters balls out under the
    /// outlet and a sliding flood gate dumps everything at once. Entry is the open top; the exit is the tray lip
    /// far below. All motion is posed from sim time (rotor angle integrated in <see cref="Step"/>).
    /// </summary>
    /// <remarks>
    /// Feeder maths (ball radius 0.065, diameter 0.13): the rotor has 6 pockets. A pocket is a 60 degree sector between the
    /// hub (r 0.18) and the vane tips (r 0.44), 0.80 m long: area 0.5 (0.44^2 - 0.18^2) (pi/3) - vane = 0.077 m^2, so
    /// 0.061 m^3. A ball plus its air is 1.15e-3 m^3 / 0.58 packing = 2.0e-3 m^3, so a full pocket holds ~30 balls. Pockets
    /// refill from a 0.8 m (6 diameter) throat for ~3 s each while they sweep the 120 degree inlet, so ~20 per pocket is a
    /// safe working figure. rpm = rate * 60 / (pockets * ballsPerPocket): 13/s is 39 deg/s (6.5 rpm). The simulation also
    /// trims the commanded rate from the measured outlet flow, so a wrong estimate here only costs a few seconds.
    /// </remarks>
    public sealed class SiloStation : MachineStation, ISilo
    {
        [Header("Geometry (set by SiloBuilder)")]
        public float innerRadius = 2f;
        [Tooltip("Local y of the cone top (bottom of the storage cylinder).")]
        public float storageBottomLocalY = -2.45f;
        [Tooltip("Local y of the open top.")]
        public float storageTopLocalY = -0.1f;

        [Tooltip("Local y of the cone's bottom (the outlet throat) and its radius there.")]
        public float coneBottomLocalY = -3.7f;
        public float throatRadius = 0.4f;
        [Tooltip("Cone height per unit of radius (tan of the wall angle from horizontal).")]
        public float coneSlope = 0.7813f;

        [Header("Star feeder")]
        public Rigidbody rotorBody;
        public Vector3 rotorRestPosition;
        public Quaternion rotorRestRotation = Quaternion.identity;
        [Min(1)] public int pockets = 6;
        [Tooltip("Estimated balls carried per pocket; sets rpm = rate * 60 / (pockets * this).")]
        [Min(0.5f)] public float ballsPerPocket = 20f;
        [Tooltip("Rotor acceleration in degrees per second squared (smooth ramps, no jumps).")]
        [Min(1f)] public float rotorAccelDegrees = 60f;
        [Tooltip("Local y of the plane just under the feeder outlet; the simulation counts balls crossing it.")]
        public float outletLocalY = -4.6f;

        [Header("Anti-bridging agitator (kinematic bar in the cone)")]
        public Rigidbody agitatorBody;
        public Vector3 agitatorRestPosition;
        public Quaternion agitatorRestRotation = Quaternion.identity;
        [Min(0f)] public float agitatorDegreesPerSecond = 20f;

        [Header("Flood gate")]
        public Rigidbody plateBody;
        public Vector3 plateRestPosition;
        public Quaternion plateRestRotation = Quaternion.identity;
        [Tooltip("Local-space slide of the plate when fully open.")]
        public Vector3 plateOpenOffset = new Vector3(1.7f, 0f, 0f);
        [Min(0.1f)] public float floodSlideSeconds = 0.6f;

        [Header("Tray (flood back-pressure)")]
        [Tooltip("Local box over the catch tray, clear of the feeder housing. Balls inside count as queued.")]
        public Bounds trayLocal = new Bounds(new Vector3(0f, -4.7f, 1.7f), new Vector3(1.6f, 0.8f, 2.4f));
        [Tooltip("Balls the tray holds comfortably; the flood gate eases shut as the tray fills toward this.")]
        [Min(10)] public int trayCapacity = 300;

        float _targetDegreesPerSecond;
        float _degreesPerSecond;
        float _angle;
        float _agitatorAngle;
        bool _floodOpen;
        float _floodT;
        int _trayBalls;
        int _activeBalls;
        int _activeCap;

        public float InnerRadius => innerRadius;

        /// <summary>The storage cylinder. The simulation's starting lattice also fills the cone below it.</summary>
        public Bounds StorageBounds
        {
            get
            {
                float h = storageTopLocalY - storageBottomLocalY;
                Vector3 centre = transform.TransformPoint(new Vector3(0f, storageBottomLocalY + h * 0.5f, 0f));
                float d = innerRadius * 2f;
                return new Bounds(centre, new Vector3(d, h, d));
            }
        }

        /// <summary>World position of the bottom of the cone.</summary>
        public Vector3 ConeBottomWorld => transform.TransformPoint(new Vector3(0f, coneBottomLocalY, 0f));

        /// <summary>World y of the plane under the feeder outlet.</summary>
        public float OutletWorldY => transform.TransformPoint(new Vector3(0f, outletLocalY, 0f)).y;

        /// <summary>World-space AABB of <see cref="trayLocal"/>.</summary>
        public Bounds TrayBounds
        {
            get
            {
                Vector3 c = trayLocal.center, e = trayLocal.extents;
                var b = new Bounds(transform.TransformPoint(c), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    b.Encapsulate(transform.TransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z)));
                }

                return b;
            }
        }

        /// <summary>Volume of the cone frustum, m^3.</summary>
        public float ConeVolume
        {
            get
            {
                float h = storageBottomLocalY - coneBottomLocalY;
                return Mathf.PI * h / 3f * (innerRadius * innerRadius + innerRadius * throatRadius + throatRadius * throatRadius);
            }
        }

        public Vector3 CannonTarget => transform.TransformPoint(new Vector3(0f, storageTopLocalY + 0.3f, 0f));

        public void SetFeedRate(float ballsPerSecond)
        {
            float revsPerSecond = Mathf.Max(0f, ballsPerSecond) / (pockets * ballsPerPocket);
            _targetDegreesPerSecond = revsPerSecond * 360f;
        }

        public void SetFloodGate(bool open)
        {
            if (open == _floodOpen) return;
            _floodOpen = open;
            if (open)
            {
                Vector3 at = plateBody != null ? plateBody.position : transform.position;
                RaisePulse(at, 1f);
            }
        }

        /// <summary>
        /// Back-pressure input from the simulation (tick order: it reports after its ball pass, the next Step reads it).
        /// While the flood gate is open it eases shut as the tray fills or too many balls are moving.
        /// </summary>
        public void ReportLoad(int trayBalls, int activeBalls, int activeCap)
        {
            _trayBalls = trayBalls;
            _activeBalls = activeBalls;
            _activeCap = activeCap;
        }

        /// <summary>0 = closed, 1 = fully open.</summary>
        public float FloodOpenFraction => _floodT;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _angle = rng.NextFloat(0f, 360f / Mathf.Max(1, pockets));
            _agitatorAngle = rng.NextFloat(0f, 180f);
            _degreesPerSecond = 0f;
            _targetDegreesPerSecond = 0f;
            _floodOpen = false;
            _floodT = 0f;
            _trayBalls = 0;
            _activeBalls = 0;
            _activeCap = 0;
            Pose();
        }

        public override void Step(float simTime, float dt)
        {
            _degreesPerSecond = Mathf.MoveTowards(_degreesPerSecond, _targetDegreesPerSecond, rotorAccelDegrees * dt);
            _angle = Mathf.Repeat(_angle + _degreesPerSecond * dt, 360f);
            if (_degreesPerSecond > 0.01f || _floodOpen)
            {
                _agitatorAngle = Mathf.Repeat(_agitatorAngle + agitatorDegreesPerSecond * dt, 360f);
            }

            // Flood opens fast (the slam) but eases shut slowly when the tray or the whole machine is backed up.
            float want = 0f;
            if (_floodOpen)
            {
                want = 1f;
                if (trayCapacity > 0) want = Mathf.Min(want, Mathf.Clamp01((trayCapacity - _trayBalls) / (0.4f * trayCapacity)));
                if (_activeCap > 0) want = Mathf.Min(want, Mathf.Clamp01((_activeCap - _activeBalls) / (0.2f * _activeCap)));
            }

            float rate = want > _floodT ? 1f / floodSlideSeconds : 0.5f / floodSlideSeconds;
            _floodT = Mathf.MoveTowards(_floodT, want, dt * rate);
            Pose();
        }

        void Pose()
        {
            if (rotorBody != null)
            {
                rotorBody.MovePosition(transform.TransformPoint(rotorRestPosition));
                rotorBody.MoveRotation(transform.rotation * rotorRestRotation * Quaternion.Euler(_angle, 0f, 0f));
            }

            if (agitatorBody != null)
            {
                agitatorBody.MovePosition(transform.TransformPoint(agitatorRestPosition));
                agitatorBody.MoveRotation(transform.rotation * agitatorRestRotation * Quaternion.Euler(0f, _agitatorAngle, 0f));
            }

            if (plateBody != null)
            {
                float e = _floodT * _floodT * (3f - 2f * _floodT);
                plateBody.MovePosition(transform.TransformPoint(plateRestPosition + plateOpenOffset * e));
                plateBody.MoveRotation(transform.rotation * plateRestRotation);
            }
        }
    }
}
