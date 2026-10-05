using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스킬 정의는 GameDataStore의 Weapons 테이블에서 읽는다. 프리팹과 아이콘 키는 기존 연결을 유지한다.</summary>
    public sealed class DefaultSkillDataProvider : ISkillDataProvider
    {
        private readonly GameDataStore _data;

        public DefaultSkillDataProvider(GameDataStore data)
        {
            _data = data;
        }

        public List<SkillData> LoadAll() => _data.LoadSkills();
    }
}
