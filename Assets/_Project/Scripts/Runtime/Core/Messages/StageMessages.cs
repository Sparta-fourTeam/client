namespace Game.Core.Messages
{
    public enum StageOutcome
    {
        Clear,
        Fail
    }

    public readonly struct StageEnded
    {
        public StageOutcome Outcome { get; }

        public StageEnded(StageOutcome outcome)
        {
            Outcome = outcome;
        }
    }

    public readonly struct FinalWaveCleared
    {

    }
}
