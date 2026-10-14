using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
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

        private sealed class MemorySettingsStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Values = new();
            public bool TryGet(string key, out int value) => Values.TryGetValue(key, out value);
            public void Set(string key, int value) => Values[key] = value;
        }

        private sealed class GuestCredentials : IAuthCredentialStore
        {
            public bool HasCredential => true;
            public LoginMethod Method => LoginMethod.Guest;
            public string AccountId => "guest";
            public void Save(LoginMethod method, string accountId) { }
            public void Clear() { }
        }

        private sealed class MemoryAccountApi : IAccountApi
        {
            public int Deleted;
            public readonly UniTaskCompletionSource Completion = new();
            public UniTask DeleteAccount() { Deleted++; return Completion.Task; }
            public UniTask LinkAccount(LoginMethod method, string accountId) => UniTask.CompletedTask;
        }

        private sealed class MemoryNavigator : ISceneNavigator
        {
            public int LoadingCalls;
            public SceneId Current => default;
            public UniTask GoToLoading() { LoadingCalls++; return UniTask.CompletedTask; }
            public UniTask GoToLobby() => UniTask.CompletedTask;
            public UniTask GoToStage(int stageId) => UniTask.CompletedTask;
            public UniTask RestartStage() => UniTask.CompletedTask;
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

        [Test(Description = "기기 설정은 저장된 값이 없으면 기본값이고, 바꾸면 저장되어 다시 켜도 남으며 알린다")]
        public void GameSettings_DefaultsPersistAndNotify()
        {
            var store = new MemorySettingsStore();
            var settings = new GameSettings(store);
            Assert.IsTrue(settings.DamageIndication);
            Assert.IsTrue(settings.DebuffEffects);
            Assert.IsFalse(settings.AutoEnterHighestStage);
            Assert.AreEqual(FrameRateMode.High, settings.FrameRate);

            int changes = 0;
            settings.Changed += () => changes++;
            settings.SetDamageIndication(false);
            settings.SetDamageIndication(false);
            settings.SetFrameRate(FrameRateMode.Standard);
            settings.SetLanguage(GameLanguage.English);

            Assert.AreEqual(3, changes, "같은 값은 알리지 않는다");
            var reloaded = new GameSettings(store);
            Assert.IsFalse(reloaded.DamageIndication);
            Assert.AreEqual(GameSettings.StandardFrameRate, reloaded.TargetFrameRate);
            Assert.AreEqual(GameLanguage.English, reloaded.Language);
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
            popup.Construct(settings, new GameSettings(new MemorySettingsStore()));

            popup.Open();
            var sliders = Field<Slider[]>(popup, "_sliders");
            var texts = Field<TMP_Text[]>(popup, "_valueTexts");
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(0.4f, sliders[0].value, 0.0001f);
            Assert.AreEqual("40%", texts[0].text);
            Assert.AreEqual("100%", texts[1].text);

            sliders[1].value = 0.55f;

            Assert.AreEqual(0.55f, settings.Get(SoundChannel.Sfx), 0.0001f);
            Assert.AreEqual("55%", texts[1].text);
            Assert.AreEqual(0.55f, store.Values[SoundSettings.Key(SoundChannel.Sfx)], 0.0001f);
        }

        // ---------- 메뉴 팝업 ----------

        [Test(Description = "설치 ID 복사는 반복 재열기·언어 변경 후에도 정확하며, 빈 값은 클립보드를 바꾸지 않는다. 실제 ID는 결과에 출력하지 않는다")]
        public void SettingsPopup_DiagnosticIdCopiesAndUnavailableDoesNotOverwriteClipboard()
        {
            string previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                var game = new GameSettings(new MemorySettingsStore());
                var popup = Spawn<SettingsPopupView>("SettingsPopup");
                popup.Construct(new SoundSettings(new MemoryStore()), game);
                var copy = Field<Button>(popup, "_diagnosticCopyButton");
                var value = Field<TMP_Text>(popup, "_diagnosticIdText");
                var label = Field<TMP_Text>(popup, "_diagnosticCopyLabel");
                var info = Field<TMP_Text>(popup, "_diagnosticInfo");
                for (int i = 0; i < 3; i++)
                {
                    popup.Open();
                    string id = Field<string>(popup, "_diagnosticId");
                    Assert.IsTrue(value.text == id || string.IsNullOrWhiteSpace(id));
                    Assert.AreEqual(!string.IsNullOrWhiteSpace(id), copy.interactable);
                    if (copy.interactable)
                    {
                        copy.onClick.Invoke();
                        Assert.IsTrue(GUIUtility.systemCopyBuffer == id, "설치 ID를 출력하지 않고 일치 여부만 검사한다");
                        Assert.AreEqual("복사 완료", label.text);
                        copy.onClick.Invoke();
                        Assert.IsTrue(GUIUtility.systemCopyBuffer == id);
                    }
                    popup.Close();
                }

                foreach (string unavailable in new[] { null, "", " " })
                {
                    typeof(SettingsPopupView).GetField("_diagnosticId", Flags).SetValue(popup, unavailable);
                    game.SetLanguage(GameLanguage.English);
                    Assert.IsFalse(copy.interactable);
                    Assert.AreEqual("ID unavailable on this installation", value.text);
                    Assert.IsTrue(info.text.Contains("teamdoodle.dev@gmail.com"));
                    GUIUtility.systemCopyBuffer = "diagnostic-test-sentinel";
                    copy.onClick.Invoke();
                    Assert.IsTrue(GUIUtility.systemCopyBuffer == "diagnostic-test-sentinel");
                    game.SetLanguage(GameLanguage.Korean);
                    Assert.AreEqual("이 설치에서는 ID를 확인할 수 없습니다", value.text);
                }

                typeof(SettingsPopupView).GetField("_diagnosticId", Flags).SetValue(popup, "synthetic-installation-id");
                game.SetLanguage(GameLanguage.English);
                copy.onClick.Invoke();
                Assert.IsTrue(GUIUtility.systemCopyBuffer == "synthetic-installation-id");
                Assert.AreEqual("Copied", label.text);
                game.SetLanguage(GameLanguage.Korean);
                Assert.AreEqual("복사 완료", label.text);
                Assert.IsTrue(info.text.Contains("계정 ID·광고 ID와 다릅니다"));
                popup.Close();
                popup.Open();
                Assert.AreEqual("ID 복사", label.text, "재열기는 복사 피드백을 초기화한다");
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        [Test(Description = "두 단계 삭제 확인이 동기적으로 이어져도 닉네임 확인을 완료하고, 처리 중 연타를 막으며 완료 뒤 다시 열린다")]
        public void Menu_DeleteAccount_CompletesSecondPromptAndReopens()
        {
            var (menu, confirm, api, nav) = CreateDeleteMenu();
            var delete = Field<Button>(menu, "_deleteAccountButton");
            var accept = Field<Button>(confirm, "_confirmButton");
            var input = Field<TMP_InputField>(confirm, "_input");

            delete.onClick.Invoke();
            delete.onClick.Invoke();
            accept.onClick.Invoke();
            Assert.IsTrue(confirm.IsOpen, "첫 확인의 continuation이 두 번째 창을 연다");
            Assert.IsTrue(input.gameObject.activeSelf);
            Assert.IsFalse(accept.interactable);
            input.text = "wrong";
            Assert.IsFalse(accept.interactable);
            input.text = "Review_Ninja";
            Assert.IsTrue(accept.interactable);
            accept.onClick.Invoke();
            Assert.AreEqual(1, api.Deleted, "두 번째 완료소스가 살아 있어야 삭제 흐름이 이어진다");
            Assert.AreEqual(0, nav.LoadingCalls);
            accept.onClick.Invoke();
            delete.onClick.Invoke();
            Assert.IsFalse(Field<PopupTransition>(confirm, "_transition").IsShown, "계정 API 완료 전에는 연타로 새 흐름을 시작하지 않는다");
            Assert.AreEqual(1, api.Deleted);

            api.Completion.TrySetResult();
            Assert.AreEqual(1, nav.LoadingCalls);
            delete.onClick.Invoke();
            Assert.IsTrue(confirm.IsOpen, "완료 후 메뉴 busy가 풀린다");
            Field<Button>(confirm, "_cancelButton").onClick.Invoke();
        }

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void Menu_DeleteAccount_CancelOrCloseAtEitherStepAllowsRetry(bool secondStep, int dismissal)
        {
            var (menu, confirm, api, nav) = CreateDeleteMenu();
            var delete = Field<Button>(menu, "_deleteAccountButton");
            delete.onClick.Invoke();
            if (secondStep)
            {
                Field<Button>(confirm, "_confirmButton").onClick.Invoke();
                Field<TMP_InputField>(confirm, "_input").text = "Review_Ninja";
            }

            if (dismissal == 2)
            {
                var panel = Field<GameObject>(confirm, "_panel");
                var background = panel.GetComponent<PointerClickRelay>();
                var window = Field<Button>(confirm, "_confirmButton").transform.parent as RectTransform;
                background.OnPointerClick(new PointerEventData(null)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, window.TransformPoint(window.rect.center))
                });
                Assert.IsTrue(Field<PopupTransition>(confirm, "_transition").IsShown, "창 안쪽 클릭은 취소로 처리하지 않는다");
                background.OnPointerClick(
                    new PointerEventData(null) { position = new Vector2(-10000f, -10000f) });
            }
            else { Field<Button>(confirm, dismissal == 1 ? "_closeButton" : "_cancelButton").onClick.Invoke(); }
            Assert.IsFalse(Field<PopupTransition>(confirm, "_transition").IsShown);
            Assert.AreEqual(0, api.Deleted);
            Assert.AreEqual(0, nav.LoadingCalls);
            delete.onClick.Invoke();
            Assert.IsTrue(confirm.IsOpen);
            Assert.IsFalse(Field<TMP_InputField>(confirm, "_input").gameObject.activeSelf, "재시도는 첫 확인부터 시작한다");
            Field<Button>(confirm, "_cancelButton").onClick.Invoke();
        }

        private (LobbyMenuPopupView, ConfirmPopupView, MemoryAccountApi, MemoryNavigator) CreateDeleteMenu()
        {
            var menu = Spawn<LobbyMenuPopupView>("LobbyMenuPopup");
            var confirm = Spawn<ConfirmPopupView>("ConfirmPopup");
            var api = new MemoryAccountApi();
            var nav = new MemoryNavigator();
            var profile = new PlayerProfile(null);
            profile.Apply(new PlayerSnapshot { nickname = "Review_Ninja" });
            menu.Construct(new AccountService(api, new GuestCredentials(), nav, null), profile);
            typeof(LobbyMenuPopupView).GetField("_confirm", Flags).SetValue(menu, confirm);
            menu.Open();
            return (menu, confirm, api, nav);
        }

        [Test(Description = "언어 선택 즉시 열려 있는 설정 표시가 바뀌고, 취소·재열기·반복 선택도 저장값과 일치한다")]
        public void SettingsPopup_LanguageSelectionRefreshesOpenLabel()
        {
            var store = new MemorySettingsStore();
            var game = new GameSettings(store);
            var settings = Spawn<SettingsPopupView>("SettingsPopup");
            var language = Spawn<LanguagePopupView>("LanguagePopup");
            settings.Construct(new SoundSettings(new MemoryStore()), game);
            language.Construct(game);
            typeof(SettingsPopupView).GetField("_language", Flags).SetValue(settings, language);
            var label = Field<TMP_Text>(settings, "_languageLabel");
            var options = Field<Toggle[]>(language, "_options");
            var open = Field<Button>(settings, "_languageButton");
            settings.Open();
            Assert.AreEqual("한국어", label.text);
            open.onClick.Invoke();
            options[1].isOn = true;
            Assert.IsTrue(settings.IsOpen);
            Assert.IsFalse(Field<PopupTransition>(language, "_transition").IsShown);
            Assert.AreEqual("English", label.text);
            Assert.AreEqual(GameLanguage.English, new GameSettings(store).Language);

            open.onClick.Invoke();
            Assert.IsTrue(options[1].isOn);
            Field<Button>(language, "_closeButton").onClick.Invoke();
            Assert.AreEqual("English", label.text);
            Assert.AreEqual(GameLanguage.English, game.Language);
            open.onClick.Invoke();
            options[0].isOn = true;
            Assert.AreEqual("한국어", label.text);
            settings.Close();
            settings.Open();
            open.onClick.Invoke();
            options[1].isOn = true;
            Assert.AreEqual("English", label.text);

            settings.Dispose();
            game.SetLanguage(GameLanguage.Korean);
            Assert.AreEqual("English", label.text, "Dispose 뒤에는 변경 구독이 남지 않는다");
        }

        [Test(Description = "메뉴 바깥을 누르면 닫히고, 설정을 누르면 메뉴가 닫히고 설정 팝업이 열린다")]
        public void Menu_ClosesOnBackground_AndOpensSettings()
        {
            var menu = Spawn<LobbyMenuPopupView>("LobbyMenuPopup");
            var settings = Spawn<SettingsPopupView>("SettingsPopup");
            settings.Construct(new SoundSettings(new MemoryStore()), new GameSettings(new MemorySettingsStore()));
            typeof(LobbyMenuPopupView).GetField("_settings", Flags).SetValue(menu, settings);
            menu.Construct(new AccountService(null, new GuestCredentials(), null, null), new PlayerProfile(null));

            menu.Toggle();
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(Field<Button>(menu, "_settingsButton").interactable);

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
