namespace Swarm.Weapon
{
    public interface IActiveRoll
    {
        int Level { get; }
        float CooldownProgress01 { get; }
        float CooldownRemaining { get; }
        void TryRoll();
    }
}
