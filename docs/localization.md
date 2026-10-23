# 다국어(Localization)

Unity Localization 패키지로 ko, en, ja, zh-Hans를 지원한다. 문구의 원본은 `Assets/_Project/Localization/`의 `UIStrings` 테이블이고, 코드와 프리팹에는 한국어 문구를 직접 두지 않는다.

## 구성

| 대상 | 위치 | 역할 |
| --- | --- | --- |
| 문자열 테이블 | `Localization/UIStrings_*.asset` | 키 하나에 로케일 4개 값과 Context(Comment 메타데이터)를 둔다 |
| 폰트 테이블 | `Localization/GameAssets_*.asset`의 `UIFont` | ja는 `NotoSansJPSdf`, zh-Hans는 `NotoSansSCSdf`. ko, en은 비워 두어 라벨의 원래 폰트를 쓴다 |
| 조회 헬퍼 | `Game.Core.L10n` | `L10n.Get(key)`, `L10n.Get(key, args)`. 인자 자리는 `{0}`, `{1}` |
| 정적 라벨 | 프리팹의 `LocalizeStringEvent` | 코드가 값을 쓰지 않는 라벨의 문구를 키로 연결한다 |
| 폰트 교체 | `Game.View.LocalizedFont` | UI 라벨에 붙어 ja, zh-Hans에서 폰트를 바꾸고 원래 머티리얼의 외곽선 설정을 옮긴다 |
| 언어 선택 | 설정 팝업의 드롭다운 | `GameSettings.Language`를 저장하면 `GameSettingsApplier`가 `SelectedLocale`에 반영하고, 로비라면 씬을 다시 로드한다 |

로케일은 `LocalizationSettings.SelectedLocale`로만 바꾼다. 별도의 현재 언어 상태를 만들지 않는다.

## 문구 추가

1. 코드가 조립하는 문구는 `L10n.Get("화면.용도", 인자)`로 쓴다. 프리팹에 고정된 문구는 라벨에 `LocalizeStringEvent`를 붙이고 같은 키를 연결한다.
2. `UIStrings`에 키를 만들고 ko, en, ja, zh-Hans 값을 모두 채운다. 빈 값이 있으면 화면에 "No translation found"가 그대로 나온다. Context에는 어느 화면의 문구인지와 자리표시자의 뜻을 적는다.
3. ko 값은 기존 화면의 문구와 같게 둔다. 영어로 고정돼 있던 표식(`BOSS`, `NEW` 등)도 ko에서 그대로 유지한다.
4. 코드가 `.text`로 값을 쓰는 라벨에는 `LocalizeStringEvent`를 붙이지 않는다. 활성화될 때 테이블 값으로 덮어써서 코드가 쓴 값이 사라진다.

키 이름은 소문자로 시작하는 카멜케이스를 점으로 구분한다(`dailyShop.refreshCost`). **키에 숫자를 쓰지 않는다.** `m_Key: 값` 형태가 저장소의 gitleaks 훅에 API 키로 오탐된다. `key`, `token`, `secret`, `password` 같은 단어도 피한다.

## 번역하지 않는 것

- 로그, 데이터 검증 예외 등 개발자용 문구
- 서버가 내려주는 값(닉네임 등). 스킬·카드·아이템 이름·설명과 튜토리얼 대사는 클라이언트 JSON 파일로 번역한다(아래 "알아 둘 점")
- 문의 메일 본문. 운영팀이 한국어로 읽는다
- 언어 이름(`한국어`, `English`, `日本語`, `简体中文`). 현재 로케일과 무관하게 항상 같은 글자로 보여야 해서 전용 폰트 `LanguageNamesSdf`(JP/SC 폴백)를 쓴다

## 알아 둘 점

- 게임 콘텐츠 표시 문구(스킬·카드·아이템·일반 카드 이름·설명, 튜토리얼 대사)는 `UIStrings`가 아니라 `Resources/ClientData/DisplayTexts.<코드>.json`, `TutorialPresentations.<코드>.json`(코드는 en, ja, zh-Hans)에 둔다. 접미사 없는 파일이 ko 원본이고, 번역 파일에는 같은 키와 구조를 쓴다. 항목이나 값이 비었거나 파일이 없으면 ko 값을 쓴다. 튜토리얼의 `Speaker`는 초상 매칭용 식별자라 번역하지 않는다. `GameDataStore`가 시작 로케일로 로드하고, 로케일이 바뀌면 `DisplayTextLocalizer`가 `ApplyDisplayLocale`로 이미 만든 객체의 문구를 제자리에서 갱신한다.
- 코드가 한 번 써 넣은 문구(`label.text = L10n.Get(...)`)는 로케일을 바꿔도 다시 계산되지 않는다. 그래서 로비에서 언어를 바꾸면 `GameSettingsApplier`가 로케일을 적용한 뒤 `GoToLobby()`로 로비 씬을 다시 로드해 UI를 새 언어로 만든다. 전투 중에는 재로드하지 않고 다음 씬 진입 때 새 언어가 적용된다.
- ja, zh-Hans 폰트는 Noto Sans(OFL) 원본이다. 파일이 커서 빌드 크기가 늘고, 대용량 파일 검사에서 `ttf|otf`를 제외했다.
- 에디터에서는 `LocalizedFont`가 동작하지 않는다. ja, zh-Hans 표시는 플레이 모드에서 확인한다.
- 개발 기기에 저장된 언어(PlayerPrefs `settings.language`)가 화면 문구를 바꾼다. PlayMode UI 테스트는 시작할 때 이 값을 지워 한국어로 고정하고 끝나면 복원한다.
