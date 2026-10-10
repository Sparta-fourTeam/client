using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Selects targets by wall proximity from the nearest eight candidates.</summary>
    public sealed class SkillTargetSelector
    {
        private const int CandidateCount = 8;
        private readonly IEnemyTargetProvider provider;
        private readonly List<IEnemyTarget> candidates = new(CandidateCount);
        private readonly List<IEnemyTarget> selected = new(CandidateCount);

        public SkillTargetSelector(IEnemyTargetProvider provider) => this.provider = provider;

        // The returned buffer belongs to this selector and is valid until the next Select call.
        public IReadOnlyList<IEnemyTarget> Select(Vector2 origin, float range)
        {
            candidates.Clear();
            selected.Clear();
            provider.GetNearest(origin, CandidateCount, candidates);
            float rangeSquared = range * range;
            foreach (var candidate in candidates)
            {
                if (candidate != null && (candidate.Position - origin).sqrMagnitude <= rangeSquared)
                {
                    selected.Add(candidate);
                }
            }
            selected.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));
            return selected;
        }

        /// <summary>벽 앞선(baseY)에서 위로 range 이내의 적을 가로 위치(x)와 상관없이 고른다 (플레이어에서 발사되지 않는 굴러가는 공격용).
        /// 벽에 가까운(y가 낮은) 순이며 최대 8마리다. 반환 목록은 다음 호출 전까지만 유효하다</summary>
        public IReadOnlyList<IEnemyTarget> SelectLane(float baseY, float range)
        {
            candidates.Clear();
            selected.Clear();
            provider.GetNearest(new Vector2(0, baseY), int.MaxValue, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate != null && !candidate.IsDead && candidate.Position.y - baseY <= range) { selected.Add(candidate); }
            }
            selected.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));
            if (selected.Count > CandidateCount) { selected.RemoveRange(CandidateCount, selected.Count - CandidateCount); }
            return selected;
        }
    }
}
