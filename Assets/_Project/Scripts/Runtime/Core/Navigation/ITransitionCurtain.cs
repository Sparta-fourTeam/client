using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>씬 전환 중 화면을 가리는 커튼. 실제 페이드 비주얼은 구현체 책임이다</summary>
    public interface ITransitionCurtain
    {
        UniTask Close();
        UniTask Open();
    }
}
