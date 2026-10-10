using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>구성 한 단계: 스킬을 켜거나(cardId가 비어 있음) 그 스킬의 카드를 하나 얻는다.</summary>
    [Serializable]
    public sealed class SandboxStep
    {
        public int skillId;
        public string cardId;

        public bool IsSkill => string.IsNullOrEmpty(cardId);
    }

    /// <summary>샌드박스에서 시험하는 구성: 켠 스킬과 카드를 얻은 순서(여러 스킬 가능), 영구 레벨.
    /// 카드를 빼는 API가 없으므로 구성이 바뀔 때마다 이 목록을 처음부터 다시 적용한다.</summary>
    [Serializable]
    public sealed class SandboxBuild
    {
        public int permanentLevel;
        /// <summary>true면 선행·배타·영구 레벨 조건을 보지 않고 카드를 고를 수 있다</summary>
        public bool ignoreConditions;
        public List<SandboxStep> steps = new List<SandboxStep>();

        // 스킬 하나만 담던 이전 저장 형식. 읽을 때만 쓰고 steps로 옮긴다.
        public int skillId;
        public List<string> cards = new List<string>();

        public SandboxBuild Clone()
        {
            var copy = new SandboxBuild { permanentLevel = permanentLevel, ignoreConditions = ignoreConditions };
            foreach (var step in steps) { copy.steps.Add(new SandboxStep { skillId = step.skillId, cardId = step.cardId }); }
            return copy;
        }

        public string ToJson() => JsonUtility.ToJson(this);

        public static bool TryParse(string json, out SandboxBuild build)
        {
            build = null;
            if (string.IsNullOrEmpty(json)) { return false; }
            try { build = JsonUtility.FromJson<SandboxBuild>(json); }
            catch (ArgumentException) { return false; }
            if (build == null) { return false; }
            build.steps ??= new List<SandboxStep>();
            build.cards ??= new List<string>();
            if (build.steps.Count == 0 && build.skillId > 0)
            {
                build.steps.Add(new SandboxStep { skillId = build.skillId });
                foreach (string card in build.cards) { build.steps.Add(new SandboxStep { skillId = build.skillId, cardId = card }); }
            }
            build.skillId = 0;
            build.cards.Clear();
            return true;
        }
    }

    /// <summary>자주 쓰는 구성을 슬롯 이름으로 저장한다 (PlayerPrefs, 이 기기에만 남는다).</summary>
    public static class SandboxPresets
    {
        private const string KeyPrefix = "SkillSandbox.Preset.";

        public static void Save(string slot, SandboxBuild build) => PlayerPrefs.SetString(KeyPrefix + slot, build.ToJson());

        public static bool TryLoad(string slot, out SandboxBuild build) =>
            SandboxBuild.TryParse(PlayerPrefs.GetString(KeyPrefix + slot, string.Empty), out build);

        public static bool Exists(string slot) => PlayerPrefs.HasKey(KeyPrefix + slot);
    }
}
