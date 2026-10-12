using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>살아 있는 적 전체를 발끝 높이로 한 줄로 세워 그리기 순서를 정한다.
    /// 지난 프레임의 줄을 유지하고, 앞 이웃보다 발끝이 Hysteresis 넘게 낮아졌을 때만 앞으로 보낸다.
    /// 그래서 높이가 같은 적(벽 앞에 멈춘 적)끼리도 순서가 같아지지 않고, 높이가 비슷한 적끼리 앞뒤가 떨리지 않는다</summary>
    public static class EnemyDepthSorter
    {
        /// <summary>앞 이웃을 앞지르려면 발끝이 이만큼 더 낮아야 한다</summary>
        public const float Hysteresis = 0.03f;

        /// <summary>줄에서 이웃한 적 사이의 순서 간격. 몸 순서 +1은 상태이상, -1은 사망 이펙트가 쓰므로 이웃 적과 겹치지 않게 띄운다</summary>
        public const int Spacing = 3;

        private static readonly List<EnemyDepthSort> s_line = new(); // 0번이 맨 앞(발끝이 가장 낮은 적)
        private static readonly List<EnemyDepthSort> s_added = new();
        private static int s_sortedFrame = -1;

        // 도메인 리로드를 끈 Play Mode에서도 이전 플레이의 줄이 남지 않게 한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_line.Clear();
            s_added.Clear();
            s_sortedFrame = -1;
        }

        /// <summary>다음 정렬 때 줄에 넣는다</summary>
        public static void Add(EnemyDepthSort enemy)
        {
            if (!s_line.Contains(enemy) && !s_added.Contains(enemy))
            {
                s_added.Add(enemy);
            }
        }

        public static void Remove(EnemyDepthSort enemy)
        {
            s_line.Remove(enemy);
            s_added.Remove(enemy);
        }

        /// <summary>적마다 LateUpdate에서 부르지만 프레임마다 한 번만 정렬한다</summary>
        internal static void SortOncePerFrame()
        {
            if (s_sortedFrame == Time.frameCount)
            {
                return;
            }

            s_sortedFrame = Time.frameCount;
            Sort();
        }

        /// <summary>줄을 다시 세우고 순서가 바뀐 적에게만 쓴다. 테스트가 바로 부르기도 한다</summary>
        public static void Sort()
        {
            s_line.RemoveAll(enemy => enemy == null);
            foreach (var enemy in s_added)
            {
                if (enemy != null)
                {
                    Insert(enemy);
                }
            }
            s_added.Clear();

            // 지난 줄이 거의 정렬되어 있으므로 삽입 정렬이 거의 한 번 훑기로 끝난다
            for (int i = 1; i < s_line.Count; i++)
            {
                var enemy = s_line[i];
                float foot = enemy.FootY;
                int j = i;
                while (j > 0 && foot < s_line[j - 1].FootY - Hysteresis)
                {
                    s_line[j] = s_line[j - 1];
                    j--;
                }
                s_line[j] = enemy;
            }

            // 줄이 아주 길면 맨 뒤쪽 적들은 BackOrder에서 같은 순서가 된다
            for (int i = 0; i < s_line.Count; i++)
            {
                s_line[i].Apply(Mathf.Max(EnemyDepthSort.FrontOrder - i * Spacing, EnemyDepthSort.BackOrder));
            }
        }

        // 새 적은 히스테리시스 없이 높이에 맞는 자리에 넣는다. 높이가 같으면 먼저 있던 적이 앞
        private static void Insert(EnemyDepthSort enemy)
        {
            float foot = enemy.FootY;
            int index = 0;
            while (index < s_line.Count && s_line[index].FootY <= foot)
            {
                index++;
            }
            s_line.Insert(index, enemy);
        }
    }
}
