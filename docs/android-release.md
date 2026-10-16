# Android 태그 출시

## 사용법

배포 설정과 사전 확인이 완료되면 검토한 **dev 커밋 하나에 새 `vMAJOR.MINOR.PATCH` 태그를 push**한다. `v1.0.4`이면 표시 버전은 `1.0.4`이다. JSON 후보 수정이나 Actions 버전 입력은 필요 없다.

```sh
git tag -a v1.0.4 <검토된-dev-커밋-SHA> -m 'Release 1.0.4'
git push origin refs/tags/v1.0.4
```

태그 push는 **고정 출시 PR 생성 → 최신 source SHA Codex 검토 → GitHub 보호 조건에 따른 merge commit 병합 → 실제 merge SHA 빌드·검증 → Console 확인 → internal 게시**를 승인하는 신호다. **Actions → Deploy Google Play Internal**에서 실행 상태와 Summary를 확인한다. 새 dev 커밋이 들어와도 태그의 소스는 바뀌지 않는다. 일반 기능 PR이나 태그 없는 main 변경은 이 흐름을 실행하지 않는다.

빌드가 끝나면 **Actions → Deploy Google Play Internal → 해당 실행 → publish → Prepare receipt and request clean Console confirmation → 로그**를 확인한다. 태그를 push한 사용자가 **Play Console → 이 게임 → 게시 개요**에서 다른 검토 전송 대기·검토 중·게시 대기 변경이 없는지 확인하고, 다른 Console/API 작성 작업을 실행 종료까지 멈춘다. 읽기는 가능하다. 그 로그의 `Play-Console-Clean: ...` 한 줄을 **그 출시 PR에 새 댓글로 그대로** 붙인다. 확인은 원래 태그 작성자의 GitHub ID와 run·merge SHA·versionCode·AAB hash에 고정되며, 빌드 후 요청된 확인만 최대 10분 기다린다. 댓글 생성 후 10분 동안만 유효하며 삭제/수정으로 철회할 수 있다. 확인이 없거나 만료/철회되면 edit/upload/commit을 중단한다. 같은 내용은 작업이 끝난 뒤 Summary에도 남지만, 실행 중에는 준비 단계의 실시간 로그를 사용한다.

