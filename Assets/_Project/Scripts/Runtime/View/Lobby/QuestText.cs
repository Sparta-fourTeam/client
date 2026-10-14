using Game.Core;

namespace Game.View
{
    /// <summary>퀘스트 이름. 서버 표에는 종류와 목표만 있어 표시 문구는 클라이언트가 만든다 (로컬라이징 도입 시 문자열 키로 교체)</summary>
    public static class QuestText
    {
        public static string Title(QuestDefinition quest) => quest.GetKind() switch
        {
            QuestKind.Login => "로그인하기",
            QuestKind.SpendEnergy => $"잉크펜 {quest.Target:N0}개 소모하기",
            QuestKind.KillMonsters => $"요마 {quest.Target:N0}마리 처치하기",
            _ => quest.Id
        };
    }
}
