namespace Game.Core
{
    public class CardExclusion
    {
        public int weaponId;
        public string cardId;
        // 0이면 항상 적용. 양수이면 소속 스킬의 영구 성장 레벨이 이 값 미만일 때만 적용.
        public int belowPermanentLevel;
    }
}
