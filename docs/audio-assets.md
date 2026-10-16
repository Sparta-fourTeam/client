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
