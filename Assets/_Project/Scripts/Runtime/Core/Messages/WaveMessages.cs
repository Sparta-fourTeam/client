namespace Game.Core.Messages
{
    public readonly struct WaveGaugeFilled
    {
        public bool IsFinalWave { get; }

        public WaveGaugeFilled(bool isFinalWave)
        {
            IsFinalWave = isFinalWave;
        }
    }
}
