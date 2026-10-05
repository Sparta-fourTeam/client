# Unity EditMode 실제 검증

2026-10-05 (한국 시간). 검증 대상 코드: 71d288c까지의 codex/ninjutsu-refactor.

Unity 6000.6.3f1 CLI로 실제 EditMode 테스트 409건을 실행하여 409건 통과, 실패 0, 건너뜀 0을 확인했다. 스크립트 컴파일·Unity 후처리·Test Runner 실행을 포함한다. (+) 전환 표시/적용, 공유 후보 중복 방지와 양쪽 갱신, 연속 화염구Ⅱ의 두 번 선택 선행조건 테스트가 결과 XML에 포함된다.

작업 에디터를 종료하지 않고 검증용 사본 work/unity-cli-check를 사용했다. Scripts/Resources 파일이 원래 워크트리와 동일한지 SHA-256으로 대조했다. 사본에서 관련 없는 URP 설정 변경/사본은 제외하고 커밋된 설정을 사용했다.

이전 UDS 초기화 오류는 일반 샌드박스에서 발생했다. 승인된 샌드박스 밖 CLI 실행에서는 Unity가 정상 기동하고 테스트를 완료했다. 프로젝트 코드의 UDS 우회 설정이나 라이선스 설정 변경은 하지 않았다.

## 재실행

검증용 사본의 Scripts/Resources를 최신 작업 코드로 맞춘 뒤 실행한다. 다른 Unity 에디터가 검증용 사본을 열고 있으면 먼저 그 사본을 닫아야 한다. 원래 작업 에디터는 유지할 수 있다.

```sh
/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath <검증용 사본 절대 경로> \
  -runTests -testPlatform EditMode \
  -testResults <결과 XML 절대 경로> \
  -logFile <로그 절대 경로>
```

종료 코드만으로 통과를 판단하지 않고 XML의 result·total·passed·failed·skipped를 확인한다. 현재 원본 결과는 작업 outputs/unity-path-visual-results.xml에 저장했다. 게임 플레이·시각 효과와 미구현 인술 검증은 EditMode 성공과 별개로 남아 있다.

현재 테스트는 통나무 경로·예비 시전, 5종 강화 실행, 상태이상·분열·풀 재사용 및 리팩토링 경로를 포함한다. 409개 테스트는 미구현 10종의 전투 완성을 검증하지 않는다. 문서만 갱신한 이번 단계는 요구사항 검증기를 실행하며 Unity 테스트를 반복하지 않는다.
