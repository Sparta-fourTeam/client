using System;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>변형 효과는 데이터에 숫자가 아니라 이름("TriangleIce")으로 적는다</summary>
    public sealed class EffectFormNameTests
    {
        [TestCase("Enbakutsu", SkillForm.Enbakutsu)]
        [TestCase("TriangleIce", SkillForm.TriangleIce)]
        [TestCase("triangleice", SkillForm.TriangleIce)]
        [TestCase("FireLog", SkillForm.FireLog)]
        public void FormName_ResolvesToValue(string name, SkillForm expected)
        {
            var effect = JsonConvert.DeserializeObject<EffectDef>($"{{\"kind\":\"form\",\"form\":\"{name}\"}}");

            Assert.AreEqual((int)expected, effect.value);
            Assert.IsTrue(SkillStatEffects.TryCompile(effect, out _));
        }

        [Test(Description = "예전처럼 숫자 value를 적어도 읽힌다")]
        public void NumericValue_StillAccepted()
        {
            var effect = JsonConvert.DeserializeObject<EffectDef>("{\"kind\":\"form\",\"value\":3}");

            Assert.IsTrue(SkillStatEffects.TryCompile(effect, out _));
        }

        [TestCase("Nope")]
        [TestCase("Default")]
        public void UnknownOrDefaultFormName_IsRejected(string name)
        {
            var effect = JsonConvert.DeserializeObject<EffectDef>($"{{\"kind\":\"form\",\"form\":\"{name}\"}}");

            Assert.IsFalse(SkillStatEffects.TryCompile(effect, out _));
        }

        [Test(Description = "카탈로그의 변형 카드는 모두 이름으로 적혀 있고, 없는 이름이면 검증이 막는다")]
        public void Catalog_UsesNames_AndValidatorRejectsUnknownName()
        {
            var store = new GameDataStore();
            var forms = store.LoadSkills().SelectMany(s => s.upgrades).SelectMany(u => u.effects).Where(e => e.kind == "form").ToList();

            Assert.AreEqual(5, forms.Count);
            Assert.IsTrue(forms.All(e => !string.IsNullOrEmpty(e.form)));

            var broken = store.RawJson("Skills").Replace("\"TriangleIce\"", "\"Nope\"");
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(broken));
        }
    }
}