**이 Console 확인은 매 게시마다 필요한 운영 단계이므로 태그만으로 완전 무인 게시되지는 않는다.** Google은 API edit commit이 Console의 미전송 변경도 함께 제출할 수 있다고 설명한다. 트랙 비교와 `ERROR_IF_IN_REVIEW`만으로 검토 전송 대기 변경 전체가 없음을 보증하지 않는다. 새 credential/권한을 만들거나 이 위험을 묵인하는 대신 현재 구현은 위 확인을 요구한다. [Google 동시 작업 주의사항](https://developers.google.com/android-publisher/concurrency-considerations).


출시 PR 생성·병합은 자동화한다. **GitHub가 PR workflow 실행 승인을 요구하면 저장소 write 권한을 가진 사람이 PR 병합 영역의 `Approve workflows to run`을 눌러야 한다.** 이 경우 태그만으로 무인 완료되지 않는다. 새 token이나 `actions: write`로 승인 단계를 우회하지 않는다. 또한  필요한 설정이 꺼졌거나 검토/필수 검사/충돌이 해결되지 않으면 자동으로 중단한다. 출시 의사가 없으면 태그를 push하지 않는다. 현재 main의 승인 수 0을 기준으로 동작하며, 별도의 사람 승인 1명 규칙을 추가하지 않는다. 향후 GitHub 보호 조건이 강화되면 그 조건을 충족할 때까지 대기하거나 실패한다.

## 권한과 실행 구조

기본 workflow 권한은 `contents: read`, `actions: read`이다. `integrate` job에만 `contents: write`, `pull-requests: write`를 부여해 `release/v1.0.4` 브랜치를 태그 SHA 그대로 만들고 main PR을 생성·병합한다. 이 job은 현재 main의 배포 스크립트를 사용하며 Unity/Play secrets를 받지 않는다. build/publish는 콘텐츠·Actions·PR 읽기 권한만 가진다. 새 token, `actions: write`, 자기 PR 승인, 리뷰 dismiss/해결 조작, 보호 우회, 직접 main push는 사용하지 않는다.

저장소 **Settings → Actions → General → Workflow permissions → Allow GitHub Actions to create and approve pull requests**가 켜져 있어야 한다. GitHub UI는 생성과 승인을 묶어 허용하므로 이 설정은 저장소 전체에서 PR 쓰기 권한을 가진 workflow에 영향을 준다. 출시 코드는 승인 API를 호출하지 않는다. 기본 token 권한은 read로 유지한다. 조직 정책이 repo 설정을 막으면 우회하지 않고 중단한다. repository auto-merge 기능은 필요 없다.

`GITHUB_TOKEN`으로 병합한 closed/push 이벤트는 후속 workflow를 시작하지 않을 수 있다. 따라서 **사용자가 시작한 같은 태그 workflow 실행**에서 병합 결과 SHA를 받고 이어서 빌드/게시한다. immutable `GITHUB_SHA`는 원래 태그 source로 유지하며 실제 checkout·AAB 증거·게시 검증에는 별도 `BUILD_SHA`를 사용한다. closed/push 후속 실행에 의존하지 않는다. `GITHUB_TOKEN`이 만든 PR의 opened/synchronize/reopened Actions 실행은 GitHub 정책에 따라 승인 대기 상태가 될 수 있다. 대기를 감지하면 PR 링크와 실행 승인 메뉴를 Summary에 기록하고, 승인된 CI와 리뷰가 완료되기 전에는 병합하지 않는다. 20분 안에 완료되지 않으면 배포 없이 중단한다. [GitHub 공식 이벤트 동작](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow)을 참고한다.

## 검토·필수 검사 gate

PR head는 태그 commit과 같고 base는 요청 당시 main이어야 한다. `@codex review`를 요청한 뒤 최대 20분 동안 다음을 확인한다.

- 실제 Codex GitHub 봇 계정 ID `199175422`/`chatgpt-codex-connector[bot]`가 최신 head SHA를 검토했는가. 이전 SHA의 리뷰나 이름만 비슷한 계정, PENDING/DISMISSED/CHANGES_REQUESTED 상태는 통과하지 않는다.
- Codex 리뷰가 submitted 상태의 COMMENTED 또는 APPROVED로 완료됐는가. 지적 없는 리뷰가 정식 review 대신 완료 Summary만 남기는 경우에는 고정 봇 ID/login/type, 최종 Completed 표, 그 표의 commit 약어를 GitHub가 해석한 정확한 전체 source SHA를 검증한다. 진행 중 상태나 일반 댓글·이모지·리뷰 요청만으로 통과시키지 않는다. **검토 완료는 GitHub 승인 대체가 아니다.** 최상위 리뷰 본문에 `[P0]`~`[P3]` 지적이 있으면 새 검토가 필요하다.
- 모든 페이지의 review thread에 미해결 항목이 없는가. 다른 리뷰어의 최신 CHANGES_REQUESTED나 GitHub REVIEW_REQUIRED도 차단한다. 자동으로 댓글을 해결하거나 리뷰를 지우지 않는다.
- GitHub mergeability가 확인되고 필수 조건이 병합을 허용하는가. BLOCKED/불명확 상태는 대기한다. 필수가 아닌 현재 태그 실행 자체가 진행 중이라 UNSTABLE일 수 있지만, 마지막 일반 PR merge API가 **실제 required CI/review/ruleset 조건을 강제**한다. API가 거절하거나 결과가 불명확하면 단 한 번의 요청 뒤 중단한다. --admin이나 우회를 사용하지 않는다.

기존 Codex GitHub 연동이 repo를 검토할 수 있어야 한다. 서비스 설정·봇 PR 지원·실제 리뷰 응답은 오프라인 테스트로 확인할 수 없다. 응답이 없거나 검토가 끝나지 않으면 timeout으로 중단하며 검토가 없다는 사실을 통과로 취급하지 않는다. 별도 OpenAI API key를 새로 만들지 않는다.

PR의 `Android Release Checks / Android release controls`는 네트워크·Play·Unity 빌드 없이 배포 제어 회귀 검사를 실행한다. 실제 required check와 병합 보호는 GitHub 설정을 따른다.

## 첫 사용 전 조건

1. 이 배포 코드와 main release profile/CiBuild/검증을 main 및 태그 source의 dev에 검토·동기화한다. 기존 dev에는 예전 `Release APK`의 `v*` 자동 트리거와 오래된 Play 설정이 있으므로 **동기화 전에는 출시 태그를 push하지 않는다.** 태그 source의 `.github/`, `docs/android-release.md`, `Assets/Settings/Build Profiles/Android_Release.asset`, `Assets/_Project/Scripts/Editor/CiBuild.cs`가 현재 main과 다르면 거절한다. 충돌은 dev에서 먼저 검토·해결하며 Unity YAML은 Unity에서 저장한다.
2. `v*` 태그에 active ruleset으로 **updates와 deletion 제한, bypass 없음**을 적용한다. creation까지 제한해 새 태그 생성을 막지 않는다. 태그 요청은 `GITHUB_REF_PROTECTED=true`가 아니면 중단한다. 그 변수만으로 모든 세부 조건을 보증하지 않으므로 실제 규칙도 담당자가 확인한다.
3. 위 Actions PR 생성 설정과 기존 Codex 연동을 확인한다. main의 기존 PR 보호·필수 검사·차단 리뷰는 유지한다.
4. 게시 기준은 실제 내부 테스트 `1.0.3(3)`이다. 기존 workflow ID `378016847`의 실행 `38832543309`(run number `11`)에서 게시됐으며, 이 확인 이력을 main/dev 장부에 포함한다. 과거 미게시 code3/code4 빌드는 사용 코드가 아니다. legacy `214011987`은 마지막 Console 확인 당시 비활성·임시·출시 0개였으며 그 번호 자체는 재사용하지 않는다. 다른 업로더를 병행하지 않는다. 자동 게시 직전에 모든 트랙과 번들/APK 사용 코드를 다시 검사하며 다른 업로드와 충돌하면 증가 재시도 없이 중단한다.

## 정확한 소스·추적과 실패 처리

태그 사전 검증은 원본 tag object·tag commit·main base·tag run을 artifact에 기록한다. main의 배포 코드가 source에서 되돌려지지 않는지와 충돌 없는 예상 merge tree를 검사한 뒤에만 repo에 쓰기 작업을 시작한다. 기존 release 브랜치/PR이 다른 SHA를 가리키면 overwrite/force하지 않는다. 더 최신 dev, 다른 repo/fork, draft/닫힌 PR, 이동/재사용 태그를 거절한다.

병합 API에 정확한 head SHA와 `merge_method=merge`를 전달한다. 반환된 실제 merge의 부모가 `[원본 main base, tag commit]`이고 tree가 예상 Git merge와 같아야 한다. build/publish 때도 현재 main·원격 태그·PR merge·Codex 최신 SHA 검토·미해결 리뷰를 다시 검사한다. 다른 main 커밋이 끼거나 squash/rebase/추가 head 커밋이면 중단한다. **tag commit과 build merge SHA는 다를 수 있으며** 두 SHA, tag object, base, PR 번호, run, tree, Codex 리뷰 ID/SHA를 함께 남긴다.

PR 생성 후 검토 대기, 병합 요청 직전/응답, 검증 완료 상태를 비민감 attempt receipt에 기록하며 실패 때도 보존한다. 병합 응답이 불명확하면 재시도하지 않는다. 실제 main에는 병합됐더라도 후속 검증이 실패하면 배포하지 않는다. PR과 receipt를 확인한 뒤 다음 출시를 결정한다. workflow concurrency는 FIFO가 아니므로 출시를 하나씩 진행한다. 새 실행이 오래된 pending 실행을 취소하거나 거절할 수 있다.

## 자동 versionCode와 재시도

`versionCode = 3 + (GITHUB_RUN_NUMBER - 11)`로 **원래 태그 실행**의 번호를 사용한다. 실제 코드3 게시 실행을 기준점으로 삼으므로 다음 실행이 12면 코드4, 13이면 코드5다. 실패·빌드만 한 실행·복구 실행도 workflow 번호를 소비하므로 빈칸은 생길 수 있다. 성공 게시 횟수만 세는 방식은 아니다. 과거 미게시 후보나 비활성 큰 초안을 다음 번호의 최솟값으로 쓰지 않는다.

workflow ID `378016847`과 repository에 결합되어 있다. workflow 이름만 바꿔 ID가 유지되면 충돌하지 않으며 ID 변경·카운터 초기화·기준점 이하 실행은 거절한다. run ID 자체는 코드로 쓰지 않는다. workflow/run ID·번호·attempt·원본 태그 event/source를 API로 확인하고 전체 실행 이력에 더 큰 번호가 있으면 오래된 실행을 거절한다. Play 상한 `2100000000`, release name 50자, 이미 게시된 코드보다 큰지, 업로드됐던 번호와 겹치지 않는지 검사한다. 병합 전 후보 코드 검사와 게시 전 원본 코드 대조를 모두 수행한다. 자동 업로드 증거에는 `workflow-run-offset-v1`과 workflow ID·run number를 남긴다. 장부에 나중에 확인된 게시를 추가할 때 `versionCodeAllocation`에 이 증거를 포함하면 자동 실행의 정상적인 빈 번호와 과거 장부 누락을 구분할 수 있다.

**Re-run jobs / Re-run failed jobs는 거절한다.** 업로드 전 Unity/네트워크 실패도 동일하다. 새 버전 태그가 필요하며 main에 병합된 실패 출시라면 변경된 main을 dev에 통합·검토해야 한다. 전체 빌드를 다시 하므로 시간/실행 비용이 다시 들고 기존 AAB만 재게시하지 않는다. 업로드 결과가 불명확하면 Console의 수용 여부부터 확인한다. workflow 이름은 같은 ID이면 바꿀 수 있지만 파일 재생성 등으로 ID가 바뀌면 새 기준점과 번호 충돌을 검토해야 한다.

## 기존 안전장치와 수동 복구

서명, `com.teamdoodle.doodledefense`, API 36+, ARM64, 비개발 AAB, 실제 manifest·서명·16KB alignment·hash 검증을 유지한다. 서명 인증서는 기존 설정의 업로드 keystore와 대조한다. 기존 `.github/android-release.json` 기록은 보존하고 자동 후보/빌드 성공을 Play 업로드 완료로 기록하지 않는다. 다음 자동 실행마다 JSON을 편집할 필요는 없다.

자동 게시도 기존 서비스 계정과 권한을 사용한다. Play preflight는 candidate code가 bundles/APKs/트랙 어디에도 사용되지 않았고 현재 모든 트랙의 코드보다 큰지 검사한다. internal의 기존 완료 출시 한 개와 실제 코드·메모·대상 국가·이름·우선순위가 없는 빈 초안 최대 한 개만 허용한다. 그 밖의 작업 중 초안은 덮어쓰지 않는다. internal 이외의 트랙이 달라지면 commit하지 않는다. 업로드 후 commit 직전에도 현재 main·원본 실행·태그·PR·최신 Codex 리뷰·CI를 다시 확인한다. 검증된 artifact의 식별자와 최초 단계는 인증·token refresh·최초 원격 게시 검증보다 먼저 receipt에 기록한다. 이후 실패/응답 불명확 때에도 최소 단계 receipt를 보존하며 원시 Play 응답이나 인증 정보는 남기지 않는다.

**Actions → Deploy Google Play Internal → Run workflow**는 고정 원본 code2 복구 전용이다. main에서 `release_version=1.0.2`, `recover_original_code2=true`, `publish_to_internal=true`, `acknowledge_edit_invalidation=true`를 명시해야 한다. 원본 artifact/hash·게시 범위·edit 무효화·재실행 검증을 유지하며 태그 경로와 분리한다.

main의 **Release APK**는 기존 수동 `workflow_dispatch`와 `--apk-build` 검증을 유지하며 `v*` push를 구독하지 않는다. Actions 웹 UI의 branch 선택으로 실행하지 않고, workflow와 장부가 이미 포함된 기존 후보 태그를 CLI의 `--ref`로 지정한다. branch/fork 요청은 성공처럼 건너뛰지 않고 실패한다. APK 경로는 기존 장부 후보와 일치해야 하며 자동 AAB 코드 배정 경로와 구분한다.

```sh
gh workflow run release-apk.yml --repo Sparta-fourTeam/client --ref <장부와-일치하는-기존-버전-태그>
```

이 명령은 실제 APK 빌드와 GitHub Release 게시를 시작하므로 그 작업을 의도할 때만 실행한다. Dev APK는 기존 정책을 유지한다. 과거 APK 재빌드를 Play 재업로드 승인으로 취급하지 않는다. code2 복구를 main 이외의 ref에서 요청하면 복구 범위 검증이 실행되어 artifact 다운로드·Play 접근 전에 명확히 실패한다.

근거: [GitHub token/event 제한](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow), [PR 생성/병합 API](https://docs.github.com/en/rest/pulls/pulls), [Codex 리뷰](https://learn.chatgpt.com/docs/third-party/github), [Android 버전 규칙](https://developer.android.com/studio/publish/versioning).
