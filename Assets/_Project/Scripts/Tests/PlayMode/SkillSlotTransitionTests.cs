#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class SkillSlotTransitionTests
    {
        private sealed class Status : ISkillStatus
        {
            public int Id { get; set; } = 2;
            public int Level { get; set; } = 1;
            public SkillForm Form { get; set; }
            public float CooldownRatio => 0.35f;
        }

        private SkillSlotView _slot;
        private Image _icon;
        private TMP_Text _level;
        private Sprite _basic;
        private Sprite _form;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Common/SkillSlot.prefab");
            _slot = Object.Instantiate(prefab).GetComponent<SkillSlotView>();
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            _basic = assets.GetFormIcon(SkillForm.Default, 2);
            _form = assets.GetFormIcon(SkillForm.FlameArrow, 2);
#endif
            Assert.That(_slot, Is.Not.Null);
            Assert.That(_form, Is.Not.Null.And.Not.SameAs(_basic));
            _icon = Field<Image>("_icon");
            _level = Field<TMP_Text>("_levelText");
        }

        private T Field<T>(string name) => (T)typeof(SkillSlotView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_slot);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
            if (_slot != null) { Object.Destroy(_slot.gameObject); }
            yield return null;
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Slot transition did not finish in real frames");
                yield return null;
            }
        }

        private bool Settled() => Vector3.Distance(_icon.rectTransform.localScale, Vector3.one) < 0.001f
            && Quaternion.Angle(_icon.rectTransform.localRotation, Quaternion.identity) < 0.01f;

        private bool EffectsHidden() => (Field<Image>("_formFlash") == null || !Field<Image>("_formFlash").enabled)
            && (Field<Image>("_formRing") == null || !Field<Image>("_formRing").enabled);

        [UnityTest]
        public IEnumerator RepeatedRefresh_PreservesPendingFormTransitionAtZeroTimeScale()
        {
            Time.timeScale = 0f;
            var status = new Status();
            _slot.Bind(status, _basic);
            status.Form = SkillForm.FlameArrow;
            status.Level++;
            _slot.Bind(status, _form);
            yield return Until(() => !Settled());
            var scale = _icon.rectTransform.localScale;
            _slot.Bind(status, _form); // another skill's SkillChanged refreshes every slot
            Assert.That(Vector3.Distance(_icon.rectTransform.localScale, scale), Is.LessThan(0.001f),
                "An unchanged HUD refresh must not finish the pending transformation");
            yield return Until(() => _icon.sprite == _form && Settled() && EffectsHidden());
            Assert.That(_level.text, Is.EqualTo("2"));
            yield return Until(() => Vector3.Distance(_level.rectTransform.localScale, Vector3.one) < 0.001f);
        }

        [UnityTest]
        public IEnumerator ClearAndDisable_CancelEffectsAndAllowImmediateRebind()
        {
            var status = new Status();
            _slot.Bind(status, _basic);
            status.Form = SkillForm.FlameArrow;
            _slot.Bind(status, _form);
            yield return Until(() => !Settled());
            _slot.Clear();
            Assert.That(_icon.enabled, Is.False);
            Assert.That(Settled() && EffectsHidden(), Is.True);
            _slot.Bind(new Status(), _basic);
            Assert.That(_icon.sprite, Is.SameAs(_basic));
            status = new Status { Form = SkillForm.FlameArrow };
            _slot.Bind(status, _form);
            yield return Until(() => !Settled());
            _slot.gameObject.SetActive(false);
            Assert.That(Settled() && EffectsHidden(), Is.True);
            status.Form = SkillForm.ThunderArrow;
            _slot.Bind(status, _basic);
            Assert.That(_icon.sprite, Is.SameAs(_basic), "Inactive binding must apply the latest state immediately");
            _slot.gameObject.SetActive(true);
            yield return null;
            Assert.That(_icon.sprite, Is.SameAs(_basic));
            Assert.That(Settled() && EffectsHidden(), Is.True);
        }

        [UnityTest]
        public IEnumerator ReplacementDuringShrink_DoesNotRestoreStaleIconOrEffects()
        {
            var status = new Status();
            _slot.Bind(status, _basic);
            status.Form = SkillForm.FlameArrow;
            _slot.Bind(status, _form);
            yield return Until(() => !Settled());
            _slot.Bind(new Status { Id = 1 }, _basic);
            Assert.That(_icon.sprite, Is.SameAs(_basic));
            Assert.That(Settled() && EffectsHidden(), Is.True);
            yield return null;
            Assert.That(_icon.sprite, Is.SameAs(_basic));
            status = new Status { Id = 1 };
            _slot.Bind(status, _basic);
            status.Form = SkillForm.FlameArrow;
            _slot.Bind(status, _form);
            yield return Until(() => !Settled());
            status.Form = SkillForm.ThunderArrow;
            _slot.Bind(status, _basic); // a missing form icon falls back while the old form is shrinking
            yield return Until(() => _icon.sprite == _basic && Settled() && EffectsHidden());
            _slot.Clear();
            _slot.Bind(new Status(), null);
            Assert.That(_icon.enabled, Is.False);
        }
    }
}
