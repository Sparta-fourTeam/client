# Android 태그 출시

## 사용법

배포 설정과 사전 확인이 완료되면 검토한 **dev 커밋 하나에 새 `vMAJOR.MINOR.PATCH` 태그를 push**한다. `v1.0.3`이면 표시 버전은 `1.0.3`이다. JSON 후보 수정이나 Actions 버전 입력은 필요 없다.

```sh
git tag -a v1.0.3 <검토된-dev-커밋-SHA> -m 'Release 1.0.3'
git push origin refs/tags/v1.0.3
```

태그 push는 **고정 출시 PR 생성 → 최신 source SHA Codex 검토 → GitHub 보호 조건에 따른 merge commit 병합 → 실제 merge SHA 빌드·검증 → internal 게시**를 승인하는 신호다. **Actions → Deploy Google Play Internal**에서 실행 상태와 Summary를 확인한다. 새 dev 커밋이 들어와도 태그의 소스는 바뀌지 않는다. 일반 기능 PR이나 태그 없는 main 변경은 이 흐름을 실행하지 않는다.

사람이 출시 PR을 생성·병합하는 단계는 없다. 단, 필요한 설정이 꺼졌거나 검토/필수 검사/충돌이 해결되지 않으면 자동으로 중단한다. 출시 의사가 없으면 태그를 push하지 않는다. 현재 main의 승인 수 0을 기준으로 동작하며, 별도의 사람 승인 1명 규칙을 추가하지 않는다. 향후 GitHub 보호 조건이 강화되면 그 조건을 충족할 때까지 대기하거나 실패한다.

## 권한과 실행 구조

기본 workflow 권한은 `contents: read`, `actions: read`이다. `integrate` job에만 `contents: write`, `pull-requests: write`를 부여해 `release/v1.0.3` 브랜치를 태그 SHA 그대로 만들고 main PR을 생성·병합한다. 이 job은 현재 main의 배포 스크립트를 사용하며 Unity/Play secrets를 받지 않는다. build/publish는 콘텐츠·Actions·PR 읽기 권한만 가진다. 새 token, `actions: write`, 자기 PR 승인, 리뷰 dismiss/해결 조작, 보호 우회, 직접 main push는 사용하지 않는다.

저장소 **Settings → Actions → General → Workflow permissions → Allow GitHub Actions to create and approve pull requests**가 켜져 있어야 한다. GitHub UI는 생성과 승인을 묶어 허용하므로 이 설정은 저장소 전체에서 PR 쓰기 권한을 가진 workflow에 영향을 준다. 출시 코드는 승인 API를 호출하지 않는다. 기본 token 권한은 read로 유지한다. 조직 정책이 repo 설정을 막으면 우회하지 않고 중단한다. repository auto-merge 기능은 필요 없다.

`GITHUB_TOKEN`으로 병합한 closed/push 이벤트는 후속 workflow를 시작하지 않을 수 있다. 따라서 **사용자가 시작한 같은 태그 workflow 실행**에서 병합 결과 SHA를 받고 이어서 빌드/게시한다. immutable `GITHUB_SHA`는 원래 태그 source로 유지하며 실제 checkout·AAB 증거·게시 검증에는 별도 `BUILD_SHA`를 사용한다. PR opened/closed workflow가 후속 실행된다고 가정하지 않는다.

## 검토·필수 검사 gate

PR head는 태그 commit과 같고 base는 요청 당시 main이어야 한다. `@codex review`를 요청한 뒤 최대 20분 동안 다음을 확인한다.

