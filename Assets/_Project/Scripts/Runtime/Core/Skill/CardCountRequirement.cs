namespace Game.Core
{
    public class CardCountRequirement
    {
        // 0이면 이 강화가 속한 무기. 다른 무기의 강화도 참조할 수 있다.
        public int weaponId;
        public string cardId;
        public int count = 1;
    }
}
