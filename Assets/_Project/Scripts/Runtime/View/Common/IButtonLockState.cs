namespace Game.View
{
    /// <summary>누른 버튼이 해금 조건 때문에 잠겨 있는지 공통 입력 피드백에 알린다</summary>
    public interface IButtonLockState
    {
        bool IsLockedButton(UnityEngine.UI.Button button);
    }
}
