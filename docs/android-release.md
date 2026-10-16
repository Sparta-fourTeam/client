# Android 내부 테스트 배포

## 사용 방법

검토·통합된 dev 커밋에 새 `vMAJOR.MINOR.PATCH` 태그를 push하면 **Deploy Google Play Internal**이 그 태그 SHA를 직접 빌드해 내부 테스트에 게시한다. 표시 버전은 태그에서 읽고 versionCode는 자동 배정하므로 매번 `.github/android-release.json`을 수정하지 않는다. 태그를 push하는 것이 기존에 합의한 내부 테스트 게시 승인이다. 다른 Console 게시 변경과 API 업로드는 실행이 끝날 때까지 병행하지 않는다.

배포 액션은 출시 PR 생성, `@codex review`, 리뷰 대기, main 자동 병합을 수행하지 않는다. 일반 개발 PR의 리뷰 규칙·필수 검사·GitHub 보호 설정은 그대로 적용된다. 이 변경을 반영하는 PR 자체도 정상 검토·병합 절차를 따른다.

1. 배포 코드가 main과 dev에 검토·동기화되어 있는지 확인한다. 태그 소스의 `.github/`, 이 문서, `Assets/Settings/Build Profiles/Android_Release.asset`, `Assets/_Project/Scripts/Editor/CiBuild.cs`는 현재 main과 같아야 한다. 태그의 게임 소스는 dev에서 선택한 고정 커밋이다. **main에만 있는 게임 수정은 이 AAB에 포함되지 않는다.** 필요한 수정은 dev에 먼저 통합한다.
2. 고정한 dev 커밋에 아직 사용하지 않은 버전 태그를 생성·push한다. 태그를 이동·삭제·재사용하지 않는다. `v*` ruleset은 active, updates/deletion 제한, bypass 없음으로 유지한다.
3. **GitHub → Actions → Deploy Google Play Internal → 해당 태그 실행**에서 `tag_request → prepare → build → publish`를 확인한다. 추가 승인이나 Codex 응답을 기다리는 단계는 없다. 태그 source, 실제 build SHA, 원본 run·counter, versionCode, AAB 검증 증거와 최소 게시 receipt를 남긴다.

이전 태그가 참조하는 워크플로 파일은 변경되지 않는다. 특히 기존 `v1.0.4`는 이전 PR/리뷰 경로를 포함한다. 이 수정으로 과거 실행이나 PR을 자동 재개·병합하지 않으며, 이전 태그를 움직여 새 코드를 적용하지 않는다. 새 경로는 코드 동기화 후 새 태그부터 적용된다.

## 최신 dev AAB만 만들기

제출을 사용자가 직접 할 때는 **Actions → Deploy Google Play Internal → Run workflow**에서 branch를 **dev**, `build_only=true`, `release_version`을 새 표시 버전으로 입력한다. `publish_to_internal=false`, `recover_original_code2=false`를 유지한다. 새 태그는 만들지 않는다. 원본 code2 복구 옵션과 build-only를 함께 선택하면 거절한다.

수동 실행이 선택한 최신 dev SHA를 고정하고 검토된 main과 배포 인프라 일치 및 필수 CI·서명 AAB 검증을 수행한다. `publish` job은 push 태그에서만 실행되므로 이 수동 경로는 **AAB 생성만** 하며 Play 인증·업로드·내부 트랙 게시·Google 검토 제출을 하지 않는다. 게시 스크립트도 build-only provenance를 별도로 거절한다. 완료 후 `Release-AAB-<code>-<source SHA>` artifact의 `Android/*.aab`와 검증 manifest를 내려받아 사용자가 제출한다.

이 수동 실행도 같은 workflow counter를 사용한다. 예를 들어 다음 실행이 run13이면 versionCode5이며 이후 태그 실행은 더 큰 counter를 사용한다. 수동 build-only는 재실행하지 않고 새 수동 실행으로 새 코드를 배정한다. 기존 v1.0.4 태그와 다른 최신 소스를 같은 표시 버전으로 빌드할 수 있으나, 태그 빌드라고 기록하지 않는다. 정확한 SHA·모드·실행·코드를 artifact에 남긴다.

## 소스와 필수 CI

새 protected tag push, 원본 repository/workflow ID `378016847`, tag object·commit, dev 조상 관계, 원본 run ID·번호·attempt를 검증한다. 다른 repo/fork, 이동·재생성 태그, 새 dev 커밋 혼입과 더 최신 배포 실행이 있는 오래된 요청은 거절한다. checkout·AAB·게시 source는 모두 태그 commit과 같아야 한다. 합성 merge SHA나 PR 승인 기록을 만들지 않는다.

`tag_request`가 해당 태그의 전체 오프라인 배포 제어 회귀 검사를 통과한 뒤에만 `prepare`가 실행된다. 이 선행 검사는 필수다. 추가로 현재 dev의 active ruleset과 classic branch protection 요약에 설정된 required status checks를 읽어 **정확한 소스 SHA**의 최신 성공을 요구한다. 검증기를 호출하는 prepare/build/publish job의 기존 `GITHUB_TOKEN`에 `checks: read`, `statuses: read`만 명시하여 check-runs·commit-status 조회를 허용한다. 새 credential이나 쓰기 권한은 만들지 않는다. App이 지정된 검사는 App ID도 대조한다. classic 보호 요약에 검사 이름만 있고 App binding 정보가 없으면 같은 이름의 성공을 대신 인정하지 않고 배포를 중단한다. 이를 읽기 위해 새 administration 권한이나 credential을 추가하지 않는다. pending/실패/다른 SHA/다른 App/오래된 성공을 통과로 취급하지 않는다. dev 보호가 해제됐거나 설정·검사 조회가 실패하면 중단한다. 새 credential이나 쓰기 권한을 만들지 않으며 token으로 읽지 못하는 필수 검사는 생략하지 않는다.

