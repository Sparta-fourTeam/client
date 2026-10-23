using Game.Core;

namespace Game.View
{
    /// <summary>퀘스트 이름. 서버 표에는 종류와 목표만 있어 표시 문구는 클라이언트가 만든다 (로컬라이징 도입 시 문자열 키로 교체)</summary>
    public static class QuestText
    {
        public static string Title(QuestDefinition quest) => quest.GetKind() switch
        {
            QuestKind.Login => L10n.Get("questText.login"),
            QuestKind.SpendEnergy => L10n.Get("questText.spendEnergy", quest.Target.ToString("N0")),
            QuestKind.KillMonsters => L10n.Get("questText.killMonsters", quest.Target.ToString("N0")),
            _ => quest.Id
        };
    }
}
