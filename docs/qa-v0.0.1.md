# v0.0.1 빌드 QA 결과

## 빌드 설정

- 플랫폼: Android (APK)
- Orientation: Portrait 고정
- Scripting Backend: IL2CPP
- 패키지명: `com.teamdodle.dodledodle`
- 서명: 커스텀 키스토어 없음 (디버그 키 사용, 패스워드 불필요)
- Canvas Scaler: Lobby/Stage 전체 Canvas가 1080x1920 / ScaleWithScreenSize / MatchWidthOrHeight(0.5)로 동일하게 설정되어 있음을 확인 (수정 불필요)

## 빌드 결과

- 개발 빌드: Succeeded, 에러 0개, 경고 7개 (전부 사소함 — IL2CPP 컴파일 정보, 쉐이더 deprecated 경고, `DataVersions.cs`의 `[SerializeField]` 누락 권고 등)
- 배포 빌드: 아래 "배포 빌드 결과" 참고

## 런타임 분석 (Logcat, BlueStacks 플레이 테스트)

- 앱 기동 정상: IL2CPP, URP, 오디오(AAudio), 입력(GameActivity) 전부 정상 초기화
- `FATAL EXCEPTION` / `AndroidRuntime` 크래시 로그 없음 — 플레이 중 앱이 튕기지 않음
- 반복 발생하지만 **무해한** 에러: `ClassNotFoundException: com.google.android.play.core.assetpacks.*` — Google Play 에셋 전송(Asset Delivery) 관련 클래스가 없어서 발생. 이 프로젝트는 해당 기능을 쓰지 않으므로 게임 동작에 영향 없음 (Unity 엔진이 방어적으로 시도 후 조용히 넘어감)
- 수동 플레이 테스트: 버벅임·멈춤·화면 깨짐 등 체감 이상 없음

### [확인된 이슈] Bloom 포스트프로세싱이 빌드에서 렌더링되지 않음

`DefaultVolumeProfile.asset`에서 **Bloom이 활성화(active: 1)** 돼 있는데, Android 빌드 로그에 다음이 반복 출력됨:

```
Shader 'Hidden/Universal Render Pipeline/Bloom' is not supported or has been stripped from the build
(in 'BloomPostProcessPass'). PostProcessing render passes will not execute.
```

즉 **에디터에서는 Bloom이 정상 적용된 것처럼 보이지만, 실제 빌드에서는 쉐이더가 Strip(제거)되어 Bloom 효과가 전혀 렌더링되지 않음.** Depth of Field / Motion Blur / Panini Projection도 같은 경고가 뜨지만, Volume Profile 상 모드가 꺼져 있어 실질적 영향은 적어 보임.

**권장 조치**: URP 에셋의 Shader Stripping 설정 또는 `Always Included Shaders` 목록에 Bloom 쉐이더를 추가해서 빌드에서 제거되지 않도록 수정 필요.

## 퍼포먼스

- 메모리 (`dumpsys meminfo`): 전체 PSS 약 284MB (Native Heap 38MB, Dalvik Heap 1.9MB 등) — 2D 모바일 게임 기준 정상 범위, 과도한 사용 징후 없음
- 프레임레이트: 객관적 수치 확보 실패
  - BlueStacks 에뮬레이터 네트워크 특성상 Unity Profiler 자동 연결이 되지 않음 (Player Connection 디스커버리 실패)
  - Android `dumpsys gfxinfo`는 Unity가 Android View 시스템 밖에서 직접 그리는 렌더링 프레임을 추적하지 못해 참고 불가 (`Total frames rendered: 0`으로 나오는 게 정상)
  - 사용자 수동 플레이 기준으로는 프레임 끊김 체감 없음

## 배포 빌드 결과

- 결과: **Succeeded** (에러 0개, 경고 7개 — 개발 빌드와 동일하게 전부 사소함)
- 빌드 시간: 약 85초 (IL2CPP 캐시 재사용으로 개발 빌드 대비 훨씬 빠름)
- 파일: `Build/Android/DodleDodle_v0.0.1.apk` (약 44.4MB)
- Development Build / Script Debugging / Profiler Autoconnect 모두 끄고 빌드함
