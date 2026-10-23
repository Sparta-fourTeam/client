using Cysharp.Threading.Tasks;
using Game.Core;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>읽지 않은 공지를 로비에서 하나씩 팝업으로 띄운다(오래된 것부터). 튜토리얼 가이드와 레벨업·해금·대사 장면이 끝나
    /// 로비가 조용할 때만 띄우므로 가이드 진행을 끊지 않는다. 전투 중에 온 공지는 저장돼 있다가 로비에 돌아오면 나온다.
    /// 팝업은 확인창(ConfirmPopupView)을 확인 버튼 하나로 재사용하고, 닫으면 읽음 처리한다.
    /// 서버가 다른 기기 로그인으로 이 세션을 끊었으면(forceLogout) 공지보다 먼저 안내 팝업을 띄우고 로그아웃해 로그인 화면으로 보낸다</summary>
    public sealed class NoticePresenterView : HudView, IUiBindable
    {
        private NoticeService _notices;
        private AccountService _account;
        private ConfirmPopupView _confirm;
        private DialogueView _dialogue;
        private LevelUpPopupView _levelUp;
        private ContentUnlockView _contentUnlock;
        private TutorialGuideView _guide;
        private bool _pending;
        private bool _showing;
        private bool _loggingOut;

        public void BindUi(IUiRegistry registry)
        {
            _confirm = registry.Get<ConfirmPopupView>();
            _dialogue = registry.Get<DialogueView>();
            _levelUp = registry.Get<LevelUpPopupView>();
            _contentUnlock = registry.Get<ContentUnlockView>();
            _guide = registry.Get<TutorialGuideView>();
        }

        [Inject]
        public void Construct(NoticeService notices, AccountService account, IBufferedSubscriber<NoticeChanged> changed)
        {
            _notices = notices;
            _account = account;
            _pending = notices.NextUnread() != null;
            Track(changed.Subscribe(_ => _pending = _notices.NextUnread() != null));
        }

        private void Update()
        {
            // 세션이 이미 끊겼으므로 가이드나 다른 팝업을 기다리지 않는다
            if (!_loggingOut && _notices.ForceLogoutPending)
            {
                ForceLogoutAsync().Forget(Debug.LogException);
                return;
            }

            if (!_pending || _showing || _loggingOut || !IsQuiet())
            {
                return;
            }

            var next = _notices.NextUnread();
            if (next == null)
            {
                _pending = false;
                return;
            }

            ShowAsync(next).Forget(Debug.LogException);
        }

        // 다른 흐름이 확인창을 쓰는 중에 열면 그 흐름이 취소되므로, 확인창과 연출 화면이 모두 닫혀 있을 때만 연다
        private bool IsQuiet() => _guide.IsIdle && !_confirm.IsOpen && !_dialogue.IsOpen && !_levelUp.IsOpen && !_contentUnlock.IsOpen;

        private async UniTask ForceLogoutAsync()
        {
            _loggingOut = true;
            try
            {
                _notices.ConsumeForceLogout();
                // 닫는 방법과 상관없이 로그아웃한다
                await _confirm.ShowAsync(new ConfirmRequest { Title = L10n.Get("menu.logout"), Message = L10n.Get("notice.forceLogout") });
                await _account.ForceLogoutAsync();
            }
            finally
            {
                _loggingOut = false;
            }
        }

        private async UniTask ShowAsync(Notice notice)
        {
            _showing = true;
            try
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = notice.Title, Message = notice.Content });
                _notices.MarkRead(notice.Id);
            }
            finally
            {
                _showing = false;
            }
        }
    }
}
