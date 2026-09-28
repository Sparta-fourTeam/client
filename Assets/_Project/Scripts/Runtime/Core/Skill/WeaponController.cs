using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public class WeaponPrefabEntry
    {
        public int id;
        public GameObject prefab;
    }


    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private List<WeaponPrefabEntry> prefabEntries;
        private Dictionary<int, WeaponData> testDataTable;
        private List<WeaponBase> weapons = new List<WeaponBase>();

        private void Awake()
        {
            TestTable();
        }

        private void TestTable()
        {
            testDataTable = new Dictionary<int, WeaponData>
            {
                { 1, new WeaponData { id = 1, name = "Projectile1", baseStats = new WeaponBaseStats{
                    cooldown = 1f, cooldownMultiple = 1f, baseDamage = 10f, speed = 8f, hitCount = 1 }, maxLevel = 7, upgrades =
                new List<WeaponUpgradeOption> {
                new WeaponUpgradeOption { name = "공속", type = UpgradeType.AttackSpeed, value = 5f },
                new WeaponUpgradeOption { name = "데미지", type = UpgradeType.Damage, value = 3f },
                new WeaponUpgradeOption { name = "추가", type = UpgradeType.ProjectileCount, value = 1f }, } } },

                { 2, new WeaponData { id = 2, name = "Projectile2", baseStats = new WeaponBaseStats{
                    cooldown = 1f, cooldownMultiple = 1f, baseDamage = 10f, speed = 8f, hitCount = 1 }, maxLevel = 7, upgrades =
                new List<WeaponUpgradeOption> {
                new WeaponUpgradeOption { name = "공속", type = UpgradeType.AttackSpeed, value = 5f },
                new WeaponUpgradeOption { name = "데미지", type = UpgradeType.Damage, value = 3f },
                new WeaponUpgradeOption { name = "추가", type = UpgradeType.ProjectileCount, value = 1f }, } } },

                { 3, new WeaponData { id = 3, name = "Projectile3", baseStats = new WeaponBaseStats{
                    cooldown = 1f, cooldownMultiple = 1f, baseDamage = 10f, speed = 8f, hitCount = 1 }, maxLevel = 7, upgrades =
                new List<WeaponUpgradeOption> {
                new WeaponUpgradeOption { name = "공속", type = UpgradeType.AttackSpeed, value = 5f },
                new WeaponUpgradeOption { name = "데미지", type = UpgradeType.Damage, value = 3f },
                new WeaponUpgradeOption { name = "추가", type = UpgradeType.ProjectileCount, value = 1f }, } } },

                { 4, new WeaponData { id = 4, name = "Projectile4", baseStats = new WeaponBaseStats{
                    cooldown = 1f, cooldownMultiple = 1f, baseDamage = 10f, speed = 8f, hitCount = 1 }, maxLevel = 7, upgrades =
                new List<WeaponUpgradeOption> {
                new WeaponUpgradeOption { name = "공속", type = UpgradeType.AttackSpeed, value = 5f },
                new WeaponUpgradeOption { name = "데미지", type = UpgradeType.Damage, value = 3f },
                new WeaponUpgradeOption { name = "추가", type = UpgradeType.ProjectileCount, value = 1f }, } } },

                { 5, new WeaponData { id = 5, name = "Projectile5", baseStats = new WeaponBaseStats{
                    cooldown = 1f, cooldownMultiple = 1f, baseDamage = 10f, speed = 8f, hitCount = 1 }, maxLevel = 7, upgrades =
                new List<WeaponUpgradeOption> {
                new WeaponUpgradeOption { name = "공속", type = UpgradeType.AttackSpeed, value = 5f },
                new WeaponUpgradeOption { name = "데미지", type = UpgradeType.Damage, value = 3f },
                new WeaponUpgradeOption { name = "추가", type = UpgradeType.ProjectileCount, value = 1f }, } } },
            };
        }



        public void AddWeapon(int weaponId)
        {
            if (!testDataTable.TryGetValue(weaponId, out var data))
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 WeaponData가 없습니다.");
                return;
            }

            var entry = prefabEntries.Find(e => e.id == weaponId);
            if (entry == null)
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 프리팹 엔트리가 없습니다.");
                return;
            }

            weapons.Add(new ProjectileCaster(data, entry.prefab, transform));
        }

        public void WeaponLevelUp(int weaponId)
        {

        }



        private void Update()
        {
            foreach (var weapon in weapons)
            {
                weapon.Tick();
            }
        }
    }
}
