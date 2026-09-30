namespace Game.Core
{
    /// <summary>HUD가 스킬 한 칸을 그릴 때 읽는 읽기 전용 상태. 무기(WeaponBase)가 구현한다.
    /// 뷰가 무기 자체(발사, 강화)를 건드리지 못하게 이 인터페이스로만 노출한다</summary>
    public interface ISkillStatus
    {
        int Id { get; }

        int Level { get; }

        /// <summary>아이콘을 찾는 키. 컨벤션상 아이콘은 테이블의 IconKey로 찾는다</summary>
        string IconKey { get; }

        /// <summary>쿨타임 진행도 0~1. 0이면 발사 가능, 1이면 방금 발사해서 쿨타임이 가득 남은 상태.
        /// 매 프레임 바뀌는 값이라 메시지로 보내지 않고 뷰가 Update에서 직접 읽는다 (docs/architecture.md)</summary>
        float CooldownRatio { get; }
    }
}
