using Game.Core;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>스킬 HUD를 실제 무기 없이 확인하려고 만든 가짜 ISkillStatus.
    /// WeaponBase가 ISkillStatus를 구현하고 WeaponController가 SkillChanged를 발행하게 되면 필요 없어진다.
    /// 쿨타임은 진짜처럼 "방금 쓴 직후 1 -> 0으로 줄고, 0이 되면 다시 발사"를 반복한다 (Time.deltaTime 기준이라 timeScale 0에서 멈춘다)</summary>
    public sealed class FakeSkillStatus : ISkillStatus
    {
        private readonly float _cooldown;
        private float _remaining;

        public int Id { get; }
        public int Level { get; set; }
        public string IconKey { get; }

        public float CooldownRatio => _cooldown <= 0f ? 0f : Mathf.Clamp01(_remaining / _cooldown);

        public FakeSkillStatus(int id, int level, string iconKey, float cooldown, float initialRemaining)
        {
            Id = id;
            Level = level;
            IconKey = iconKey;
            _cooldown = cooldown;
            _remaining = initialRemaining;
        }

        /// <summary>WeaponBase.Tick과 같은 규칙으로 쿨타임을 진행시킨다</summary>
        public void Tick(float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = _cooldown;
            }
        }
    }
}
