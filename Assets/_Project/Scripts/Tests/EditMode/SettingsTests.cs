using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>로비 메뉴·설정 팝업과 볼륨 설정(#238)</summary>
    public sealed class SettingsTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class MemoryStore : IVolumeStore
        {
            public readonly Dictionary<string, float> Values = new();
            public bool TryGet(string key, out float value) => Values.TryGetValue(key, out value);
            public void Set(string key, float value) => Values[key] = value;
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) { Object.DestroyImmediate(go); }
            _spawned.Clear();
        }

        private T Spawn<T>(string prefab) where T : HudView
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/Lobby/{prefab}.prefab"));
            _spawned.Add(go);
            var view = go.GetComponent<T>();
            view.Initialize();
            return view;
        }

        // ---------- 볼륨 설정 ----------

        [Test(Description = "저장된 값이 없으면 모든 채널이 100%이고, 저장된 값이 있으면 그 값으로 시작한다")]
        public void Settings_DefaultsAndLoadsSaved()
        {
            var store = new MemoryStore();
            store.Values[SoundSettings.Key(SoundChannel.Sfx)] = 0.3f;

            var settings = new SoundSettings(store);

            Assert.AreEqual(1f, settings.Get(SoundChannel.Music));
            Assert.AreEqual(0.3f, settings.Get(SoundChannel.Sfx), 0.0001f);
            Assert.AreEqual(1f, settings.Get(SoundChannel.Voice));
        }

        [Test(Description = "바꾸면 0~1로 자르고 저장하며 알린다. 같은 값이면 알리지 않는다")]
        public void Settings_SetClampsSavesAndNotifies()
        {
            var store = new MemoryStore();
            var settings = new SoundSettings(store);
            var changes = new List<(SoundChannel, float)>();
            settings.Changed += (c, v) => changes.Add((c, v));

            settings.Set(SoundChannel.Voice, 1.5f);
            settings.Set(SoundChannel.Music, 0.25f);
            settings.Set(SoundChannel.Music, 0.25f);

            Assert.AreEqual(new[] { (SoundChannel.Music, 0.25f) }, changes.ToArray(), "Voice는 이미 1이라 바뀌지 않았다");
            Assert.AreEqual(0.25f, store.Values[SoundSettings.Key(SoundChannel.Music)], 0.0001f);
            Assert.AreEqual(0.25f, new SoundSettings(store).Get(SoundChannel.Music), 0.0001f, "다시 켜도 남는다");
        }

        [Test(Description = "볼륨은 데시벨로 바뀐다: 1 → 0dB, 0.5 → 약 -6dB, 0 → -80dB(무음)")]
        public void ToDecibel()
        {
            Assert.AreEqual(0f, SoundVolumeApplier.ToDecibel(1f), 0.001f);
            Assert.AreEqual(-6.02f, SoundVolumeApplier.ToDecibel(0.5f), 0.01f);
            Assert.AreEqual(-80f, SoundVolumeApplier.ToDecibel(0f), 0.001f);
        }

        [Test(Description = "믹서에 음악·효과음·음성 그룹과 각 볼륨 파라미터가 있다")]
        public void Mixer_HasGroupsAndExposedVolumes()
        {
            var mixer = Resources.Load<AudioMixer>(SoundVolumeApplier.MixerPath);
            Assert.IsNotNull(mixer);

            foreach (SoundChannel channel in System.Enum.GetValues(typeof(SoundChannel)))
            {
                Assert.IsTrue(mixer.GetFloat(SoundVolumeApplier.ParameterOf(channel), out _), channel.ToString());
                Assert.AreEqual(1, mixer.FindMatchingGroups("Master/" + channel).Length, channel.ToString());
            }
        }

        // ---------- 설정 팝업 ----------

        [Test(Description = "설정 팝업을 열면 저장된 볼륨으로 슬라이더가 맞춰지고, 슬라이더를 움직이면 바로 저장·표시된다")]
        public void SettingsPopup_ShowsAndChangesVolumes()
        {
            var store = new MemoryStore();
            store.Values[SoundSettings.Key(SoundChannel.Music)] = 0.4f;
            var settings = new SoundSettings(store);
            var popup = Spawn<SettingsPopupView>("SettingsPopup");
            popup.Construct(settings);

            popup.Open();
            var sliders = Field<Slider[]>(popup, "_sliders");
            var texts = Field<TMP_Text[]>(popup, "_valueTexts");
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(0.4f, sliders[0].value, 0.0001f);
            Assert.AreEqual("40%", texts[0].text);
            Assert.AreEqual("100%", texts[2].text);

            sliders[1].value = 0.55f;

            Assert.AreEqual(0.55f, settings.Get(SoundChannel.Sfx), 0.0001f);
            Assert.AreEqual("55%", texts[1].text);
            Assert.AreEqual(0.55f, store.Values[SoundSettings.Key(SoundChannel.Sfx)], 0.0001f);
        }

        // ---------- 메뉴 팝업 ----------

        [Test(Description = "메뉴는 설정만 누를 수 있고, 바깥을 누르면 닫히며, 설정을 누르면 메뉴가 닫히고 설정 팝업이 열린다")]
        public void Menu_OnlySettingsEnabled_AndOpensSettings()
        {
            var menu = Spawn<LobbyMenuPopupView>("LobbyMenuPopup");
            var settings = Spawn<SettingsPopupView>("SettingsPopup");
            settings.Construct(new SoundSettings(new MemoryStore()));
            typeof(LobbyMenuPopupView).GetField("_settings", Flags).SetValue(menu, settings);

            menu.Toggle();
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(Field<Button>(menu, "_settingsButton").interactable);
            foreach (var button in Field<Button[]>(menu, "_disabledButtons")) { Assert.IsFalse(button.interactable, button.name); }

            Field<PointerClickRelay>(menu, "_background").OnPointerClick(new PointerEventData(null));
            Assert.IsFalse(menu.IsOpen, "바깥을 누르면 닫힌다");

            menu.Open();
            Field<Button>(menu, "_settingsButton").onClick.Invoke();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(settings.IsOpen);
        }

        [Test(Description = "상단 메뉴 버튼은 미구현 안내 대신 메뉴를 연다")]
        public void TopBarMenuButton_OpensMenu()
        {
            var top = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/LobbyTopBar.prefab");
            var menu = top.transform.Find("TopBar/Menu");

            Assert.IsNotNull(menu.GetComponent<LobbyMenuOpenButton>());
            Assert.IsNull(menu.GetComponent<ComingSoonButton>());
        }
    }
}
