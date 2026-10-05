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
    }
}
