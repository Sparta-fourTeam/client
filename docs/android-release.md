# Android 릴리스 번호

`.github/android-release.json`이 Play AAB와 Release APK의 표시 버전과 versionCode를 함께 정한다. Dev APK는 개발용 기존 버전 정책을 유지한다.

- `versionName`: 현재 후보 표시 버전. 대응 태그는 `v<versionName>`이다.
- `versionCode`: 새 번호 체계에서 Play에 업로드되지 않은 다음 작은 정수. 첫 후보는 1이며, 이후 2, 3 순서로 진행한다.
- `playUploads`: 새 번호 체계에서 Play가 업로드를 수용한 `{ "versionName": "...", "versionCode": ... }` 목록이다. 공개/테스트 게시 완료를 기다리지 않고, 초안 업로드 수용 시점에도 기록한다.
- `legacyDraftUploads`: 작은 번호 전환 전에 수용된 미출시 high-code 초안의 보존 기록이다. 새 순차 카운터의 이력에 포함하지 않으며 그 code 자체를 다시 사용하지 않는다. 이 예외는 미출시 high-code에서 새 code1을 시도하기로 승인된 전환을 기록한다.

같은 미업로드 후보를 다시 빌드하면 같은 code를 유지한다. 빌드마다 번호를 소비하지 않는다. 로컬/build-only 산출물·GitHub Release·태그는 Play 업로드 수용의 증거가 아니다. 기존 기록은 삭제하거나 이동하지 않는다.

Play에서 새 후보 업로드가 실제 수용된 것을 확인한 뒤 해당 code와 표시 버전을 `playUploads`에 기록하고 다음 후보 code를 정확히 1 증가시킨다. 표시 버전도 새 후보와 태그에 맞춰 갱신한다. 첫 수용 확인 후에는 다음처럼 기록한다.

```json
{
  "versionName": "1.0.2",
  "versionCode": 2,
  "playUploads": [{ "versionName": "1.0.1", "versionCode": 1 }],
  "legacyDraftUploads": [{ "versionName": "1.0", "versionCode": 214011987 }]
}
```

두 release workflow는 동일한 검증 스크립트를 호출하고 같은 concurrency 그룹으로 직렬 실행한다. 스크립트는 현재 checkout과 checkout 시 가져온 최신 `origin/main`의 확인된 업로드 기록을 함께 검사해 이미 소비된 code, 누락/중복 기록, 이름 불일치와 다른 source tree의 기존 태그를 거절한다. Play 게시 job은 새 checkout으로 현재 main 기록을 다시 검사한다. 아직 Play에 업로드되지 않은 main 후보만으로 번호를 소비하거나 후보 이름을 고정하지 않는다.

이 파일은 Play 서버 조회를 대체하지 않는다. Console 담당자가 실제 업로드 수용 확인 후 기록을 갱신해야 한다. [Android 버전 규칙](https://developer.android.com/studio/publish/versioning)에 따라 업로드에 이미 사용한 code는 재사용할 수 없다. 낮은 새 code가 기존 high-code 초안과 함께 수용되는지는 실제 Console 검증 전에는 보장하지 않는다. 수용이 거절되면 번호 기록·태그를 임의로 덮어쓰지 말고 사유를 보고한다. 기존 게시 승인 절차도 유지한다. 새 자격증명이나 자동 Play API 설정은 필요하지 않다.

CI의 `versioning: Custom`이 profile의 기본 표시 버전을 덮어써 이 파일의 후보 번호를 적용한다. 태그 annotation에는 빌드 영수증으로 `Android-Version-Code`, `Android-Version-Name`, source SHA, AAB hash, Actions run을 기록한다. 이 annotation 자체를 Play 업로드 수용으로 해석하지 않는다.
