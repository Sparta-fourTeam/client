using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>전투를 시작할 때 처음 가지고 있을 스킬. 캐릭터마다 달라지면 이 인터페이스를 캐릭터 데이터로 구현한다.</summary>
    public interface IStartingSkills
    {
        IReadOnlyList<int> GetSkillIds();
    }

    /// <summary>캐릭터별 초기 스킬이 생기기 전까지 쓰는 고정 목록.</summary>
    public sealed class DefaultStartingSkills : IStartingSkills
    {
        private static readonly int[] Ids = { 3 };
        public IReadOnlyList<int> GetSkillIds() => Ids;
    }
}
