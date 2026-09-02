namespace Swarm
{
    /// <summary>
    /// Names of the project's sorting layers, bottom to top.
    /// Draw order is decided by the layer first; <c>sortingOrder</c> only breaks ties inside one layer,
    /// so keep per-layer orders small and local instead of reaching for large global offsets.
    /// The list here must stay in sync with Project Settings > Tags and Layers > Sorting Layers.
    /// </summary>
    public static class SortingLayers
    {
        /// <summary>Arena floor tilemap.</summary>
        public const string GROUND = "Ground";

        /// <summary>Flat marks painted on the floor — worn dirt, the centre sigil, ground-level telegraphs and pools.</summary>
        public const string DECAL = "Decal";

        /// <summary>Scattered arena decoration.</summary>
        public const string PROP = "Prop";

        /// <summary>Ring wall.</summary>
        public const string WALL = "Wall";

        /// <summary>Experience and gold drops.</summary>
        public const string PICKUP = "Pickup";

        /// <summary>The player, the enemies and the boss share one layer.
        /// They stand on the same ground, so depth between them is decided by the custom
        /// transparency sort axis (0, 1, 0) — whoever stands lower on screen is drawn in front.
        /// Splitting the player into a layer of his own would draw him over an enemy standing
        /// in front of him, which reads as the enemy being behind: the bodies overlap because
        /// the player's physics radius (0.20) is far smaller than his sprite (0.56 wide).</summary>
        public const string CHARACTER = "Character";

        /// <summary>Weapon effects, indicators and projectiles.</summary>
        public const string EFFECT = "Effect";

        /// <summary>Damage numbers and anything else that must never be covered.</summary>
        public const string OVERLAY = "Overlay";
    }
}