- 실제 Codex GitHub 봇 계정 ID `199175422`/`chatgpt-codex-connector[bot]`가 최신 head SHA를 검토했는가. 이전 SHA의 리뷰나 이름만 비슷한 계정, PENDING/DISMISSED/CHANGES_REQUESTED 상태는 통과하지 않는다.
- Codex 리뷰가 submitted 상태의 COMMENTED 또는 APPROVED로 완료됐는가. **COMMENTED는 검토 완료이고 GitHub 승인 대체가 아니다.** 댓글/이모지/리뷰 요청만으로 통과시키지 않는다. 최상위 리뷰 본문에 `[P0]`~`[P3]` 지적이 있으면 새 검토가 필요하다.
- 모든 페이지의 review thread에 미해결 항목이 없는가. 다른 리뷰어의 최신 CHANGES_REQUESTED나 GitHub REVIEW_REQUIRED도 차단한다. 자동으로 댓글을 해결하거나 리뷰를 지우지 않는다.
- GitHub mergeability가 확인되고 필수 조건이 병합을 허용하는가. BLOCKED/불명확 상태는 대기한다. 필수가 아닌 현재 태그 실행 자체가 진행 중이라 UNSTABLE일 수 있지만, 마지막 일반 PR merge API가 **실제 required CI/review/ruleset 조건을 강제**한다. API가 거절하거나 결과가 불명확하면 단 한 번의 요청 뒤 중단한다. --admin이나 우회를 사용하지 않는다.

기존 Codex GitHub 연동이 repo를 검토할 수 있어야 한다. 서비스 설정·봇 PR 지원·실제 리뷰 응답은 오프라인 테스트로 확인할 수 없다. 응답이 없거나 검토가 끝나지 않으면 timeout으로 중단하며 검토가 없다는 사실을 통과로 취급하지 않는다. 별도 OpenAI API key를 새로 만들지 않는다.

## 첫 사용 전 조건

1. 이 배포 코드와 main release profile/CiBuild/검증을 main 및 태그 source의 dev에 검토·동기화한다. 기존 dev에는 예전 `Release APK`의 `v*` 자동 트리거와 오래된 Play 설정이 있으므로 **동기화 전에는 출시 태그를 push하지 않는다.** 태그 source의 `.github/`, `docs/android-release.md`, `Assets/Settings/Build Profiles/Android_Release.asset`, `Assets/_Project/Scripts/Editor/CiBuild.cs`가 현재 main과 다르면 거절한다. 충돌은 dev에서 먼저 검토·해결하며 Unity YAML은 Unity에서 저장한다.
2. `v*` 태그에 active ruleset으로 **updates와 deletion 제한, bypass 없음**을 적용한다. creation까지 제한해 새 태그 생성을 막지 않는다. 태그 요청은 `GITHUB_REF_PROTECTED=true`가 아니면 중단한다. 그 변수만으로 모든 세부 조건을 보증하지 않으므로 실제 규칙도 담당자가 확인한다.
3. 위 Actions PR 생성 설정과 기존 Codex 연동을 확인한다. main의 기존 PR 보호·필수 검사·차단 리뷰는 유지한다.
4. **`300000000`이 실제 Play 전체 사용 코드보다 크다는 점은 아직 확인하지 않았다.** 알려진 legacy `214011987`, 작은 사용 코드와 과거 code4 후보보다 큰 값일 뿐이다. 담당자가 Console의 모든 트랙/초안 최고 코드가 첫 자동 코드보다 작은지 확인해야 한다. 더 큰 코드가 있으면 범위를 재검토하고 실행하지 않는다. 다른 업로더가 이 범위를 쓰지 않게 관리한다.

## 정확한 소스·추적과 실패 처리

태그 사전 검증은 원본 tag object·tag commit·main base·tag run을 artifact에 기록한다. main의 배포 코드가 source에서 되돌려지지 않는지와 충돌 없는 예상 merge tree를 검사한 뒤에만 repo에 쓰기 작업을 시작한다. 기존 release 브랜치/PR이 다른 SHA를 가리키면 overwrite/force하지 않는다. 더 최신 dev, 다른 repo/fork, draft/닫힌 PR, 이동/재사용 태그를 거절한다.

