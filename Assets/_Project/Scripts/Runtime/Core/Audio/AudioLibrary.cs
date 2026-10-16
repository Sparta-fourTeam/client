using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public sealed class AudioEntry
    {
        [Tooltip("코드와 메시지가 부르는 소리 ID")]
        public string id;
        [Tooltip("효과음은 이 중 하나를 무작위로 재생하고, 배경음은 첫 번째 클립을 쓴다")]
        public AudioClip[] clips;
        [Tooltip("전투가 멈추면 재생 위치를 보존해 멈추고, 전투 재개 시 이어서 재생한다")]
        public bool pauseWithBattle;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("효과음 피치를 1 ± 이 값 안에서 무작위로 흔든다")]
        [Range(0f, 0.5f)] public float pitchVariance;
        [Tooltip("같은 ID를 다시 재생하기까지 최소 간격(초). 한 프레임에 같은 소리가 겹쳐 커지는 것을 막는다")]
        [Min(0f)] public float minInterval = 0.05f;
    }

    /// <summary>소리 ID를 클립과 재생 설정에 잇는 표. 소리 연결은 코드가 아니라 이 데이터로 관리한다.
    /// 클립이 아직 없는 ID도 자리로 등록해 두면 재생 요청이 조용히 무시된다</summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Project Nova/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioLibrary";

        [SerializeField] private List<AudioEntry> entries = new List<AudioEntry>();

        private Dictionary<string, AudioEntry> _byId;

        public bool TryGet(string id, out AudioEntry entry)
        {
            _byId ??= BuildIndex();
            return _byId.TryGetValue(id ?? string.Empty, out entry);
        }

        private Dictionary<string, AudioEntry> BuildIndex()
        {
            var index = new Dictionary<string, AudioEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.id))
                {
                    continue;
                }

                if (!index.TryAdd(entry.id, entry))
                {
                    Debug.LogWarning($"[AudioLibrary] 소리 ID '{entry.id}'가 중복되어 처음 항목만 쓴다", this);
                }
            }

            return index;
        }

        private void OnValidate()
        {
            _byId = null;
        }
    }
}
