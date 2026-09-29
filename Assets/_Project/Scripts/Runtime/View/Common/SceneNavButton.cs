using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>테스트용 씬 이동 버튼. 클릭 시 지정된 대상으로 이동한다</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SceneNavButton : MonoBehaviour
    {
        private enum Target
        {
            Lobby,
            Stage
        }

        [SerializeField] private Target target;
        [SerializeField] private int stageId = 1;

        private ISceneNavigator _nav;

        [Inject]
        public void Construct(ISceneNavigator nav)
        {
            _nav = nav;
        }

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            switch (target)
            {
                case Target.Lobby:
                    _nav.GoToLobby().Forget(Debug.LogException);
                    break;
                case Target.Stage:
                    _nav.GoToStage(stageId).Forget(Debug.LogException);
                    break;
            }
        }
    }
}
