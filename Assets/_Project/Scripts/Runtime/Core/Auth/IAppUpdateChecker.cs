using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    public interface IAppUpdateChecker
    {
        /// <summary>설치 계정이 다운로드할 수 있는 업데이트가 없으면 null을 반환한다.</summary>
        UniTask<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken);
    }

    public sealed class AppUpdateInfo
    {
        public string StoreUrl { get; }

        public AppUpdateInfo(string storeUrl)
        {
            StoreUrl = storeUrl;
        }
    }
}
