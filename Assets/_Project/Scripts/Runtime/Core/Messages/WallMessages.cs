namespace Game.Core.Messages
{
    public readonly struct WallHpChanged
    {
        public int Current { get; }
        public int Max { get; }

        public WallHpChanged(int current, int max)
        {
            Current = current;
            Max = max;
        }
    }

    public readonly struct WallDestroyed
    {

    }
}
