using System.Collections.Generic;

namespace Game.Core
{
    public interface ISkillDataProvider
    {
        List<SkillData> LoadAll();
    }
}
