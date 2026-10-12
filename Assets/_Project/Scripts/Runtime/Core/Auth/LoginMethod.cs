namespace Game.Core
{
    /// <summary>로그인 방식. 게스트는 기기에서 만든 계정 ID로, 나머지는 외부 계정(ISocialSignIn)으로 로그인한다</summary>
    public enum LoginMethod
    {
        Guest,
        Google,
        Apple,
    }
}
