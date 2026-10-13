namespace Game.Core
{
    public class CardExclusion
    {
        // 카탈로그를 읽을 때 cardId로 찾는 소속 스킬. 서버 필드가 아니다.
        [Newtonsoft.Json.JsonIgnore] public int OwnerSkillId { get; internal set; } = -1;
        public string cardId;
        // 0이면 항상 적용. 양수이면 이 배타 조건을 가진 스킬의 영구 성장 레벨이 이 값 미만일 때만 적용.
        public int belowPermanentLevel;
    }
}