병합 API에 정확한 head SHA와 `merge_method=merge`를 전달한다. 반환된 실제 merge의 부모가 `[원본 main base, tag commit]`이고 tree가 예상 Git merge와 같아야 한다. build/publish 때도 현재 main·원격 태그·PR merge·Codex 최신 SHA 검토·미해결 리뷰를 다시 검사한다. 다른 main 커밋이 끼거나 squash/rebase/추가 head 커밋이면 중단한다. **tag commit과 build merge SHA는 다를 수 있으며** 두 SHA, tag object, base, PR 번호, run, tree, Codex 리뷰 ID/SHA를 함께 남긴다.

PR 생성 후 검토 대기, 병합 요청 직전/응답, 검증 완료 상태를 비민감 attempt receipt에 기록하며 실패 때도 보존한다. 병합 응답이 불명확하면 재시도하지 않는다. 실제 main에는 병합됐더라도 후속 검증이 실패하면 배포하지 않는다. PR과 receipt를 확인한 뒤 다음 출시를 결정한다. workflow concurrency는 FIFO가 아니므로 출시를 하나씩 진행한다. 새 실행이 오래된 pending 실행을 취소하거나 거절할 수 있다.

## 자동 versionCode와 재시도

`versionCode = 300000000 + GITHUB_RUN_NUMBER`로 **원래 태그 실행**의 번호를 사용한다. workflow ID `378016847`에 결합되어 있으며 실패·복구 실행도 번호를 소비하므로 빈칸이 생긴다. run ID 자체는 코드로 쓰지 않는다. repository·workflow/run ID·번호·attempt·태그 event/source를 API로 확인하고 전체 실행 이력에 더 큰 번호가 있으면 오래된 실행을 거절한다. Play 상한 `2100000000`, release name 50자와 checkout/main 장부의 모든 코드보다 큰지도 확인한다. 병합 전 후보 코드 검사와 게시 전 원본 코드 대조를 모두 수행한다.

**Re-run jobs / Re-run failed jobs는 거절한다.** 업로드 전 Unity/네트워크 실패도 동일하다. 새 버전 태그가 필요하며 main에 병합된 실패 출시라면 변경된 main을 dev에 통합·검토해야 한다. 전체 빌드를 다시 하므로 시간/실행 비용이 다시 들고 기존 AAB만 재게시하지 않는다. 업로드 결과가 불명확하면 Console의 수용 여부부터 확인한다. workflow 이름은 같은 ID이면 바꿀 수 있지만 파일 재생성 등으로 ID가 바뀌면 범위 이관 검토가 필요하다.

## 기존 안전장치와 수동 복구

서명, `com.teamdoodle.doodledefense`, API 36+, ARM64, 비개발 AAB, 실제 manifest·서명·16KB alignment·hash 검증을 유지한다. 기존 `.github/android-release.json` 기록은 보존하고 자동 후보/빌드 성공을 Play 업로드 완료로 기록하지 않는다. 다음 자동 실행마다 JSON을 편집할 필요는 없다.

**Actions → Deploy Google Play Internal → Run workflow**는 고정 원본 code2 복구 전용이다. main에서 `release_version=1.0.2`, `recover_original_code2=true`, `publish_to_internal=true`, `acknowledge_edit_invalidation=true`를 명시해야 한다. 원본 artifact/hash·게시 범위·edit 무효화·재실행 검증을 유지하며 태그 경로와 분리한다.

main의 **Release APK**는 기존 수동 `workflow_dispatch`와 `--apk-build` 검증을 유지하며 `v*` push를 구독하지 않는다. Dev APK도 기존 정책을 유지한다. 과거 APK 재빌드를 Play 재업로드 승인으로 취급하지 않는다.

근거: [GitHub token/event 제한](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow), [PR 생성/병합 API](https://docs.github.com/en/rest/pulls/pulls), [Codex 리뷰](https://learn.chatgpt.com/docs/third-party/github), [Android 버전 규칙](https://developer.android.com/studio/publish/versioning).
