namespace PixelRoad.Data
{
    public sealed class SpotRuntimeState
    {
        public SpotDefinition Definition { get; private set; }
        public bool IsUnlocked { get; private set; }

        public SpotRuntimeState(SpotDefinition definition, bool isUnlocked)
        {
            Definition = definition;
            IsUnlocked = isUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }
}
