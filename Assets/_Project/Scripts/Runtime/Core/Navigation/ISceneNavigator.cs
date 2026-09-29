using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>씬 전환을 담당한다. 커튼을 닫고 씬을 로드한 뒤 다시 연다</summary>
    public interface ISceneNavigator
    {
        SceneId Current { get; }
        UniTask GoToLoading();
        UniTask GoToLobby();
        UniTask GoToBoot();
        UniTask GoToStage(int stageId);
        UniTask RestartStage();
    }
}
