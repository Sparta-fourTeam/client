using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인법(무기) 한 줄: 아이콘, 레벨, 이번 판에서 준 피해량</summary>
    public readonly struct ResultDamageRow
    {
        public Sprite Icon { get; }
        public int Level { get; }
        public int Damage { get; }

        public ResultDamageRow(Sprite icon, int level, int damage)
        {
            Icon = icon;
            Level = level;
            Damage = damage;
        }
    }

    /// <summary>"인법 피해량" 영역. 무기별 피해 데이터가 생기면 Show를 호출한다. 호출 전이나 목록이 비면 영역 전체가 꺼진다</summary>
    public sealed class ResultDamageListView : MonoBehaviour
    {
        [Serializable]
        private sealed class Row
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Level;
            public TMP_Text Damage;
        }

        [SerializeField] private Row[] _rows;

        public void Show(IReadOnlyList<ResultDamageRow> rows)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                bool has = i < rows.Count;
                _rows[i].Root.SetActive(has);
                if (!has)
                {
                    continue;
                }

                // 아이콘이 없으면 프리팹의 자리표시 스프라이트를 그대로 둔다
                if (rows[i].Icon != null)
                {
                    _rows[i].Icon.sprite = rows[i].Icon;
                }

                _rows[i].Level.text = $"Lv. {rows[i].Level}";
                _rows[i].Damage.text = rows[i].Damage.ToString("N0");
            }

            gameObject.SetActive(rows.Count > 0);
        }
    }
}
