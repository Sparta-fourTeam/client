using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>Loading 화면의 "화면을 터치하세요". 저장된 로그인 정보로 자동 로그인한 뒤 화면 아무 곳이나 누를 때까지 기다린다.
    /// 패널은 화면 전체를 덮는 터치 영역이며 시작할 때는 숨긴다(UiInitialVisibility)</summary>
    public sealed class TapToStartView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _tapArea;

        private UniTaskCompletionSource _pending;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _tapArea.onClick.AddListener(() =>
            {
                var pending = _pending;
                _pending = null;
                pending?.TrySetResult();
            });
        }

        // 화면이 사라지면(씬 전환 등) 기다리던 흐름도 끝낸다
        protected override void DisposeView()
        {
            _pending?.TrySetCanceled();
            _pending = null;
        }

        public async UniTask WaitForTapAsync()
        {
            _pending = new UniTaskCompletionSource();
            _panel.SetActive(true);
            await _pending.Task;
        }
    }
}
