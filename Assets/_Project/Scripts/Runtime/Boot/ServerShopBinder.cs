using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
using UnityEngine;
using VContainer.Unity;

namespace Game.Boot
{
    /// <summary>로비 UI 조립 뒤 같은 서버 상점 세션을 두 탭에 주입한다.</summary>
    public sealed class ServerShopBinder : IStartable
    {
        private readonly UiHost _host;
        private readonly IUiRegistry _ui;
        private readonly IServerShopViewSession _session;
        public ServerShopBinder(UiHost host, IUiRegistry ui, IServerShopViewSession session)
        { _host = host; _ui = ui; _session = session; }

        public void Start() => Bind().Forget(Debug.LogException);

        private async UniTask Bind()
        {
            try
            {
                await _host.WhenReady.AttachExternalCancellation(_host.GetCancellationTokenOnDestroy());
                _ui.Get<DailyShopView>().UseServerShop(_session);
                _ui.Get<GoldShopView>().UseServerShop(_session);
            }
            catch (OperationCanceledException) { }
        }
    }
}
