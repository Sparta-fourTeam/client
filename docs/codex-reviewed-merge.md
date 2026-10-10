# Codex review 기반 이벤트 자동 머지

## 범위와 활성화

`codex-reviewed-merge.yml`이 기본 브랜치 `dev`에 머지된 이후 동작한다.
구현 PR은 draft로 제출하며, 자체 정책·워크플로 변경은 이 자동화가 머지하지 않는다.
현재 `dev` 규칙에는 필수 CI가 없지만, 이 정책은 별도의 `Codex merge policy tests`
성공을 항상 요구한다. Unity 게임 코드·씬·빌드 설정은 변경하지 않는다.

대상은 `Sparta-fourTeam/client` 안에서 Eunseok(사용자 ID `79277772`)이 만든
비-draft PR이며, base가 `dev`인 경우로 제한한다. fork와 `main`은 제외한다.
모델 호출, OpenAI API 키, AI 수정 루프, 자동 `@codex review` 댓글은 없다.
GitHub Actions 러너 사용량은 발생할 수 있지만 예약·상시 폴링은 없다.

## 머지 조건

다음 조건을 모두 재조회하여 두 번 검증한다.

1. Codex 앱 봇(ID `199175422`, `chatgpt-codex-connector[bot]`)의 리뷰 요약이
   유일하고, 모든 표시된 리뷰가 완료되어야 한다. 편집된 요약은 편집자도 같은
   Codex 봇이어야 한다. 알 수 없는 형식·누락된 요약·진행 중 리뷰는 보류한다.
2. 요약의 축약 커밋을 GitHub API로 해석한 전체 SHA가 현재 PR head와 같아야 한다.
   단순한 문자열 prefix 비교는 사용하지 않는다. 모호한 SHA/API 오류는 보류한다.
3. PR 본문 또는 최신 Eunseok `@codex review` 요청 댓글에 달린 Codex `+1` 반응
   생성 시간이 최신 리뷰 완료 시각 이후여야 한다. 요청 댓글은 작성자·편집자와
   본문도 검증한다. 다른 댓글의 반응, 이전 요청의 반응, 완료 이후 새 요청은 거절한다.
   이전 커밋의 👍, 다른 사용자의 👍, 진행 중 👀가 있으면 머지하지 않는다.
4. 미해결 인라인 리뷰 스레드와 유효한 `CHANGES_REQUESTED` 리뷰가 없어야 한다.
   일반 `COMMENTED` 리뷰나 다른 리뷰의 dismissal은 변경 요청을 해제하지 않는다.
   자연어 일반 댓글의 의미를 AI로 판별하지는 않는다. 차단 의견은 변경 요청 또는
   미해결 리뷰 스레드로 남긴다.
5. GitHub의 REST/GraphQL 머지 상태가 모두 clean이어야 한다. head와 현재
   test-merge 커밋의 모든 보이는 check/status가 성공 상태여야 한다
   (GitHub가 허용하는 neutral/skipped 포함). 정책 테스트 자체는 반드시 success여야
   하며, 실행의 PR 번호·head SHA·base SHA도 일치해야 한다.
6. base/head와 리뷰·반응 증거가 재검증 중 변하지 않아야 한다. merge API에는
   예상한 전체 head SHA를 전달하므로 head가 바뀌면 GitHub가 거절한다.

현재 저장소의 기존 방식에 맞춰 squash 머지한다. PR 본문·제목에서 명령이나
커밋 메시지를 만들지 않고, PR 번호를 포함한 고정 커밋 제목을 사용한다.
GitHub 보호 규칙을 우회하거나 관리자 권한을 쓰지 않는다. merge queue가 추가되면
별도 구현이 필요하므로 보류한다. 토큰·권한·규칙 설정은 변경하지 않는다.

## 이벤트와 재시도

- Codex PR 댓글 생성/수정
- PR 생성, 다시 열기, ready-for-review, 새 커밋 push
- `Codex merge policy tests` 또는 `Build Dev APK` 워크플로 완료
- 외부 check 완료 또는 commit status 변경
- Eunseok이 PR에 작성한 정확한 `/codex-merge` 댓글

