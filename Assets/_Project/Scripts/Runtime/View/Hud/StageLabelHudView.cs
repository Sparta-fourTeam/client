using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>지금 플레이 중인 스테이지 번호를 "Stage N"으로 보여준다. Stage 씬을 바로 열어 로비에서 정한 스테이지가 없으면
    /// 스코프가 고른 첫 스테이지(StageDefinition)의 번호를 보여준다</summary>
    public sealed class StageLabelHudView : HudView
    {
        [SerializeField] private TMP_Text _label;

        private StageDefinition _stage;

        [Inject]
        public void Construct(StageDefinition stage)
        {
            _stage = stage;
        }

        private void Start()
        {
            if (_stage != null) { _label.SetL10n("hud.stage", _stage.Id); }
            else { _label.text = string.Empty; }
        }
    }
}