필수 CI가 미완료면 빌드 전에 즉시 멈추고 검사 이름을 표시한다. CI 완료 후 아래의 안전한 재개를 사용할 수 있다. 배포용 봇 PR을 만들지 않으므로 그 PR의 `Approve workflows to run` 대기는 발생하지 않는다. 조직·저장소가 별도로 강제하는 Actions 보안 승인이나 environment 승인은 해제·우회하지 않는다.

현재 main과 같은 배포 인프라, 원본 태그·실행·필수 CI를 build/publish 및 Play commit 직전에도 다시 확인한다. 일반 PR용 **Android Release Checks**와 과거 merge receipt 검증기는 유지한다. 새 태그 경로에서 과거 PR/리뷰 검증기를 호출하지 않는다.

## 자동 versionCode와 빌드 전 재개

`versionCode = 3 + (GITHUB_RUN_NUMBER - 11)`로 **원래 태그 실행**의 번호를 사용한다. 실제 코드3 게시 실행 `38832543309`(run 11, `1.0.3`)을 기준으로 run12는 코드4, run13은 코드5다. 실패·빌드만 한 실행·수동 복구도 workflow 번호를 소비하므로 빈 번호가 생길 수 있다. 성공 게시 횟수를 세지 않는다. workflow 이름이 바뀌어도 ID가 같으면 같은 counter이며 ID 변경·초기화는 거절한다. Play 상한 `2100000000`, release name 50자, 게시 코드보다 큰지, 이미 사용한 코드와 겹치는지 검사한다.

새 direct-tag 경로의 원본 request artifact가 보존되어 있고 **이전 모든 attempt에서 build/publish가 단계 실행 없이 skipped**였을 때만 같은 실행의 **Re-run jobs → Re-run all jobs**를 허용한다. 이전 attempt별 GitHub 메타데이터와 전체 jobs를 읽고 원본 tag/source/run/counter를 대조한다. 원본 artifact는 덮어쓰지 않고 실행별 release context를 별도 artifact에 보존한다. 같은 run number를 유지하므로 versionCode도 그대로다.

build 또는 publish가 한 번이라도 시작됐거나 상태·기록이 누락/불명확한 경우, 다른 실행이 추가된 경우, 기존 PR/merge 방식인 경우에는 코드 재사용을 거절한다. 과거 시도에서 빌드했는데 마지막 시도만 skipped인 경우도 거절한다. 이때 새 버전 태그를 사용한다. 업로드 결과가 불명확하면 Console의 수용 여부를 먼저 확인한다. 자동 재게시나 증가 재시도는 하지 않는다.

기존 `.github/android-release.json`의 실제 게시·과거 미게시 build 기록은 보존한다. 후보 코드나 빌드 성공을 게시 완료로 기록하지 않는다. legacy `214011987`은 마지막 확인 당시 비활성·임시·출시 0개였으며 번호 자체를 재사용하지 않는다. 비활성 큰 초안이 모든 작은 코드의 최솟값을 정하지 않는다.

## AAB·Play 안전장치

서명과 업로드 keystore 대조, `com.teamdoodle.doodledefense`, API 36+, ARM64, 비개발 AAB, 실제 manifest·16KB alignment·hash 검증을 유지한다. Play preflight는 candidate code가 bundles/APKs/모든 트랙에서 미사용이고 현재 트랙 코드보다 큰지 검사한다. internal의 기존 완료 출시 한 개와 실제 코드·메모·대상 국가·이름·우선순위가 없는 빈 초안 최대 한 개만 허용한다. 다른 작업 중 초안은 덮어쓰지 않는다.

internal 이외의 트랙이 달라지면 commit하지 않는다. 업로드 후 commit 직전에도 원본 tag·source·run·필수 CI를 재확인한다. API commit은 `ERROR_IF_IN_REVIEW`를 사용하며 결과 불명확 시 재시도하지 않는다. 최초 원격 검증·인증 전에 검증한 artifact의 최소 receipt를 기록하고 실패 때도 보존한다. 원시 Play 응답·인증 정보는 남기지 않는다. 기존 서비스 계정과 서명 secret만 사용한다.

## 기존 수동 복구와 APK

**Actions → Deploy Google Play Internal → Run workflow**에서 `build_only=false`인 복구 모드는 고정 원본 code2 전용이다. main에서 `release_version=1.0.2`, `recover_original_code2=true`, `publish_to_internal=true`, `acknowledge_edit_invalidation=true`를 명시한다. 원본 artifact/hash, 게시 범위, edit 무효화 확인과 재실행 거절을 유지한다. 일반 새 출시는 위의 태그 경로를 사용한다.

**Release APK**는 기존 수동 `workflow_dispatch`와 `--apk-build` 검증을 유지하며 `v*` push를 구독하지 않는다. 기존 장부와 일치하는 후보 태그를 CLI `--ref`로 지정한다. branch/fork 요청은 실패한다. APK 경로와 자동 AAB 코드 배정을 구분하며 과거 APK 재빌드를 Play 재업로드 승인으로 취급하지 않는다. Dev APK 정책도 유지한다.

근거: [GitHub 재실행의 원본 SHA/ref 유지](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs), [브랜치 및 보호 요약 조회](https://docs.github.com/en/rest/branches/branches#get-a-branch), [active rules 조회](https://docs.github.com/en/rest/repos/rules#get-rules-for-a-branch), [Android 버전 규칙](https://developer.android.com/studio/publish/versioning).
