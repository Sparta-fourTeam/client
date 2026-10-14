using System;
using System.Text;

namespace Game.Core
{
    /// <summary>문의하기 메일. 받는 주소와 제목, 문의 처리에 필요한 기기·계정 정보를 채운 본문을 만든다.
    /// 사용자가 내용을 쓰고 직접 보내며, 아무것도 자동으로 전송하지 않는다</summary>
    public static class SupportMail
    {
        public const string Address = "teamdoodle.dev@gmail.com";
        public const string Subject = "[Project Nova] 문의";

        /// <summary>문의 내용을 쓸 자리와 처리에 필요한 정보 줄들. 값이 없는 항목은 줄째 뺀다</summary>
        public static string Body(string nickname, string loginMethod, string guestId, string diagnosticId,
            string appVersion, string device, string os)
        {
            var text = new StringBuilder();
            text.AppendLine("문의 내용을 아래에 적어 주세요.");
            text.AppendLine();
            text.AppendLine();
            text.AppendLine("---------- 아래는 문의 확인에 필요한 정보예요. 지우지 말아 주세요 ----------");
            Line(text, "닉네임", nickname);
            Line(text, "로그인 방식", loginMethod);
            Line(text, "게스트 ID", guestId);
            Line(text, "Unity 진단 ID", diagnosticId);
            Line(text, "앱 버전", appVersion);
            Line(text, "기기", device);
            Line(text, "OS", os);
            return text.ToString();
        }

        /// <summary>메일 앱을 여는 주소. 제목과 본문의 줄바꿈·공백·한글을 퍼센트 인코딩한다</summary>
        public static string Uri(string body) =>
            $"mailto:{Address}?subject={System.Uri.EscapeDataString(Subject)}&body={System.Uri.EscapeDataString(body)}";

        /// <summary>Unity가 설치마다 주는 진단 ID. 읽을 수 없으면 null</summary>
        public static string ReadDiagnosticId()
        {
#if UNITY_6000_6_OR_NEWER
            try
            {
                return UnityEngine.Identifiers.Identifiers.installationId;
            }
            catch (Exception)
            {
                // ID나 예외 내용을 기록하지 않는다. 미지원 플랫폼도 값 없음으로 둔다.
                return null;
            }
#else
            return null;
#endif
        }

        private static void Line(StringBuilder text, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                text.AppendLine($"{label}: {value}");
            }
        }
    }
}
