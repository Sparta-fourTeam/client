# Android 태그 출시

## 출시 신호와 실제 승인

검토된 **dev 커밋 하나에 새 `vMAJOR.MINOR.PATCH` 태그를 push**하면 그 커밋을 출시 대상으로 고정한다. `v1.0.3`이면 앱 표시 버전은 `1.0.3`이다. JSON 후보 수정이나 Actions 버전 입력은 필요 없다.

흐름은 **태그 사전 검증 → 고정 출시 브랜치의 main PR → 보호 규칙에 따른 사람의 merge commit 병합 → 실제 merge SHA 빌드·검증 → internal 게시**이다. 출시 PR을 main에 병합하는 행위가 내부 게시의 최종 승인이다. 일반 기능 PR이나 태그 없는 main 변경은 게시하지 않는다.

현재 배포 workflow의 token은 `contents: read`, `actions: read`뿐이다. 저장소 main ruleset은 PR을 요구하고 우회 권한이 없으며, Actions의 PR 생성/승인 설정과 저장소 auto-merge도 꺼져 있다. **현재 권한으로 태그 하나만으로 PR 생성·병합까지 자동화할 수는 없다.** workflow는 저장소를 수정하거나 직접 main push/병합을 시도하지 않는다. 새 credential이나 보안 권한을 추가하지 않는다.

## 첫 사용 전에 필요한 조치

1. 이 배포 코드와 관련 main release 설정을 검토하여 main 및 기본 브랜치 dev에 동기화한다. 태그 push의 workflow는 태그 커밋에서, PR 이벤트의 workflow는 기본 브랜치에서 사용할 수 있어야 한다. 기존 dev에는 예전 `Release APK`의 `v*` 자동 트리거와 오래된 Play 배포 코드가 있으므로 **동기화 전에는 출시 태그를 push하지 않는다.** 이 PR은 dev 전체 통합을 수행하지 않는다.
2. 저장소 담당자가 `v*` 태그에 **수정/강제 이동과 삭제를 금지하고 우회가 없는 tag ruleset**을 설정한다. 이 설정은 이번 변경에 포함하지 않는다. 태그 요청은 `GITHUB_REF_PROTECTED=true`가 아니면 중단한다. 이 값만으로 모든 ruleset 세부 조건을 보증하지 않으므로 담당자가 실제 수정·삭제 금지와 우회 부재를 확인해야 한다.
3. **`300000000` 오프셋이 실제 Play 전체 사용 코드보다 크다는 점은 아직 확인하지 않았다.** 알려진 legacy 초안 `214011987`와 작은 사용 코드·과거 code4 후보보다 큰 값일 뿐이다. 담당자가 모든 트랙·초안의 최고 코드가 첫 자동 코드보다 작은지 Console에서 확인해야 한다. 더 큰 코드가 있으면 범위를 재검토하고 실행하지 않는다. 다른 업로더가 이 자동 범위의 코드를 발급하지 않게 관리한다.
4. dev 소스의 `.github/`, `docs/android-release.md`, `Assets/Settings/Build Profiles/Android_Release.asset`, `Assets/_Project/Scripts/Editor/CiBuild.cs`가 현재 main과 동일해야 한다. 과거 dev 설정이 main의 패키지·서명/버전 적용 코드·수동 복구·APK 트리거 정책을 되돌리지 않게 검사한다. 런타임 변경 충돌도 dev에서 먼저 검토·해결한 뒤 새 커밋에 태그를 붙인다. Unity YAML은 저장소 지침대로 Unity에서 저장한다.

## 한 번의 출시

출시 담당자는 원하는 정확한 dev SHA를 확인하고 새 버전 태그를 만든다. 최신 dev를 가리키는지는 별도 확인하며, 이후 dev가 전진해도 대상은 바뀌지 않는다.

```sh
git tag -a v1.0.3 <검토된-dev-커밋-SHA> -m 'Release 1.0.3'
git push origin refs/tags/v1.0.3
```

**Actions → Deploy Google Play Internal**의 태그 실행이 성공할 때까지 기다린다. source가 dev의 조상인지, 태그가 신규/보호 상태인지, main 배포 기반이 보존되는지, main과 충돌이 없는지 검사한다. Summary에는 정확한 SHA를 사용한 다음 단계 명령이 나온다.

- `release/v1.0.3` 브랜치를 **태그가 가리킨 SHA 그대로** 생성·push하고 main 대상 PR을 만든다. 움직이는 dev 브랜치 자체로 PR을 만들지 않는다.
- release PR 검증 결과를 확인하고 main의 모든 리뷰/필수 검사 조건을 충족한다. **Create a merge commit** 방식으로 병합한다. squash/rebase는 허용하지 않는다.
- 해당 PR의 `closed + merged` 이벤트가 같은 workflow에서 실제 main merge SHA를 checkout한다. 원본 태그 요청과 merge tree를 재검증한 뒤 AAB를 빌드하고 internal에 게시한다. 일반 출시에는 수동 publish 체크박스가 없다. 태그와 검증된 출시 PR 병합이 게시 승인이므로 게시 의사가 없으면 PR을 병합하지 않는다.

main이 태그 검증 당시 base에서 바뀌었거나 release 브랜치에 다른 커밋이 추가되면 거절한다. 충돌 해결을 PR 브랜치에 추가하는 방식도 태그 source와 달라지므로 거절한다. dev에서 통합·검토한 새 커밋에 **새 버전 태그**를 사용해야 한다. 한 번에 하나의 출시를 진행한다. concurrency 그룹은 FIFO가 아니며 새 요청이 오래된 pending 실행을 취소하거나 코드 검사에서 오래된 실행을 중단시킬 수 있다.

