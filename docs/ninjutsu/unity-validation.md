# Unity 검증

2026-10-05, Unity 6000.6.3f1. 대상은 현재 스킬 조합 구조 개편 작업 트리다.

EditMode **471/471 통과**, 실패·건너뜀 0. 숫자 강화 38종, 카드 조건·상한·공유 갱신·(+) 변형, 발사 경로, 상태이상, 분열, 풀 재사용, 설정 불변성, 강화 배치 롤백, 공격 이벤트·확률·주기·수명 종료 순서를 검증한다. 새 수명 관리 테스트는 비행 중인 효과와 풀 대기 효과를 함께 파괴하며 중복 정리와 정리 후 Tick을 안전하게 처리하는지 확인한다.

작업 에디터를 유지하고 `/private/tmp/projectnova-skill-validation` 사본에서 CLI 테스트를 실행했다. Scripts/Resources의 SHA-256이 작업 트리와 일치하는지 대조한다. 결과 XML·로그·소스 해시는 `out/skill-refactor/`에 보존한다. 종료 코드만으로 통과를 판단하지 않고 XML의 result/total/passed/failed/skipped를 확인한다.

```sh
unity test <검증용 사본 절대 경로> --mode EditMode --timeout 600 \
  --output <결과 XML 절대 경로> --format json -- \
  -nographics -logFile <로그 절대 경로>
python3 docs/ninjutsu/validate_requirements.py
```

요구사항 검증은 15종·168개 카드·66개 실행 연결의 ID·참조·횟수·원문 위치·선행 그래프를 확인한다. 이 수치는 나머지 10종의 전투 구현 완료를 뜻하지 않는다.

프로덕션 스테이지 선택 UI, 실제 영구 성장 데이터, 최종 밸런스·아트·성능 및 미구현 10종은 별도 검증이 필요하다.
