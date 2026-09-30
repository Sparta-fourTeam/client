using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.Boot
{
    /// <summary>샌드박스 씬 전용. Loading 씬을 거치지 않으므로 시작할 때 로컬 저장 데이터로 PlayerProfile을 채운다 (LoadingFlow와 같은 순서)</summary>
    public sealed class SandboxProfileLoader : MonoBehaviour
    {
        private IAuthApi _auth;
        private IPlayerApi _player;
        private PlayerProfile _profile;

        [Inject]
        public void Construct(IAuthApi auth, IPlayerApi player, PlayerProfile profile)
        {
            _auth = auth;
            _player = player;
            _profile = profile;
        }

        private void Start()
        {
            if (_profile == null)
            {
                Debug.LogError("[Sandbox] 주입되지 않았습니다. LifetimeScope의 Auto Inject Game Objects에 이 오브젝트를 넣어 주세요", this);
                return;
            }

            LoadAsync().Forget(Debug.LogException);
        }

        private async UniTask LoadAsync()
        {
            await _auth.Login();
            PlayerSnapshot snapshot = await _player.GetMe();
            _profile.Apply(snapshot);
            Debug.Log($"[Sandbox] PlayerProfile 로드: 골드 {_profile.Gold}, 에너지 {_profile.EnergyStored}/{_profile.EnergyConfig.Max}");
        }
    }
}
