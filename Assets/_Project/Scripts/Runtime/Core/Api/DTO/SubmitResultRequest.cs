using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public class SubmitResultRequest
    {
        public string battleId;
        public bool cleared;
        public int reachedWave, completedWaves, kills;
        public float playTime;
        public List<string> buildLog;
        public string createdAt;
    }
}
