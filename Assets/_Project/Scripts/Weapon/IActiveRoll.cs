namespace Swarm.Weapon
{
    public interface IActiveRoll
    {
        int Level { get; }
        float CooldownProgress01 { get; }
        void TryRoll();
    }
}