PR 전체를 다시 평가하므로 서로 다른 PR의 이벤트가 몰려도 대기 이벤트 교체로
대상이 누락되지 않는다. 동시에 하나의 머지 작업만 실행한다.
요약 댓글 수정 직후 👍가 약간 늦게 생성되는 경우에만 최대 20초간 두 번 추가 확인한다.
각 실행은 최대 10분이며, API 오류 또는 결과가 불명확한 머지 요청을 자동 재시도하지 않는다.

GitHub Actions에는 반응 추가·스레드 해결 이벤트가 없다. 지연 반응이 20초 뒤에
도착하거나 스레드 해결 후 다른 이벤트가 없으면 `/codex-merge`로 재평가한다.
이는 리뷰 재요청이 아니다. PR을 처음 열거나 draft를 ready-for-review로 바꾸면
저장소의 Codex 자동 리뷰가 시작되므로 초기 요청을 중복으로 남기지 않는다.
그 이후 수정 push 후에는 사람이 `@codex review`를 남겨
최신 커밋 리뷰를 요청해야 한다. push만으로 재리뷰가 완료됐다고 간주하지 않는다.
GitHub Actions 봇의 `@codex review`가 실제 재리뷰를 유발하는지는 검증하지 않았으며
이 구현에서는 해당 댓글을 보내지 않는다.

## 보수적으로 멈추는 경우

- Codex 요약 형식 변경, 요약 미생성, 여러 요약, 봇이 아닌 편집자
- API 권한 부족·네트워크 실패·GraphQL 부분 오류·불완전한 pagination
- 같은 초에 완료된 리뷰와 반응: 완료 시각은 밀리초, 반응 시각은 초 단위이므로
  반응이 확실히 이후임을 입증하지 못하면 보류한다. 기존 👍를 계속 유지하는
  재리뷰도 새 반응 시각이 없으면 보류한다. 필요하면 수동 검증·머지한다.
- base가 바뀌어 CI 실행 base와 현재 base가 다른 경우: 브랜치를 갱신하고 CI와
  `@codex review`를 다시 실행한다.
- `.github/scripts/`와 `.github/workflows/` 전체 변경(새 파일 포함): 수동 리뷰와 머지가 필요하다.

GitHub merge API의 원자적 조건은 head SHA와 서버 보호 규칙이다. base 변경,
리뷰 수정, 반응 제거를 하나의 트랜잭션으로 잠글 API는 없으므로, 마지막 재조회와
머지 사이의 경쟁 조건을 완전히 제거했다고 주장하지 않는다. 엄격한 서버 측
필수 체크/브랜치 최신화 규칙은 저장소 관리자가 별도로 관리한다.

`GITHUB_TOKEN`으로 만든 merge의 push/closed 이벤트는 일반적으로 다른 Actions
워크플로를 새로 실행하지 않는다. 현재 dev APK는 `workflow_dispatch` 전용이며,
main 배포는 이 자동화 대상이 아니다. 향후 dev push 후속 작업에 의존한다면
이 제한을 먼저 검토해야 한다. 이 구현은 별도 자격 증명을 만들지 않는다.

## 보안 경계와 테스트

write 토큰 작업은 이벤트의 신뢰된 기본/base 커밋 `${{ github.sha }}`에서 정책만
가져온다. PR 코드를 checkout·실행하지 않고, 캐시나 PR 산출물도 실행하지 않는다.
별도의 read-only `pull_request` 워크플로에서 오프라인 회귀 테스트만 실행한다.
checkout은 고정된 action 커밋을 사용하고 git 인증 정보를 저장하지 않는다.
권한이 있는 Python 실행은 `-I` 격리 모드로 로컬 모듈과 환경변수 주입을 차단한다.

```sh
python3 -m unittest discover -s .github/scripts -p 'test_codex_merge.py' -v
```

실제 이벤트→요약→반응→머지 전체 경로는 dev에 활성화한 뒤 적합한 실제 PR에서
확인해야 한다. 오프라인 테스트 성공은 운영 자동 머지 성공을 의미하지 않는다.

공식 API 동작:
- [워크플로 이벤트](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)
- [SHA를 지정한 PR merge](https://docs.github.com/en/rest/pulls/pulls#merge-a-pull-request)
- [브랜치 필수 체크](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)
- [GITHUB_TOKEN 이벤트 재귀 방지](https://docs.github.com/en/actions/how-tos/writing-workflows/choosing-when-your-workflow-runs/triggering-a-workflow)
