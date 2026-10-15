# Android 릴리스 번호

## 내부 테스트 실행

변경이 배포 대상 브랜치에 반영된 뒤 GitHub 저장소의 **Actions → Deploy Google Play Internal → Run workflow**를 연다.

1. **Use workflow from**에서 배포할 소스 브랜치를 고른다. main의 검증된 release 설정을 포함하는 브랜치여야 한다. 과거 `build/aab-dev-*` 브랜치는 당시 소스의 후보이며 최신 dev가 아니다.
2. **release_version**에 `1.0.3`처럼 표시 버전을 입력한다. `v`는 붙이지 않는다. `.github/android-release.json`이나 Build Profile의 버전 값을 매번 편집할 필요가 없다.
3. 빌드와 검증만 할 때는 **publish_to_internal**을 끈다(기본값). 별도로 승인된 내부 테스트 게시를 실행할 때만 켠다.
4. 일반 빌드에서는 **recover_original_code2**, **acknowledge_edit_invalidation**을 모두 끈다. 두 항목은 고정 원본 code2 복구 전용이다. 복구는 기존대로 main에서 표시 버전 `1.0.2`와 두 확인 항목 및 게시 선택을 요구한다.
5. **Run workflow**를 누른다. 실행 Summary에 표시 버전·자동 versionCode·source SHA가 나오고, 검증된 AAB와 증거 파일이 artifact에 보관된다. 빌드 성공만으로 Play 게시가 완료된 것은 아니다.

이 workflow는 수동 실행만 허용한다. 서명, package `com.teamdoodle.doodledefense`, API 36+, ARM64, 비개발 AAB, 실제 manifest·서명·16KB alignment 검증과 게시 직전 artifact 검증을 유지한다. 태그를 자동으로 만들거나 main을 병합하지 않는다.

## 자동 versionCode와 충돌 방지

Play AAB는 `versionCode = 300000000 + GITHUB_RUN_NUMBER`를 사용한다. 번호는 표시 버전과 독립적이다. 빌드 전용 실행·실패·code2 복구도 workflow 실행 번호를 소비하므로 번호 사이에 빈칸이 생길 수 있다. 게시 직전에는 같은 run으로 계산한 코드가 빌드 artifact 코드와 일치하는지 재검사한다.

- 고정 범위의 시작값은 기존 legacy 초안 `214011987`, 실제 사용된 작은 코드와 과거 code4 후보보다 크다. checkout과 최신 `origin/main` 장부의 후보·`playUploads`·`legacyDraftUploads` 코드도 모두 넘어야 한다. 기존 기록은 삭제하거나 자동으로 업로드 완료 처리하지 않는다.
- GitHub API로 repository `Sparta-fourTeam/client`, workflow ID `378016847`, run ID·번호·attempt·source SHA·수동 실행 여부를 검증한다. API 실패나 현재 main 장부 누락 시 빌드/게시를 중단한다. 이미 사용 중인 `actions: read`와 `github.token`만 사용하며 새 credential, 권한, repository variable은 필요 없다.
- 표시용 workflow 이름을 바꾸어도 같은 ID의 카운터를 사용한다. 파일 이동·삭제 후 재생성 등으로 workflow ID가 바뀌면 중단한다. ID나 시작값을 임의로 바꾸면 안 된다. 이관 시 이전에 할당한 모든 코드보다 높은 새 범위를 검토해야 한다.
- **Re-run jobs / Re-run failed jobs는 거절한다.** 같은 run은 동일 번호를 갖기 때문에 업로드 성공 여부가 불명확한 재시도에서 코드를 재사용하지 않는다. 다시 시도하거나 빌드 전용 결과를 게시하려면 표시 버전을 입력해 **Run workflow**로 새 실행을 만든다. 새 AAB에는 새 코드가 붙는다.
- Unity 빌드나 네트워크 단계에서 실패하여 아직 업로드하지 않은 실행도 Re-run으로 복구할 수 없다. 같은 소스·표시 버전으로 새 실행을 만들어 처음부터 빌드해야 하므로 빌드 시간과 실행 비용이 다시 든다. 실패/빌드 전용 실행의 번호는 건너뛰며, 기존 artifact만 재사용해 게시하는 기능은 제공하지 않는다. 업로드 결과가 불명확하면 Console에서 수용 여부를 확인한 뒤 다음 실행을 결정한다.
- `android-release-build` concurrency 그룹을 이름·브랜치와 무관하게 유지한다. GitHub의 실행 순서는 FIFO가 아니므로 전체 workflow 실행 이력을 페이지 끝까지 읽고 더 큰 실행 번호가 이미 있으면 오래된 대기 실행을 중단한다. 동시 요청 시 pending 실행이 취소될 수도 있다. 새 실행으로 다시 요청한다.
- Google Play 상한 `2100000000`을 넘으면 중단한다. 현재 공식과 상한으로 허용되는 최대 run number는 `1800000000`이다. 표시 버전 형식과 Play release name의 50자 제한도 빌드 전에 검사한다.

[GitHub 변수 규칙](https://docs.github.com/en/actions/reference/workflows-and-actions/variables)에 따르면 run number는 workflow별로 증가하고 재실행에서는 바뀌지 않는다. run ID 자체는 이미 Play 코드 상한보다 클 수 있어 코드로 쓰지 않는다. [GitHub concurrency 규칙](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)은 실행 순서를 보장하지 않는다. [Android 버전 규칙](https://developer.android.com/studio/publish/versioning)은 새 버전의 더 큰 코드, 재사용 금지와 상한을 규정한다.

**`300000000` 오프셋이 실제 Play 전체 사용 코드보다 크다는 점은 아직 확인하지 않았다.** 위 비교는 저장소에 알려진 기록만을 근거로 하며 전체 Play 코드와의 충돌 방지를 보장하지 않는다. 이 장부는 Play 서버 조회를 대체하지 않는다. **첫 사용 전에** Console의 모든 트랙·초안을 포함한 실제 사용 코드가 첫 자동 코드보다 작은지 담당자가 확인해야 한다. 그보다 큰 사용 코드가 있으면 이 설정으로 실행하지 말고 범위를 재검토한다. 다른 workflow나 수동 업로드가 이 자동 범위의 코드를 발급하면 안 된다. 새 코드가 서버에서 거절되면 기록이나 태그를 덮어쓰지 말고 사유를 확인한다. 기존 Play 업로드 credential의 동작과 실제 AAB 빌드/업로드는 오프라인 검사만으로 확인할 수 없다.

## 기존 장부와 Release APK

`.github/android-release.json`의 기존 `versionName`·`versionCode`는 수동 후보/과거 APK 검증 경로를 위해 보존한다. 자동 Play AAB의 입력 버전과 코드를 정하지 않는다. `playUploads`는 Play에서 업로드 수용이 실제 확인된 기록이고 `legacyDraftUploads`는 이전 high-code 초안 기록이다. 새 자동 후보를 빌드한 사실만으로 장부에 행을 추가하지 않는다. 실제 업로드 기록은 증거를 확인한 뒤 별도 관리할 수 있으며 다음 자동 실행의 선행 조건은 아니다.

**Release APK**의 기존 태그 기반 수동 경로와 `--apk-build` 검증은 그대로 유지한다. 정확히 기록된 과거 code/versionName 재빌드만 예외적으로 허용하며, 이를 Play 재업로드 허가로 해석하지 않는다. Dev APK도 기존 개발용 번호 정책을 유지한다. 자동 Play AAB와 같은 표시 버전의 APK가 필요하면 AAB 증거의 코드와 소스를 확인하여 별도 작업으로 처리한다.
