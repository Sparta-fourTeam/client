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
using UnityEngine.Localization.Settings;
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
            public UniTask<string> ChangeNickname(string nickname) => UniTask.FromResult(nickname);
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

        [Test(Description = "저장된 언어가 없을 때의 기본 언어는 시스템 언어에서 정하고, 저장된 언어가 있으면 저장값이 우선한다. 지원하지 않는 시스템 언어로 첫 화면이 비는 것을 막는다")]
        public void GameSettings_DefaultLanguageFollowsSystemUnlessSaved()
        {
            Assert.AreEqual(GameLanguage.Korean, GameLanguageLocale.FromSystemLanguage(SystemLanguage.Korean));
            Assert.AreEqual(GameLanguage.Japanese, GameLanguageLocale.FromSystemLanguage(SystemLanguage.Japanese));
            Assert.AreEqual(GameLanguage.ChineseSimplified, GameLanguageLocale.FromSystemLanguage(SystemLanguage.ChineseSimplified));
            Assert.AreEqual(GameLanguage.ChineseSimplified, GameLanguageLocale.FromSystemLanguage(SystemLanguage.Chinese));
            Assert.AreEqual(GameLanguage.English, GameLanguageLocale.FromSystemLanguage(SystemLanguage.English));
            Assert.AreEqual(GameLanguage.English, GameLanguageLocale.FromSystemLanguage(SystemLanguage.ChineseTraditional));
            Assert.AreEqual(GameLanguage.English, GameLanguageLocale.FromSystemLanguage(SystemLanguage.French));

            var store = new MemorySettingsStore();
            Assert.AreEqual(GameLanguage.Korean, new GameSettings(store).Language, "인자가 없으면 한국어");
            Assert.AreEqual(GameLanguage.Japanese, new GameSettings(store, GameLanguage.Japanese).Language, "저장값이 없으면 주입한 기본 언어");
            Assert.IsEmpty(store.Values, "고르기 전에는 저장하지 않는다");

            store.Values[GameSettings.LanguageKey] = (int)GameLanguage.English;
            Assert.AreEqual(GameLanguage.English, new GameSettings(store, GameLanguage.Japanese).Language, "저장값이 기본 언어보다 우선");
        }

        [Test(Description = "로케일이 바뀌면 라벨을 새 문구로 다시 계산하지만, 그 사이 다른 코드가 직접 쓴 문구는 덮어쓰지 않고 바인딩을 푼다")]
        public void LocalizedLabel_DoesNotOverwriteTextWrittenByOtherCode()
        {
            if (!LocalizationSettings.HasSettings || LocalizationSettings.AvailableLocales.Locales.Count < 2)
            {
                Assert.Inconclusive("Localization 로케일이 2개 이상 필요하다");
            }
            var go = new GameObject("Label", typeof(RectTransform));
            var original = LocalizationSettings.SelectedLocale;
            try
            {
                var label = go.AddComponent<TextMeshProUGUI>();
                var locales = LocalizationSettings.AvailableLocales.Locales;
                int calls = 0;
                label.SetL10n(() => "v" + ++calls);
                Assert.AreEqual("v1", label.text);

                LocalizationSettings.SelectedLocale = locales[0] == original ? locales[1] : locales[0];
                if (calls == 1) { Assert.Inconclusive("에디터에서 SelectedLocaleChanged가 동기로 오지 않았다"); }
                Assert.AreEqual("v2", label.text, "직접 쓰지 않았으면 새로 계산한다");

                label.text = "직접";
                LocalizationSettings.SelectedLocale = original;
                Assert.AreEqual("직접", label.text, "다른 코드가 쓴 문구는 덮어쓰지 않는다");
                LocalizationSettings.SelectedLocale = locales[0] == original ? locales[1] : locales[0];
                Assert.AreEqual("직접", label.text, "바인딩이 풀려 이후에도 건드리지 않는다");
            }
            finally
            {
                LocalizationSettings.SelectedLocale = original;
                Object.DestroyImmediate(go);
            }
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
            string body = SupportMail.Body("닉 네임", "게스트", "guest-login-secret", " ", "1.2.3", "Pixel 8", "Android 15");

            StringAssert.Contains("닉네임: 닉 네임", body);
            StringAssert.Contains("앱 버전: 1.2.3", body);
            StringAssert.DoesNotContain("게스트 ID", body);
            StringAssert.DoesNotContain("guest-login-secret", body);
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
            Assert.IsFalse(delete.interactable);
            Assert.IsFalse(Field<Button>(menu, "_logoutButton").interactable);
            StringAssert.Contains("삭제 중", delete.GetComponentInChildren<TMP_Text>(true).text);
            menu.Close();
            Assert.IsTrue(menu.IsOpen, "삭제 처리 중에는 로비로 돌아가 진행을 바꾸지 않는다");
            Assert.IsFalse(Field<Button>(menu, "_settingsButton").interactable);
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

        [TestCase("HTTP_500", false, true)]
        [TestCase("HTTP_401", true, true)]
        [TestCase("SESSION_EXPIRED", true, true)]
        [TestCase("SESSION_EXPIRED", true, false)]
        [TestCase("NETWORK_ERROR", false, true)]
        public void Menu_DeleteFailure_ShowsNoticeAndRecoversOrAllowsRetry(string code, bool reauthenticate, bool confirmLogin)
        {
            var (menu, confirm, api, nav) = CreateDeleteMenu();
            var delete = Field<Button>(menu, "_deleteAccountButton");
            var accept = Field<Button>(confirm, "_confirmButton");
            delete.onClick.Invoke();
            accept.onClick.Invoke();
            Field<TMP_InputField>(confirm, "_input").text = "Review_Ninja";
            accept.onClick.Invoke();
            api.Completion.TrySetException(new ApiException(ApiErrorKind.Rejected, code));
            Assert.AreEqual(0, nav.LoadingCalls);
            Assert.IsTrue(confirm.IsOpen);
            Assert.AreEqual("계정 삭제 확인 필요", Field<TMP_Text>(confirm, "_title").text);
            Assert.IsFalse(delete.interactable);
            StringAssert.DoesNotContain("삭제 중", delete.GetComponentInChildren<TMP_Text>(true).text);
            Field<Button>(confirm, confirmLogin ? "_confirmButton" : "_closeButton").onClick.Invoke();
            Assert.AreEqual(reauthenticate && confirmLogin ? 1 : 0, nav.LoadingCalls);
            Assert.AreEqual(1, api.Deleted);
            Assert.IsTrue(delete.interactable);
            if (reauthenticate) { return; }
            delete.onClick.Invoke();
            Assert.IsTrue(confirm.IsOpen);
            Assert.AreEqual(1, api.Deleted);
            Field<Button>(confirm, "_cancelButton").onClick.Invoke();
        }

        [TestCase("HTTP_500", false, true)]
        [TestCase("SESSION_EXPIRED", true, true)]
        [TestCase("SESSION_EXPIRED", true, false)]
        public void Menu_GuestLogoutFailure_ShowsNoticeAndCanReauthenticate(string code, bool reauthenticate, bool confirmLogin)
        {
            var (menu, confirm, api, nav) = CreateDeleteMenu();
            var accept = Field<Button>(confirm, "_confirmButton");
            Field<Button>(menu, "_logoutButton").onClick.Invoke();
            accept.onClick.Invoke();
            Assert.AreEqual(0, api.Deleted, "게스트 로그아웃 확인 뒤에는 삭제 확인을 먼저 거친다");
            accept.onClick.Invoke();
            Assert.AreEqual(0, api.Deleted, "닉네임 확인을 마치기 전에는 삭제하지 않는다");
            StringAssert.Contains("Review_Ninja", Field<TMP_Text>(confirm, "_message").text);
            Field<TMP_InputField>(confirm, "_input").text = "Review_Ninja";
            accept.onClick.Invoke();
            Assert.AreEqual(1, api.Deleted);
            api.Completion.TrySetException(new ApiException(ApiErrorKind.Rejected, code));
            Assert.IsTrue(confirm.IsOpen);
            Assert.AreEqual("계정 삭제 확인 필요", Field<TMP_Text>(confirm, "_title").text);
            Assert.AreEqual(0, nav.LoadingCalls);
            Field<Button>(confirm, confirmLogin ? "_confirmButton" : "_closeButton").onClick.Invoke();
            Assert.AreEqual(reauthenticate && confirmLogin ? 1 : 0, nav.LoadingCalls);
            Assert.AreEqual(1, api.Deleted, "재인증할 때 서버 삭제를 다시 요청하지 않는다");
            Assert.IsTrue(Field<Button>(menu, "_logoutButton").interactable);
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

        [TestCase(true)]
        [TestCase(false)]
        public void Menu_LinkSessionExpired_OffersLoginAndPreservesGuest(bool confirmLogin)
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
            Field<Button>(confirm, confirmLogin ? "_confirmButton" : "_closeButton").onClick.Invoke();
            Assert.AreEqual(confirmLogin ? 1 : 0, nav.LoadingCalls);
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

        [Test(Description = "설정의 언어 드롭다운은 저장된 언어를 보여 주고, 선택하면 저장되며 설정 팝업은 열린 채 유지된다. Dispose 뒤에는 외부 변경을 따라가지 않는다")]
        public void SettingsPopup_LanguageDropdownSavesSelection()
        {
            var store = new MemorySettingsStore();
            var game = new GameSettings(store);
            var settings = Spawn<SettingsPopupView>("SettingsPopup");
            settings.Construct(new SoundSettings(new MemoryStore()), game);
            var dropdown = Field<TMP_Dropdown>(settings, "_languageDropdown");
            settings.Open();
            Assert.AreEqual((int)GameLanguage.Korean, dropdown.value);

            foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese, GameLanguage.ChineseSimplified })
            {
                dropdown.value = (int)language;
                Assert.IsTrue(settings.IsOpen);
                Assert.AreEqual(language, game.Language);
                Assert.AreEqual(language, new GameSettings(store).Language);
            }

            settings.Dispose();
            game.SetLanguage(GameLanguage.Korean);
            Assert.AreEqual((int)GameLanguage.ChineseSimplified, dropdown.value, "Dispose 뒤에는 변경 구독이 남지 않는다");
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
            Assert.IsTrue(Field<Button>(menu, "_termsButton").interactable);
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

    /// <summary>EditMode 테스트 전체가 개발 기기에 저장된 언어나 시스템 언어에 흔들리지 않게 로케일을 한국어로 고정한다.
    /// 시작 로케일이 저장값·시스템 언어를 따르므로, 한국어 문구를 단언하는 테스트는 이 고정에 기댄다</summary>
    [SetUpFixture]
    public sealed class LocalizationTestSetup
    {
        private UnityEngine.Localization.Locale _previous;

        [OneTimeSetUp]
        public void PinKorean()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            _previous = LocalizationSettings.SelectedLocale;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
        }

        [OneTimeTearDown]
        public void Restore()
        {
            if (_previous != null) { LocalizationSettings.SelectedLocale = _previous; }
        }
    }
}
