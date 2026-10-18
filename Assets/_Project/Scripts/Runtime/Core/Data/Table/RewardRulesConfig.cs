using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Core
{
    /// <summary>아이템의 지급 종류와 랜덤 여부. ID별 분류도 기준 데이터로 관리한다.</summary>
    [Serializable]
    public sealed class RewardItemRule
    {
        [JsonProperty(Required = Required.Always)] public string Kind;
        [JsonProperty(Required = Required.Always)] public bool Random;
    }

    /// <summary>서버 RewardRules와 같은 지급 일정·아이템 분류 스냅샷. 출시 수치를 코드에 두지 않는다.</summary>
    [Serializable]
    public sealed class RewardRulesConfig
    {
        [JsonProperty(Required = Required.Always)] public Dictionary<string, RewardSchedule> Schedules;
        [JsonProperty(Required = Required.Always)] public Dictionary<string, RewardItemRule> Items;
        public int? Version;
        [JsonIgnore] public RewardSchedule Coin => Schedules["Coin"];

        public bool TryGetItem(string itemId, out RewardSchedule schedule)
        {
            if (itemId != null && Items.TryGetValue(itemId, out var item))
            { return Schedules.TryGetValue(item.Kind, out schedule); }
            schedule = default;
            return false;
        }

        public void Validate()
        {
            var kinds = new HashSet<string>(new[] { "Coin", "SkillMaterial", "EquipmentMaterial", "GemChest" }, StringComparer.Ordinal);
            if (Schedules == null || !kinds.SetEquals(Schedules.Keys) || Items == null)
            { throw new InvalidOperationException("RewardRules: 일정과 아이템 분류 전체가 필요합니다."); }
            foreach (var schedule in Schedules.Values)
            {
                if (schedule.Start < 0 || schedule.PerWave < 0 || schedule.BonusWave < 1
                    || schedule.BonusWave > StageRewardRules.WaveCount
                    || (long)schedule.Start + (long)schedule.PerWave * StageRewardRules.WaveCount > int.MaxValue)
                { throw new InvalidOperationException("RewardRules: 지급 일정이 잘못되었습니다."); }
            }
            foreach (var item in Items)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null || item.Value.Kind == null
                    || item.Value.Kind == "Coin" || !kinds.Contains(item.Value.Kind)
                    || (item.Value.Random && item.Value.Kind == "GemChest"))
                { throw new InvalidOperationException("RewardRules: 아이템 분류가 잘못되었습니다."); }
            }
        }
    }
}
