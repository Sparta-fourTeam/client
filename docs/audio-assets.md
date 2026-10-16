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

VARCO Sound 플러그인(Window > VARCO Sound)에 넣을 영문 프롬프트다. 텍스트→소리는 항상 **10초짜리**가 나오므로 아래 **목표 길이**로 잘라서 쓴다. 같은 프롬프트도 결과가 다르니 샘플을 5개씩 뽑아 가장 나은 것을 고른다(호출당 크레딧이 같고 소리당 가장 싸다).

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

| ID(안) | 종류 | 언제 | 비고 | 우선 |
| --- | --- | --- | --- | --- |
| `bgm_lobby` | BGM 루프 | 로비 | 씬 전환 시 교체 | 높음 |
| `sfx_ui_click` | SFX | 버튼 누름 | 가장 흔한 UI 소리 | 높음 |
| `sfx_ui_back` | SFX | 닫기·뒤로 | | 중간 |
| `sfx_popup_open` | SFX | 팝업 열림 | | 중간 |
| `sfx_card_show` | SFX | 카드 선택지 등장 | | 높음 |
| `sfx_card_pick` | SFX | 카드 선택 | | 높음 |
| `sfx_roulette_spin` / `sfx_roulette_stop` | SFX | 엘리트 룰렛 | 돌아가는 동안과 멈출 때 | 중간 |
| `sfx_enemy_hit` | SFX | 적 피격 | 빈도가 매우 높다. 매우 짧게, 변형 3개 이상 | 높음 |
| `sfx_skill_*` | SFX | 스킬 발동·명중 | 스킬마다 필요한지 먼저 정한다. 계열별(번개·불 등) 공용도 가능 | 높음 |
| `sfx_reward_get` | SFX | 보상 획득 | 결과 화면 | 중간 |
| `sfx_upgrade_success` | SFX | 강화 성공 | | 낮음 |
| `sfx_level_up` | SFX | 레벨업 | | 낮음 |
| 음성(Voice) 채널 | | | 믹서에 그룹은 있으나 쓰는 곳이 없다. 필요 여부부터 결정 | 보류 |

## 결정이 필요한 것

- 스킬 소리를 스킬별로 만들지, 계열별 공용으로 할지.
- 음성(Voice) 채널을 쓰는지.
- 에셋 출처: 무료 사운드 팩 / 구매 / 자체 제작. 무료·구매 에셋은 라이선스(상업 이용, 크레딧 표기 여부)를 확인한다.
