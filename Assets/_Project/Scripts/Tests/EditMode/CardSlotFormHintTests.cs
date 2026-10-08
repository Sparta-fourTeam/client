using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>카드 선택창의 카드가 형태 변환 아이콘을 보여 주는지. 실제 CardSelect 프리팹으로 확인한다</summary>
    public sealed class CardSlotFormHintTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject _root;
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CardSelect.prefab");
            _root = Object.Instantiate(prefab);
            _texture = new Texture2D(2, 2);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        private CardSlotView Slot => _root.GetComponentsInChildren<CardSlotView>(true).First();

        private FormHintIconView[] Icons(CardSlotView slot) => (FormHintIconView[])typeof(CardSlotView).GetField("_formHints", Flags).GetValue(slot);

        private static UpgradeChoice NewSkill(params FormHint[] hints) => new()
        {
            IsNewWeapon = true,
            newSkillData = new SkillData { id = 5, name = "통나무", assetKey = "weapon_log" },
            FormHints = hints,
        };

        private static FormHint Hint(string cardId, FormHintKind kind) =>
            new(new SkillData { id = 5, assetKey = "weapon_log" }, new SkillUpgradeOption { id = cardId }, SkillForm.LargeLog, kind);

        [Test(Description = "프리팹의 카드마다 형태 변환 아이콘 칸이 연결돼 있고 처음에는 숨어 있다")]
        public void Prefab_EverySlotHasHiddenFormHintIcons()
        {
            foreach (var slot in _root.GetComponentsInChildren<CardSlotView>(true))
            {
                var icons = Icons(slot);
                Assert.IsNotNull(icons, slot.name);
                Assert.GreaterOrEqual(icons.Length, 2, $"{slot.name}: 통나무처럼 변환이 둘인 스킬을 보여 줄 칸이 있어야 한다");
                Assert.IsTrue(icons.All(i => i != null && !i.gameObject.activeSelf), $"{slot.name}: 칸이 비었거나 처음부터 보인다");
            }
        }

        [Test(Description = "조건인 변환은 아이콘만, 막히는 변환은 X 표시와 함께 보이고 남는 칸은 숨는다")]
        public void Bind_ShowsEnablesAndBlockedHints()
        {
            var slot = Slot;
            var asked = new List<string>();

            slot.Bind(0, NewSkill(Hint("log_large", FormHintKind.Enables), Hint("log_fire", FormHintKind.Blocks)), null, null, _ => { },
                hint => { asked.Add(hint.Card.id); return _sprite; });

            var icons = Icons(slot);
            CollectionAssert.AreEqual(new[] { "log_large", "log_fire" }, asked);
            Assert.IsTrue(icons[0].gameObject.activeSelf);
            Assert.IsFalse(icons[0].IsBlocked);
            Assert.AreSame(_sprite, icons[0].Icon);
            Assert.IsTrue(icons[1].gameObject.activeSelf);
            Assert.IsTrue(icons[1].IsBlocked);
            Assert.IsTrue(icons.Skip(2).All(i => !i.gameObject.activeSelf));
        }

        [Test(Description = "관련 변환이 없는 카드로 다시 Bind하면 아이콘이 모두 숨는다")]
        public void Rebind_WithoutHints_HidesIcons()
        {
            var slot = Slot;
            slot.Bind(0, NewSkill(Hint("log_large", FormHintKind.Blocks)), null, null, _ => { }, _ => _sprite);

            slot.Bind(0, NewSkill(), null, null, _ => { }, _ => _sprite);

            Assert.IsTrue(Icons(slot).All(i => !i.gameObject.activeSelf));
        }
    }
}
