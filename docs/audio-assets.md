# 사운드 에셋 목록

시각 에셋은 [required-assets.md](required-assets.md)를 본다. 소리를 코드에 잇는 구조는 `Core/Audio`의 `IAudioService`, `AudioLibrary`, `BattleSoundPlayer`다.

`Resources/Audio/AudioLibrary.asset`의 항목 `id`에 클립을 끌어다 넣는다. ID는 [SoundIds.cs](../Assets/_Project/Scripts/Runtime/Core/Audio/SoundIds.cs)와 같아야 한다. 클립이 없는 ID는 조용히 무음이다.

## 제작 규격

- 효과음: 모노, 44.1kHz, 짧게(대부분 1초 이하). 같은 ID에 클립을 여러 개 넣으면 무작위로 하나를 재생한다.
- 배경음: 스테레오, 끊김 없이 이어지는 루프. 앞뒤 무음 없이 잘라서 받는다.
- 파일명은 소문자 snake_case, 한글·공백 금지. 위치는 `Assets/_Project/Audio/`.
- 볼륨은 클립을 정규화(-1~-3dBFS)해서 받고, 항목의 `volume`으로 맞춘다.

## 1차: 이미 코드에 연결된 것 (전투)

| ID | 종류 | 언제 | 비고 | 우선 |
| --- | --- | --- | --- | --- |
| `bgm_battle` | BGM 루프 | 전투 내내 | 1~2분 루프, 긴장감 있되 반복 피로가 적게 | 필수 |
| `sfx_enemy_die` | SFX | 일반 적 처치 | 가장 자주 난다. 2~3개 변형 권장, 짧고 가볍게 | 필수 |
| `sfx_elite_die` | SFX | 엘리트 처치 | 일반보다 묵직하게 | 필수 |
| `sfx_boss_die` | SFX | 보스 처치 | 1~2초, 임팩트 | 필수 |
| `sfx_wave_start` | SFX | 웨이브 시작(첫 웨이브는 전투 시작 때) | 짧은 알림음 | 필수 |
| `sfx_special_warning` | SFX | 엘리트·보스 등장 경고 | 경고 연출 시간만큼 이어지는 느낌 | 필수 |
| `sfx_wall_hit` | SFX | 방벽 피격 | 2~3개 변형 | 필수 |
| `sfx_wall_destroyed` | SFX | 방벽 파괴 | 패배 직전의 큰 소리 | 필수 |
| `sfx_stage_clear` | 징글 | 스테이지 클리어 | 2~3초 | 필수 |
| `sfx_stage_fail` | 징글 | 스테이지 실패 | 2~3초 | 필수 |

### VARCO Sound 플러그인 준비

