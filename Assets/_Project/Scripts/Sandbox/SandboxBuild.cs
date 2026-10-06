using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>샌드박스에서 시험하는 구성: 스킬 하나, 카드를 얻은 순서, 영구 레벨.
    /// 카드를 빼는 API가 없으므로 구성이 바뀔 때마다 이 목록을 처음부터 다시 적용한다.</summary>
    [Serializable]
    public sealed class SandboxBuild
    {
        public int skillId;
        public int permanentLevel;
        public List<string> cards = new List<string>();

        public SandboxBuild Clone() => new SandboxBuild { skillId = skillId, permanentLevel = permanentLevel, cards = new List<string>(cards) };

        public string ToJson() => JsonUtility.ToJson(this);

        public static bool TryParse(string json, out SandboxBuild build)
        {
            build = null;
            if (string.IsNullOrEmpty(json)) { return false; }
            try { build = JsonUtility.FromJson<SandboxBuild>(json); }
            catch (ArgumentException) { return false; }
            if (build == null) { return false; }
            build.cards ??= new List<string>();
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
