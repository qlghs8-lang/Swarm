namespace Swarm.Weapon
{
    public interface ILevelableWeapon
    {
        bool IsAvailable { get; }
        int Level { get; }
        int MaxLevel { get; }
        string CardDisplayName { get; }
        string CardDescription { get; }
        void SetAvailable(bool available);
        void LevelUp();
    }
}
