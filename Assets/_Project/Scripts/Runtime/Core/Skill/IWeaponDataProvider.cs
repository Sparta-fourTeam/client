using System.Collections.Generic;

namespace Game.Core
{
    public interface IWeaponDataProvider
    {
        List<WeaponData> LoadAll();
    }
}