## 정확한 소스와 재사용 방지

태그 단계는 원본 tag object SHA·태그 commit SHA·main base SHA·요청 run ID를 `tag-release-request` artifact에 90일간 보관한다. PR 검증·게시 단계는 성공한 원본 태그 실행 하나와 그 artifact를 요구한다. artifact/실행 이력이 사라지거나 만료되면 중단한다. 태그를 이동·삭제·재생성하거나 같은 이름으로 재요청하지 않는다. 강제 이동 이벤트, 원격 객체 변경, 중복 태그 실행, 재실행을 거절한다. 태그 보호를 해제하거나 관리자가 이력을 삭제한 상황에서 과거 모든 이름의 재사용 부재를 보장하지 않는다.

release PR head는 tag commit과 같아야 하고, 실제 merge commit의 부모는 `[요청 당시 main base, tag commit]` 두 개여야 한다. 충돌 없는 Git merge로 계산한 예상 tree가 실제 merge tree와 같아야 한다. build/publish 시 현재 main도 해당 merge SHA여야 한다. **태그 commit SHA와 빌드 merge SHA는 서로 다를 수 있다.** main의 기존 수정까지 통합된 tree를 빌드하며, 증거에 두 SHA·tag object·base·PR 번호·tag request run·integrated tree를 함께 남긴다. 이후 새 dev 커밋이나 다른 main 커밋을 조용히 섞지 않는다.

## 자동 versionCode와 실패 재시도

Play AAB는 **병합 이벤트 실행의** `versionCode = 300000000 + GITHUB_RUN_NUMBER`를 사용한다. 기존 workflow ID `378016847`에 결합된 카운터이며 태그 검증·PR 검사·실패·복구 실행도 번호를 소비하므로 빈칸이 생긴다. 표시 버전과는 독립적이다. run ID 자체는 코드로 쓰지 않는다.

GitHub API로 repository·workflow ID·run ID/번호/attempt·이벤트·SHA를 확인한다. 전체 실행 이력을 끝까지 읽고 더 큰 번호가 있으면 오래된 실행을 거절한다. 게시 직전 원래 빌드 코드와 다시 대조한다. 현재 checkout과 최신 main 장부의 모든 후보/업로드/legacy 코드보다 커야 하며 Play 상한 `2100000000`과 release name 50자 제한을 검사한다. API 오류는 중단한다. workflow 표시 이름은 같은 ID이면 변경할 수 있지만 파일 이동/재생성 등으로 ID가 바뀌면 중단하며 범위 이관 검토가 필요하다.

**Re-run jobs / Re-run failed jobs는 거절한다.** Unity/네트워크 실패로 아직 업로드하지 않았더라도 예외가 없다. 새 버전 태그와 새 출시 PR이 필요하고 처음부터 빌드하므로 시간과 실행 비용이 다시 든다. 기존 artifact만 재게시하는 기능은 없다. 이미 main에 통합된 실패 출시의 재시도는 변경된 main을 dev에 통합하고 새 버전으로 검토해야 한다. 업로드 결과가 불명확하면 먼저 Console에서 수용 여부를 확인한다.

[GitHub 변수 규칙](https://docs.github.com/en/actions/reference/workflows-and-actions/variables), [concurrency 규칙](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency), [이벤트 규칙](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows), [token으로 만든 이벤트의 후속 실행 제한](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow), [Android 버전 규칙](https://developer.android.com/studio/publish/versioning)을 따른다. merge는 사람이 기존 권한으로 수행하므로 token이 만든 push가 후속 workflow를 실행하지 않는 제한을 우회하지 않는다.

## 기존 안전장치와 수동 복구

서명, package `com.teamdoodle.doodledefense`, API 36+, ARM64, 비개발 AAB, 실제 manifest·서명·16KB alignment 검증과 게시 직전 hash/출처 검증을 유지한다. `.github/android-release.json`의 기존 기록은 그대로 보존한다. 자동 후보/빌드 성공을 Play 업로드 완료로 기록하지 않는다. 실제 업로드 수용 기록은 증거로 확인한 뒤 별도로 관리할 수 있으며 다음 자동 실행마다 JSON을 편집할 필요는 없다.

**Actions → Deploy Google Play Internal → Run workflow**는 고정 원본 code2 복구 전용이다. main에서 `release_version=1.0.2`, `recover_original_code2=true`, `publish_to_internal=true`, `acknowledge_edit_invalidation=true`를 명시해야 한다. 원본 artifact/hash·게시 범위·edit 무효화·재실행 금지 검증은 유지한다. 일반 출시가 이 수동 경로로 태그/PR 검증을 우회할 수 없으며 복구는 태그/PR 이벤트에서 실행하지 않는다.

**Release APK**는 기존 main의 수동 `workflow_dispatch` 전용 경로와 `--apk-build` 검증을 유지한다. `v*` push를 구독하지 않으므로 새 Play 출시와 중복 자동 실행하지 않는다. 기존 dev의 이전 APK 트리거는 첫 사용 전 main의 설정으로 동기화해야 한다. Dev APK는 기존 개발용 정책을 유지한다. 과거 APK 재빌드를 Play 재업로드 허가로 해석하지 않는다.
