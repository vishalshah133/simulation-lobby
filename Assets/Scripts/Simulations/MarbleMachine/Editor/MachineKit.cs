using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// The material kit every station builder draws from, created by <c>MachineMaterials</c> (WP3).
    /// Neutral, textured machinery; the balls are the only saturated colour. Contract file for mach-001.
    /// </summary>
    public sealed class MachineKit
    {
        /// <summary>Warm planked wood: frames, crates, trays.</summary>
        public Material wood;
        /// <summary>Darker stained wood: accents, bases.</summary>
        public Material woodDark;
        /// <summary>Dark grey asphalt: chute floors, ramps.</summary>
        public Material asphalt;
        /// <summary>White-grey speckled stone: funnels, cones.</summary>
        public Material terrazzo;
        /// <summary>Red-brown brick: chute side walls, the trapdoor run's lower level.</summary>
        public Material brick;
        /// <summary>Polished chrome: pegs, columns, rods, rails, fill rings.</summary>
        public Material chrome;
        /// <summary>Dark cast iron: gears, hubs, the cannon.</summary>
        public Material castIron;
        /// <summary>Copper: buckets, trim.</summary>
        public Material copper;
        /// <summary>Clear glass (transparent): silo, tubes, screw tube, peg-wall front. Keep a visible tint/edge.</summary>
        public Material glass;
        /// <summary>Perforated metal / net (alpha-clipped): the ski jump's miss net, grates.</summary>
        public Material mesh;
        /// <summary>Saturated blue rubber: bumpers, roller ends (the one non-ball accent).</summary>
        public Material rubber;
        /// <summary>Brushed brass: small trim.</summary>
        public Material brass;
        /// <summary>Off-white lacquer: piston tops, plinths.</summary>
        public Material whiteLacquer;

        /// <summary>One glossy plastic material per colour, indexed like <see cref="MachineConfig.colours"/>.</summary>
        public Material[] ballMaterials;

        /// <summary>Unlit-ish emissive material the sorter tints per scan (instanced via MaterialPropertyBlock).</summary>
        public Material emissive;

        public Mesh ballMesh;
    }

    /// <summary>Where the scene builder puts a station: its entry anchor in world space and the flow heading.</summary>
    public struct StationPlacement
    {
        public Vector3 entry;

        /// <summary>Yaw of local +Z (the flow direction), degrees about world up.</summary>
        public float yawDegrees;

        public StationPlacement(Vector3 entry, float yawDegrees)
        {
            this.entry = entry;
            this.yawDegrees = yawDegrees;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, yawDegrees, 0f);
    }
}
