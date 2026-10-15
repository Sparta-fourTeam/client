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
            public int Cleared;
            public bool HasCredential => true;
            public LoginMethod Method => LoginMethod.Guest;
            public string AccountId => "guest";
            public void Save(LoginMethod method, string accountId) { }
            public void Clear() { Cleared++; }
        }

        private sealed class MemoryAccountApi : IAccountApi
        {
            public int Deleted;
            public System.Exception LinkError;
            public readonly UniTaskCompletionSource Completion = new();
            public UniTask DeleteAccount() { Deleted++; return Completion.Task; }
            public UniTask LinkAccount(LoginMethod method, string accountId) => LinkError != null
                ? UniTask.FromException(LinkError) : UniTask.CompletedTask;
        }

        private sealed class AvailableSocial : ISocialSignIn
        {
            public LoginMethod Method => LoginMethod.Google;
            public bool IsAvailable => true;
            public UniTask<string> SignInAsync() => UniTask.FromResult("google-id-token");
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

        [TestCase(false)]
        [TestCase(true)]
        public void LoginView_KeepsAppleVisibleAndDisablesItWhenUnavailable(bool available)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Loading/LoginView.prefab"));
            _spawned.Add(go);
            var view = go.GetComponent<LoginView>();
            view.Initialize();
            var methods = available ? new[] { LoginMethod.Guest, LoginMethod.Apple } : new[] { LoginMethod.Guest };
            var pending = view.WaitForSignInAsync(methods);
            Assert.IsTrue(Field<Button>(view, "_appleButton").gameObject.activeSelf);
            Assert.AreEqual(available, Field<Button>(view, "_appleButton").interactable);
            string label = Field<Button>(view, "_appleButton").GetComponentInChildren<TMP_Text>(true).text;
            Assert.AreEqual(!available, label.Contains("준비 중"));
            Assert.IsTrue(Field<Button>(view, "_guestButton").gameObject.activeSelf);
            Assert.IsTrue(Field<Button>(view, "_guestButton").interactable);
            view.Dispose();
            Assert.Throws<System.OperationCanceledException>(() => pending.GetAwaiter().GetResult());
        }

        [Test(Description = "문의 메일은 한글·공백·줄바꿈이 깨지지 않게 인코딩되어 본문이 그대로 복원되고, 값이 없는 정보 줄은 빠진다")]
        public void SupportMail_EncodesBodyAndOmitsMissingInfo()
        {
            string body = SupportMail.Body("닉 네임", "게스트", null, " ", "1.2.3", "Pixel 8", "Android 15");

            StringAssert.Contains("닉네임: 닉 네임", body);
            StringAssert.Contains("앱 버전: 1.2.3", body);
            StringAssert.DoesNotContain("게스트 ID", body);
            StringAssert.DoesNotContain("Unity 진단 ID", body);

            string uri = SupportMail.Uri(body);
            StringAssert.StartsWith($"mailto:{SupportMail.Address}?subject=", uri);
            Assert.IsFalse(uri.Contains(" ") || uri.Contains("\n"), "주소에 공백·줄바꿈이 그대로 남으면 메일 앱이 본문을 자른다");
            Assert.AreEqual(body, System.Uri.UnescapeDataString(uri.Substring(uri.IndexOf("&body=") + "&body=".Length)));
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

        [Test]
        public void Menu_LinkSessionExpired_OffersLoginAndPreservesGuest()
        {
            var (menu, confirm, api, nav) = CreateDeleteMenu();
            var credentials = new GuestCredentials();
            menu.Construct(new AccountService(api, credentials, nav, new[] { new AvailableSocial() }), new PlayerProfile(null));
            api.LinkError = new ApiException(ApiErrorKind.Rejected, "SESSION_EXPIRED");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("SESSION_EXPIRED"));

            Field<Button>(menu, "_linkAccountButton").onClick.Invoke();
            Assert.IsTrue(confirm.IsOpen);
            StringAssert.Contains("로그인", Field<TMP_Text>(confirm, "_message").text);
            Assert.AreEqual(0, nav.LoadingCalls);
            Field<Button>(confirm, "_confirmButton").onClick.Invoke();
            Assert.AreEqual(1, nav.LoadingCalls);
            Assert.AreEqual("guest", credentials.AccountId);
            Assert.AreEqual(0, credentials.Cleared);
            Assert.AreEqual(0, api.Deleted);
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
            Assert.IsFalse(Field<Button>(menu, "_termsButton").interactable);
            Assert.IsTrue(Field<Button>(menu, "_privacyButton").interactable);

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
