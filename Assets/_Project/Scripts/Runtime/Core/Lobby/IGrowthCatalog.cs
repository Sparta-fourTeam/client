using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>닌자·인술 화면이 표시할 성장 정보. 지금은 DummyGrowthCatalog(하드코딩)가 구현한다.
    /// 실제 데이터(서버 스냅샷, 데이터 테이블)가 생기면 이 인터페이스의 구현만 바꾸고 View는 그대로 둔다.
    /// 엽전(골드) 보유량은 여기 없고 PlayerProfile.Gold를 쓴다</summary>
    public interface IGrowthCatalog
    {
        ShinobiInfo Shinobi { get; }

        /// <summary>닌자 화면 장비 칸 순서 (오른쪽 2열 6칸 → 왼쪽 1칸)</summary>
        IReadOnlyList<EquipInfo> Equips { get; }

        /// <summary>인술 화면 칸 순서</summary>
        IReadOnlyList<NinpoInfo> Ninpos { get; }

        int NinpoBooks { get; }
        int EquipBooks { get; }
        int Gems { get; }
        int Tickets { get; }
    }
}
