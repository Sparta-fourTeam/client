namespace Game.Core.Messages
{
    public readonly struct StageStateChanged
    {
        public StageState State { get; }

        public StageStateChanged(StageState state)
        {
            State = state;
        }
    }


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

    /// <summary>결과 제출까지 끝난 뒤 결과 UI(ResultPopup 등)가 구독하는 최종 결과</summary>
    public readonly struct StageResult
    {
        public bool Cleared { get; }
        public int Kills { get; }
        public int ReachedWave { get; }
        public float PlayTime { get; }
        public int RewardGold { get; }

        public StageResult(bool cleared, int kills, int reachedWave, float playTime, int rewardGold)
        {
            Cleared = cleared;
            Kills = kills;
            ReachedWave = reachedWave;
            PlayTime = playTime;
            RewardGold = rewardGold;
        }
    }

    /// <summary>Lobby에서 전투 발급이 거절됐을 때 발행(BattleLauncher). 로비 UI가 이유를 보여준다</summary>
    public readonly struct StartFailed
    {
        public string Code { get; }

        public StartFailed(string code)
        {
            Code = code;
        }
    }

    /// <summary>결과 제출이 거절됐을 때 발행(StageManager)</summary>
    public readonly struct SubmitRejected
    {
        public string Code { get; }

        public SubmitRejected(string code)
        {
            Code = code;
        }
    }
}
