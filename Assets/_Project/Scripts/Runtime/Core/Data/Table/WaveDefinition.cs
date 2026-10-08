using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Core
{
    /// <summary>웨이브에 나오는 몬스터 한 종류와 마릿수</summary>
    [Serializable]
    public class WaveSpawn
    {
        public int MonsterId;
        public int Count;

        /// <summary>몬스터 등급. 데이터에 적지 않고 Monsters 테이블에서 읽을 때 채운다. 엘리트·보스는 웨이브당 한 번만 나오고 반복 스폰에 끼지 않는다</summary>
        [JsonIgnore] public EnemyType Type;
    }

    /// <summary>스테이지 한 웨이브의 스폰 계획. 어떤 몬스터가 몇 마리 나오는지를 그대로 적는다</summary>
    [Serializable]
    public class WaveDefinition
    {
        public List<WaveSpawn> Spawns = new();

        /// <summary>웨이브를 끝내려면 처치해야 하는 수. Spawns의 마릿수 합계다</summary>
        [JsonIgnore]
        public int EnemyCount
        {
            get
            {
                int total = 0;
                if (Spawns != null)
                {
                    foreach (var spawn in Spawns) { total += spawn?.Count ?? 0; }
                }

                return total;
            }
        }
    }
}
