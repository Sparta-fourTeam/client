namespace Game.Core
{
    /// <summary>씬 전환 대상. 실제 씬 이름(Boot/Loading/Lobby/Stage.unity)과 1:1로 대응한다</summary>
    public enum SceneId
    {
        Boot,
        Loading,
        Lobby,
        Stage
    }
}
