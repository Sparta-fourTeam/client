namespace Game.Core
{
    public class CardCountRequirement
    {
        // 소속 스킬은 카탈로그에서 유일한 cardId로 찾는다. 서버에는 cardId·count만 보낸다.
        [Newtonsoft.Json.JsonIgnore] public int OwnerSkillId { get; internal set; } = -1;
        public string cardId;
        public int count = 1;
    }
}
