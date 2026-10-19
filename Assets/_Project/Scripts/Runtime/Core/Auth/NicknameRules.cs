using System.Text.RegularExpressions;

namespace Game.Core
{
    /// <summary>닉네임의 중복은 서버에서 검사하며 한글 완성형·영문·숫자·밑줄 2~20자로 제한한다</summary>
    public static class NicknameRules
    {
        public static bool IsValid(string value) => value != null && Regex.IsMatch(value, @"\A[가-힣A-Za-z0-9_]{2,20}\z");
    }
}
