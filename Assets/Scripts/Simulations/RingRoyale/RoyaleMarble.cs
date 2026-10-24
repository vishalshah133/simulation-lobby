using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.RingRoyale
{
    /// <summary>
    /// One marble in the ring: its colour (team) and whether it is on its way out. Pure state: the
    /// simulation writes it, presentation reads it. Wall contact comes from <see cref="WallHitSensor2D"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(WallHitSensor2D))]
    public sealed class RoyaleMarble : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;

        Rigidbody2D _body;
        WallHitSensor2D _sensor;

        public Rigidbody2D Body => _body != null ? _body : _body = GetComponent<Rigidbody2D>();

        public WallHitSensor2D Sensor => _sensor != null ? _sensor : _sensor = GetComponent<WallHitSensor2D>();

        /// <summary>Index into the config's colour list.</summary>
        public int Colour { get; internal set; }

        /// <summary>Birth order, 0 for the first marble. Stable across a replayed seed.</summary>
        public int Serial { get; internal set; }

        /// <summary>Inside the exit channel, past the inner wall, heading out.</summary>
        public bool Exiting { get; internal set; }

        /// <summary>Left the ring. Still flying, no longer counted or cloned.</summary>
        public bool Escaped { get; internal set; }

        /// <summary>Colour the presentation last painted this body; presentation-owned, never read by the sim.</summary>
        public int StyledColour { get; set; } = -1;

        public SpriteRenderer Renderer => _renderer;

        internal void ResetState(int serial, int colour)
        {
            Serial = serial;
            Colour = colour;
            Exiting = false;
            Escaped = false;
            Sensor.Clear();
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(SpriteRenderer renderer)
        {
            _renderer = renderer;
        }
    }
}
