using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using UnityEngine;

namespace Game.View
{
    /// <summary>스킬별 피해량을 결과 팝업의 "인법 피해량" 줄(아이콘·레벨·피해량)로 바꾼다</summary>
    public static class SkillDamageRows
    {
        public const int MaxRows = 5;

        /// <summary>피해량이 큰 순서로 최대 MaxRows줄. 지금 보유하지 않은 스킬(보유 목록에 없음)은 레벨을 알 수 없어 뺀다.
        /// 피해량이 0인 스킬도 보유 중이면 보이지 않는다 (피해를 준 스킬만 목록에 있다)</summary>
        public static List<ResultDamageRow> Build(IReadOnlyList<SkillDamage> damages, IReadOnlyList<ISkillStatus> owned,
            Func<ISkillStatus, Sprite> icon)
        {
            var rows = new List<ResultDamageRow>();
            if (damages == null || owned == null) { return rows; }
            foreach (var damage in damages)
            {
                if (rows.Count >= MaxRows) { break; }
                ISkillStatus status = null;
                foreach (var candidate in owned)
                {
                    if (candidate.Id == damage.SkillId) { status = candidate; break; }
                }

                if (status == null) { continue; }
                rows.Add(new ResultDamageRow(icon?.Invoke(status), status.Level, damage.Damage));
            }

            return rows;
        }
    }
}
