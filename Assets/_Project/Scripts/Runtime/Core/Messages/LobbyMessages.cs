namespace Game.Core.Messages
{
    public readonly struct WalletChanged
    {
        public int Gold { get; }

        public WalletChanged(int gold)
        {
            Gold = gold;
        }
    }

    public readonly struct EnergyChanged
    {
        public int Current { get; }
        public int Max { get; }

        public EnergyChanged(int current, int max)
        {
            Current = current;
            Max = max;
        }
    }


}
