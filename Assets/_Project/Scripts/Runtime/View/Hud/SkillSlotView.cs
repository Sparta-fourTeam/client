using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>스킬 한 칸: 아이콘, 쿨타임 원형 채움, 레벨. 스킬이 없는 칸은 빈 틀만 보인다.
    /// 쿨타임은 매 프레임 바뀌는 값이라 메시지를 받지 않고 Update에서 직접 읽는다</summary>
    public sealed class SkillSlotView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _levelText;

        private ISkillStatus _status;

        private void Awake()
        {
            // 스코프 주입(LifetimeScope.Awake, 실행 순서 -5000)이 이 Awake보다 먼저라서, 버퍼에 남아 있던 SkillChanged가
            // 이미 Bind를 마쳤을 수 있다. 그때는 지우지 않는다. 아직 아무것도 못 받은 칸만 빈 틀로 만든다
            if (_status == null)
            {
                Clear();
            }
        }

        public void Bind(ISkillStatus status, Sprite icon)
        {
            _status = status;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _levelText.text = status.Level.ToString();
            _levelText.gameObject.SetActive(true);
            _cooldownOverlay.gameObject.SetActive(true);
            _cooldownOverlay.fillAmount = Mathf.Clamp01(status.CooldownRatio);
        }

        public void Clear()
        {
            _status = null;
            _icon.enabled = false;
            _levelText.gameObject.SetActive(false);
            _cooldownOverlay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_status == null)
            {
                return;
            }

            // 카드 선택과 일시정지 중에는 timeScale이 0이라 쿨타임도 멈춘 채 그대로 보인다
            _cooldownOverlay.fillAmount = Mathf.Clamp01(_status.CooldownRatio);
        }
    }
}
