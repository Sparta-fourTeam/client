namespace Game.Core
{
    /// <summary>코드에서 사용하는 마법북 ID. 아이템 정의와 저장 데이터도 같은 문자열을 사용한다.</summary>
    public static class ItemIds
    {
        public const string RandomSkillMaterial = "material.skill.random";
        public const string GemChest = "chest.gem";
        public const string ArrowBook = "book.arrow";
        public const string FireballBook = "book.fireball";
        public const string LightningBook = "book.lightning";
        public const string FrostCrystalBook = "book.frost_crystal";
        public const string LogBook = "book.log";
        public const string ColdZoneBook = "book.cold_zone";
        public const string LightningCloudBook = "book.lightning_cloud";
        public const string EnergyBeamBook = "book.energy_beam";
        public const string ChainLightningBook = "book.chain_lightning";

        public const string HatBook = "book.hat";
        public const string TopBook = "book.top";
        public const string ShoesBook = "book.shoes";
        public const string WeaponBook = "book.weapon";
        public const string RingBook = "book.ring";
        public const string TieBook = "book.tie";
        public const string EmployeeIdBook = "book.employee_id";

        public static readonly System.Collections.Generic.IReadOnlyList<string> SkillMaterials = System.Array.AsReadOnly(new[]
        { ArrowBook, FireballBook, LightningBook, FrostCrystalBook, LogBook, ColdZoneBook, LightningCloudBook, EnergyBeamBook, ChainLightningBook });
        public static readonly System.Collections.Generic.IReadOnlyList<string> EquipmentMaterials = System.Array.AsReadOnly(new[]
        { HatBook, TopBook, ShoesBook, WeaponBook, RingBook, TieBook, EmployeeIdBook });
    }
}