- **지원 버전**: 공식 문서 기준 Unity 2021.3 이상이다. 우리 프로젝트는 Unity 6000.6이라 실제로 동작하는지는 직접 확인한다.
- **설치**: [공식 문서](https://api.varco.ai/en/docs/plugin-sound)에서 Unity용 `.unitypackage`를 내려받아 Assets → Import Package → Custom Package...로 가져오고, Window > VARCO Sound로 연다. 생성은 온라인 API를 쓰므로 인터넷 연결이 필요하다.
- **계정·키·비용**: 공식 문서에는 키 요구가 따로 적혀 있지 않고 크레딧으로 과금한다. 텍스트→소리는 호출당 35 + (5 × 샘플 수) 크레딧(최소 40), 음악 생성은 호출당 200크레딧이다. 로그인이나 API 키를 요구하면 개인 것을 쓰고 저장소에 넣거나 커밋하지 않는다.
- **이 저장소에 설치하지 않는다**: 패키지 파일이 저장소에 들어가 팀 전체에 영향이 간다. 빈 임시 Unity 프로젝트에 설치해서 소리를 만들고, 완성된 WAV만 이 프로젝트로 가져온다.


### 1차 제작용 프롬프트와 길이

VARCO Sound 플러그인(Window > VARCO Sound)에 넣을 영문 프롬프트다. 텍스트→소리는 항상 **10초짜리**가 나오므로 아래 **목표 길이**로 잘라서 쓴다. 같은 프롬프트도 결과가 다르니 샘플을 5개씩 뽑아 가장 나은 것을 고른다(호출당 크레딧은 샘플이 많을수록 늘어 1개 40, 3개 50, 5개 60이지만, 기본 35를 샘플이 나눠 가져 소리당 단가는 5개일 때 12크레딧으로 가장 싸다).

| ID | 목표 길이 | 프롬프트 |
| --- | --- | --- |
| `sfx_enemy_die` | 0.6초 | Tiny cute cartoon pop and soft squish as a small slime monster bursts, very short, light and playful, no music |
| `sfx_elite_die` | 1.0초 | Cartoon strong monster defeated, deeper pop with a short crunchy thud and a small sparkle at the end, casual mobile game |
| `sfx_boss_die` | 2.2초 | Big boss monster defeated, deep cartoon explosion with a descending groan and a bright sparkle finish, one shot, no music |
| `sfx_wave_start` | 1.5초 | Short bright cartoon horn call announcing a new wave of enemies, playful mobile game, one quick phrase |
| `sfx_special_warning` | 2.0초 | Urgent warning alarm for a dangerous enemy approaching, two quick low pulsing horn blasts, cartoon style, no voice |
| `sfx_wall_hit` | 0.7초 | Stone wall hit by a small creature, dull cartoon thud with a few tiny crumbling pebbles, short impact |
| `sfx_wall_destroyed` | 2.6초 | Stone wall collapsing, heavy crumble and crash with falling rocks, dramatic but cartoonish, single event |
| `sfx_stage_clear` | 3.0초 | Short cheerful victory jingle with bright bells and a tiny fanfare, playful mobile game, triumphant |
| `sfx_stage_fail` | 2.5초 | Short comedic sad failure sting, descending wobbly brass notes, cartoon style, not too dramatic |
| `bgm_battle` | 60~120초 루프 | (음악 생성) Upbeat tense but cute battle loop for a casual tower defense game, 120 BPM |

- 클리어·실패 징글과 배경음은 음악에 가까워 텍스트→소리로는 품질이 낮을 수 있다. 플러그인의 음악 생성(호출당 200크레딧)을 쓰거나 다른 출처를 고려한다.
- 게임 분위기는 귀엽고 가벼운 손그림 모바일 디펜스다(슬라임·박쥐 몬스터, 돌 방벽). 무겁고 사실적인 소리는 어울리지 않는다.

### 만든 소리를 게임에 적용하기

1. **파일 이름과 위치**: 클립 파일은 소리 ID에 번호를 붙여 `sfx_wall_hit_01.wav`, `sfx_wall_hit_02.wav`처럼 짓고(변형이 하나여도 `_01`) `Assets/_Project/Audio/`에 넣는다. 폴더가 없으면 만든다. 소리 ID는 파일 이름이 아니라 `AudioLibrary` 항목의 `id`이고, 번호를 붙여야 변형 파일끼리 덮어쓰지 않는다. 이름은 소문자 snake_case다.
2. **길이 다듬기**: 10초짜리에서 앞쪽 무음을 없애고, 목표 길이로 자르고, 끝을 짧게 페이드 아웃한다. 최대 볼륨은 편집기의 Normalize로 -3dB 안팎에 맞춘다. 플러그인에 자르기 기능이 없으면 Audacity 같은 편집기를 쓰거나, 아래 명령으로 자른다(무음 제거 → 0.7초로 자르기 → 끝 페이드).
   ```bash
   ffmpeg -i in.wav -af "silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.01,atrim=0:0.7,afade=t=out:st=0.66:d=0.04" -ac 1 -ar 44100 sfx_wall_hit_01.wav
   ```
   무음을 없앤 뒤 소리가 목표 길이보다 짧으면 페이드 시작 `st`를 `길이 - 0.04`로 바꾼다.
3. **임포트 설정** (Inspector에서 파일 선택):
   - 효과음: Force To Mono 켬, Load Type `Decompress On Load`(아주 짧은 것) 또는 `Compressed In Memory`, Compression `Vorbis`.
   - 배경음: Load Type `Streaming`, Compression `Vorbis`. 끊김 없이 이어지는 루프 파일이어야 한다.
4. **AudioLibrary에 연결**: `Assets/_Project/Resources/Audio/AudioLibrary.asset`을 선택하고 `entries`에서 **같은 id** 항목(`sfx_wall_hit`)을 찾아 `clips`에 번호 붙은 클립(`_01`, `_02`, ...)을 모두 끌어다 넣는다. 효과음은 `clips` 중 하나를 무작위로 재생한다. 배경음은 첫 번째 클립만 쓴다.
5. **값 조정**: 항목의 `volume`(0~1)으로 크기를, `pitchVariance`(0~0.5)로 반복 때 음높이 흔들림을, `minInterval`(초)로 같은 소리가 겹쳐 커지는 것을 막는다. 처치음처럼 자주 나는 소리는 `pitchVariance`를 0.1 안팎으로 둔다.
6. **확인**: 플레이 모드로 Boot부터 시작해 스테이지 1을 하고 웨이브 시작, 적 처치, 방벽 피격, 파괴, 클리어·실패에서 소리가 나는지 듣는다. 전체 볼륨은 로비 설정 팝업의 음악·효과음 설정(믹서 그룹 Music·Sfx)을 따른다.
7. **문제 확인**: 클립이 비어 있는 id는 조용히 무음이고, 라이브러리에 없는 id는 콘솔에 경고가 한 번 나온다. 소리가 안 나면 id 철자와 `clips`가 채워졌는지부터 본다.

## 2차: 아직 코드에 없는 것 (에셋이 정해지면 연결)

씬별 음악과 무기별 시전·명중음은 아래 "씬·무기 연결" 항목처럼 연결되어 있다. 다음 표는 그 외 확장 후보를 포함한다.

| ID(안) | 종류 | 언제 | 비고 | 우선 |
| --- | --- | --- | --- | --- |
| `sfx_ui_back` | SFX | 닫기·뒤로 | | 중간 |
| `sfx_popup_open` | SFX | 팝업 열림 | | 중간 |
| `sfx_card_pick` | SFX | 카드 선택 | | 높음 |
| `sfx_roulette_stop` | SFX | 엘리트 룰렛 당첨 | 정지 순간 별도 소리 | 중간 |
| `sfx_reward_get` | SFX | 보상 획득 | 결과 화면 | 중간 |
| 음성(Voice) 채널 | | | 믹서에 그룹은 있으나 쓰는 곳이 없다. 필요 여부부터 결정 | 보류 |

## 씬·무기 연결

원본은 사용자가 넣은 `Assets/_Project/Sound/`에 보존하며, 게임이 참조하는 파일은 `Assets/_Project/Audio/`에 정리한다. 시전·명중 효과음은 앞쪽 무음을 정리하고 0.2~0.9초로 잘라 짧은 페이드를 적용한다. 원본의 샘플레이트를 유지하며 효과음은 모노·Decompress On Load, 배경음은 스테레오·Streaming으로 임포트한다.

| 음악 ID | 재생 씬 | 원본 파일 |
| --- | --- | --- |
| `bgm_intro` | Loading | `introscene.mp3` |
| `bgm_lobby` | Lobby | `lobbyscene.mp3` |
| `bgm_battle` | Stage 전투 진행 | `StageScene_.mp3` |

`SceneLoader`가 Loading·Lobby 음악을 바꾸고, 전투 음악의 시작·종료는 `BattleSoundPlayer`가 전투 상태에 맞춘다. 음악은 크로스페이드하며 기존 Music·Sfx 믹서 볼륨 설정을 따른다.

일반 버튼의 `sfx_ui_click`은 `Click.wav`, 잠긴 버튼의 `sfx_ui_locked`는 기존 `Button_click_1.wav`를 쓴다. `ButtonPressFeedback`에 루트의 `IAudioService`를 주입하여 모든 화면에서 활성화되고 상호작용 가능한 버튼을 누를 때 재생한다. `IButtonLockState`로 기능 잠금 판·잠긴 스킬 탭·장비·스킬 칸을 구분하여 둘 중 하나만 재생한다. 빈 화면·단순 비활성 버튼에는 재생하지 않으며, 크기 애니메이션을 끈 버튼에도 소리는 적용한다. 실행 중 생성되는 버튼도 같은 입력 경로를 따른다.

| 연출 ID | 재생 시점 | 원본 파일 |
| --- | --- | --- |
| `sfx_upgrade_success` | 로비 스킬·장비 강화 성공 후, 일괄 강화도 완료 시 한 번 | `Upgrade.wav` |
| `sfx_special_warning` | 엘리트·보스 등장 WARNING | `Enemy_warning_1.wav` |
| `sfx_level_up` | 레벨업의 새 레벨 표시·새 컨텐츠 자물쇠 열림·보상 팝업의 첫 보상 등장 | `Level_up_1.wav` |
| `sfx_roulette_spin` | 룰렛 회전 중 반복, 회전 완료·스킵·화면 이탈 시 정지 | `roulette_spin (2).wav` |
| `sfx_stage_clear` | 스테이지 클리어 판정 | `Stage_clear_3.wav` |
| `sfx_stage_fail` | 스테이지 실패 판정 | `Stage_Failed.wav` |
| `sfx_card_show` | 새 선택지 카드 묶음이 나타날 때 한 번 | `Card_Select.wav` |

이 연출음은 전투 일시정지 중에도 재생한다. 룰렛의 반복음은 일반 UI 효과음 보이스와 분리하여 클릭음과 겹쳐도 유지하고, 다음 회전에서 같은 AudioSource를 재사용한다. 원본의 복사본은 중복 등록하지 않는다. 레벨업과 보상을 함께 보여주는 화면은 한 번만 재생하고, 레벨업 없이 보상만 받는 화면도 첫 보상 등장 때 재생한다. 강화 취소·실패에는 완료음을 내지 않는다.

선택지 등장음은 카드 한 장마다가 아니라 후보 묶음당 한 번 재생한다. 일시정지 팝업을 닫아 같은 후보로 돌아오면 반복하지 않고, 다음 웨이브 등에서 새 후보가 나오면 다시 재생한다.

| 무기 | 시전 ID | 명중 ID |
| --- | --- | --- |
| 화살 | `sfx_arrow_cast` | `sfx_arrow_hit` |
| 화염구 | `sfx_fireball_cast` | `sfx_fireball_hit` |
| 벼락 | `sfx_lightning_cast` | `sfx_lightning_hit` |
| 서리 결정 | `sfx_ice_cast` | `sfx_ice_hit` |
| 나무뿌리 | `sfx_vine_cast` | `sfx_vine_hit` |
| 냉기 지대 | `sfx_cold_zone_cast` | `sfx_cold_zone_hit` |
| 전기 구름 | `sfx_cloud_cast` | `sfx_cloud_hit` |
| 에너지 빔 | `sfx_beam_cast` | `sfx_beam_hit` |
| 연쇄 번개 | `sfx_chain_cast` | `sfx_chain_hit` |
| 집중 광선 | `sfx_focus_cast` | `sfx_focus_hit` |

보조 화살·얼음 조각·보조 빔 등은 같은 계열의 명중음을 공유한다. 주 무기의 `SkillFired`로 시전음을 내고, 실제 스킬 피해가 있는 `EnemyHpChanged`로 명중음을 낸다. 회복·무효 피해·스킬 출처 없는 상태 피해는 명중음을 내지 않는다. 장판·빔의 반복 타격도 소리는 내되 볼륨과 ID별 최소 재생 간격으로 겹침을 제한한다. 소리 변주는 게임플레이 난수와 분리한다. 방벽 피격음에는 `Wall_hit_3.wav`를 사용한다.

에너지 빔 시전음은 `SkillFired.Duration`의 강화가 반영된 공격 지속시간 동안 반복한다. 공격이 끝나기 전 마지막 실제 0.3초 동안 볼륨을 줄이고 종료하며, 배속에 맞춰 남은 공격 시간을 환산한다. 일시정지 중에는 재생 위치와 남은 시간·페이드 볼륨을 함께 보존한다.

무기·방벽 피격음은 `pauseWithBattle`로 지정한다. 카드 선택·엘리트 룰렛·일시정지 중에는 `AudioSource.Pause()`로 재생 위치를 보존하고 전투가 재개되면 `UnPause()`로 이어서 재생한다. 이 동안 새 전투 효과음은 재생하지 않으며, 전투 종료·씬 이탈 시 보존한 효과음도 종료한다. 배경음과 UI 클릭음은 계속 재생하고, UI 효과음 보이스를 별도로 확보해 멈춘 공격음이 클릭음을 가로막지 않게 한다.

## 결정이 필요한 것

- 무기 형태 변환에 별도 시전·명중음을 추가할지.
- 음성(Voice) 채널을 쓰는지.
- 에셋 출처: 무료 사운드 팩 / 구매 / 자체 제작. 무료·구매 에셋은 라이선스(상업 이용, 크레딧 표기 여부)를 확인한다.
