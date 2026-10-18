using UnityEngine;

namespace HarborRacer
{
    /// <summary>Layer indexes and masks (set up by Tools > HarborRacer > Apply Layers And Collision Matrix).</summary>
    public static class Layers
    {
        public static readonly int Drivable = LayerMask.NameToLayer("Drivable");
        public static readonly int OffRoad = LayerMask.NameToLayer("OffRoad");
        public static readonly int Wall = LayerMask.NameToLayer("Wall");
        public static readonly int Kart = LayerMask.NameToLayer("Kart");
        public static readonly int Zone = LayerMask.NameToLayer("Zone");
        public static readonly int Item = LayerMask.NameToLayer("Item");
        public static readonly int ItemBox = LayerMask.NameToLayer("ItemBox");
        public static readonly int Hazard = LayerMask.NameToLayer("Hazard");
        public static readonly int Scenery = LayerMask.NameToLayer("Scenery");
        public static readonly int Waypoint = LayerMask.NameToLayer("Waypoint");

        /// <summary>Surfaces a kart can stand on (ground probe mask).</summary>
        public static readonly int GroundMask = (1 << Drivable) | (1 << OffRoad);
    }
}
