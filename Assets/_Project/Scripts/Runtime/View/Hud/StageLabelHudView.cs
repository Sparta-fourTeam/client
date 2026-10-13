using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>지금 플레이 중인 스테이지 번호를 "Stage N"으로 보여준다</summary>
    public sealed class StageLabelHudView : HudView
    {
        [SerializeField] private TMP_Text _label;

        private StageContext _stageContext;

        [Inject]
        public void Construct(StageContext stageContext)
        {
            _stageContext = stageContext;
        }

        private void Start()
        {
            // StageId가 0이면 로비를 거치지 않고 Stage 씬을 직접 연 경우라 표시하지 않는다
            _label.text = _stageContext != null && _stageContext.StageId > 0 ? $"Stage {_stageContext.StageId}" : string.Empty;
        }
    }
}
