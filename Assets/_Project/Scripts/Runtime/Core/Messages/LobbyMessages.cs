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

    public readonly struct ProgressChanged
    {
        public int HighestUnlockedStage { get; }

        public ProgressChanged(int highestUnlockedStage)
        {
            HighestUnlockedStage = highestUnlockedStage;
        }
    }

    /// <summary>로비 요청(에너지 회복 등)이 거절됐다. 발행: EnergyRecovery</summary>
    public readonly struct LobbyRequestFailed
    {
        public string Code { get; }

        public LobbyRequestFailed(string code)
        {
            Code = code;
        }
    }

}
