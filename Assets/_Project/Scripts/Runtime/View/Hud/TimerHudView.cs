using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>플레이 시간을 mm:ss로 보여준다. 결과로 제출되는 값(StageClock)과 같은 시간을 쓴다.
    /// 매 프레임 바뀌는 값이라 메시지 대신 Update에서 직접 읽는다 (docs/architecture.md)</summary>
    public sealed class TimerHudView : HudView
    {
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _spawnText;
        private EnemySpawner _spawner;
        private int _shownCountdown = -1;

        private StageClock _clock;
        private int _shownSeconds = -1;

        [Inject]
        public void Construct(StageClock clock, EnemySpawner spawner = null)
        {
            _clock = clock;
            _spawner = spawner;
        }

        private void Update()
        {
            if (_clock == null)
            {
                return;
            }

            if (_spawnText != null)
            {
                float remaining = _spawner?.NextSpawnSeconds ?? 0f;
                int countdown = remaining > 0f && remaining <= 5f ? Mathf.CeilToInt(remaining) : 0;
                if (countdown != _shownCountdown)
                {
                    _shownCountdown = countdown;
                    if (countdown > 0) { _spawnText.SetL10n("hud.nextSpawn", countdown); }
                    else { _spawnText.text = string.Empty; }
                    _spawnText.gameObject.SetActive(countdown > 0);
                }
            }

            // 초가 바뀔 때만 문자열을 만든다
            int seconds = Mathf.FloorToInt(_clock.ElapsedSeconds);
            if (seconds == _shownSeconds)
            {
                return;
            }

            _shownSeconds = seconds;
            _timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
