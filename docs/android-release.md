# Android 릴리스 번호

`.github/android-release.json`이 Play AAB와 Release APK의 표시 버전과 versionCode를 함께 정한다. Dev APK는 개발용 기존 버전 정책을 유지한다.

- `versionName`: 현재 후보 표시 버전. 대응 태그는 `v<versionName>`이다.
- `versionCode`: 확인된 Play 출시의 다음 작은 정수. 첫 후보는 1이며, 이후 2, 3 순서로 진행한다.
- `playReleases`: 실제 Play 출시가 확인된 `{ "versionName": "...", "versionCode": ... }` 목록이다. 임시 빌드, GitHub Release, 태그, Play 미출시 초안을 이 목록에 넣지 않는다.

같은 미출시 후보를 다시 빌드하면 같은 code를 유지한다. 빌드마다 번호를 소비하지 않는다. 기존 UTC 초 기반 태그와 build-only 산출물은 빌드 기록이며 이 정책의 출시 번호가 아니다. 기존 기록은 삭제하거나 이동하지 않는다.

Play에서 후보가 실제로 출시된 것을 확인한 뒤 그 code와 표시 버전을 `playReleases`에 기록하고 다음 후보의 code를 정확히 1 증가시킨다. 표시 버전도 새 후보와 태그에 맞춰 갱신한다. 예를 들어 첫 출시 확인 후에는 다음처럼 기록한다.

```json
{
  "versionName": "1.0.2",
  "versionCode": 2,
  "playReleases": [{ "versionName": "1.0.1", "versionCode": 1 }]
}
```

두 release workflow는 동일한 검증 스크립트를 호출하고 같은 concurrency 그룹으로 직렬 실행한다. 스크립트는 현재 checkout의 기록과 checkout 시 가져온 최신 `origin/main`의 확인된 출시 기록을 함께 검사하여 이미 출시된 code, 누락/중복 기록, 이름 불일치와 다른 source tree의 기존 태그를 거절한다. Play 게시 job은 새 checkout으로 현재 main 기록을 다시 검사한다.

이 파일은 Play 서버 조회를 대체하지 않는다. Console에서 실제 출시·업로드 수용을 확인한 담당자가 기록을 갱신해야 한다. 초안에 이미 업로드된 code도 Play가 재사용을 거절할 수 있으므로, 낮은 새 code가 수용된다는 보장은 하지 않는다. 게시 전에 Console 기록을 확인하고 기존 승인 절차를 따른다. 새 자격증명이나 자동 Play API 설정은 필요하지 않다.

CI의 `versioning: Custom`이 profile의 기본 표시 버전을 덮어써 이 파일의 후보 번호를 적용한다. 영구 태그 annotation에는 빌드 영수증으로 `Android-Version-Code`, `Android-Version-Name`, source SHA, AAB hash, Actions run을 기록한다. 이 annotation 자체를 Play 출시 확인으로 해석하지 않는다.
