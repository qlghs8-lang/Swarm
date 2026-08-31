namespace Swarm.Player
{
    /// <summary>
    /// Every stat a passive can raise. Each entry used to be its own ScriptableObject subclass —
    /// nine run passives and eight permanent upgrades whose only difference was which single
    /// PlayerStats method they called — so adding a stat meant writing two more classes.
    /// </summary>
    public enum StatType
    {
        // Explicit values: these are serialized as integers into the .asset files, so reordering
        // the list without keeping the numbers would silently repoint every existing asset.
        PrimaryStat = 0,
        AttackPower = 1,
        MagicPower = 2,
        MaxHealth = 3,
        MoveSpeed = 4,
        Defense = 5,
        CooldownReduction = 6,
        Penetration = 7,
        MagnetRadius = 8,
        ProjectileCount = 9,
        AreaSize = 10,
        Luck = 11,
    }
}
