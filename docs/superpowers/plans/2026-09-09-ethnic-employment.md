# 민족집단별 고용과 임금 구현 계획

목표: 집단별 노동가능인구에 기반한 고용을 먼저 검증하고, 그 뒤 주간 임금 이체를 연결한다.

## 설계

- ProvinceEmployment가 프로빈스 내 Building → ProvinceEthnicPop → 인원 기록을 소유한다.
- 동일 프로빈스만 채용한다. 기존 참여율(25%, 100%, 85%, 0%)을 유지한다.
- 건물·집단·지역의 고용 합계는 하나의 기록에서 파생한다.
- 초기 JSON workerScale 인원을 실제 집단에 배정하며 공급 부족이면 비례 축소한다.
- 주간 기존 고용을 유지하고 인구 감소, 건물 축소/제거를 먼저 반영한다. 빈자리는 건물당 최대 50명씩 충원한다.
- 건물별 부족 인원에 노동력을 비례 분배하고 집단별 미취업 인원에 비례해 채용한다. 정수 나머지는 계좌 ID 순으로 결정한다.
- 산업 지원은 관리되는 건물의 인력을 직접 증가시키지 않는다. 다음 주간 채용의 재원을 제공한다.
- 기본 주간 임금은 1인당 1이며 BuildingType의 weeklyWage로 설정한다. 양수만 허용한다.
- 원재료가 필요한 건물은 잔액의 절반을 임금 한도로 쓰고 나머지를 원재료용으로 남긴다. 원재료 없는 건물은 잔액을 임금 한도로 쓴다. 이는 초기 밸런스 규칙이며 원재료 구매 성공 보장은 아니다.
- 같은 주 임금은 한 번만 지급한다. 부족 자금, 교차 원장, 오버플로 등은 지급 전 검증한다.
- 프로빈스 단위 임금 batch 성공 후 고용 계획을 확정한다. 실패 시 해당 주 노동을 진행하지 않도록 게임 루프에서 명시적으로 실패를 알린다.
- 생산과 공사보다 먼저 고용/임금을 처리한다. 건설회사는 실제 고용 비율에 비례한 인시를 사용한다.
- 초기화 전 도메인 fixture의 기존 currentWorkers 값은 지원하되 게임 초기화 후에는 직접 변경을 막는다.
- 프로빈스 상세에 집단별 노동가능, 취업, 미취업, 자산을 표시한다.
- 임금 부채, 문화별 채용 차별, 통근, 직업/숙련도, 저장 형식은 이번 범위 밖이다.

## 작업 순서

- [x] 1. 기준 Unity EditMode 전체 테스트 확인.
- [x] 2. 고용 회귀 테스트 추가: 초기 배분, 노동 공급 부족, 여러 건물 중복 고용, 인구 감소, 건물 제거, 안정적 배분과 우회 금지. RED 확인.
- [x] 3. ProvinceEmployment.cs와 Building/Province/ProvinceEthnicPop 파생값 구현. 고용 테스트 GREEN, 기존 전체 테스트 확인.
- [x] 4. 임금 테스트 추가: 정확한 생산자 계좌 차감과 집단별 수령, 공급량 보존, 잔액 부족, 중복 주간 실행, 교차 원장 실패 원자성. RED 확인.
- [x] 5. TryProcessWeek(long week) 구현, 양수 임금 JSON 로딩, 게임 초기화 및 주간 순서, 산업 지원, UI 연결.
- [x] 6. 최종 전체 테스트, 직접 노동자 변경 경로와 실행 순서 검토, 사용자 문서 작성.

## 인터페이스 및 파일

- 새 `Assets/Scripts/Class/ProvinceEmployment.cs`: Initialize(Province), Reconcile(), WorkersAt(Building), Employed(ProvinceEthnicPop), TotalEmployed, GetWorkers(Building), TryProcessWeek(long).
- `Building.cs`: currentWorkers 호환 뷰, weeklyWage, HireWorkers 경로.
- `Province.cs`, `EthnicGroup.cs`: 고용 관리자 및 집단별 파생 합계.
- `GlobalVariables.cs`: BuildingType 임금 로딩/검증.
- `GovernmentBudget.cs`: 관리되는 건물의 직접 인원 수정 제거.
- `GameManager.cs`: 경제 초기화 뒤 고용 초기화, 주간 생산 전 임금, 고용 기반 건설.
- `ProvinceDetailUI.cs`: 인구집단별 고용 지표.
- `Assets/Tests/EditMode/EmploymentTests.cs`: 기존 ReflectionTestHelpers로 런타임 호출.

검증 명령: Unity 6000.0.71f1 `-batchmode -nographics -projectPath D:/ProjectNO/.worktrees/ethnic-employment -runTests -testPlatform EditMode -testResults <unique.xml> -logFile <unique.log>`; `-quit` 사용하지 않음.

## 최종 검증

고용 7개 RED 후 전체 100/100 GREEN, 임금 5개 RED 후 전체 105/105 GREEN.
연령 이동 자동 조정 회귀 테스트 및 통합 테스트를 추가했다.
독립 검토에서 공개 Reconcile(true)의 무급 채용 경로를 확인했고, 자금 0에서 50명으로 증가하는 RED를 재현한 뒤 Reconcile()을 감원 전용으로 변경했다.
최종 Unity EditMode 결과: 111/111 통과, 실패/스킵/컴파일 오류 0. 검토자 재확인에서 추가 문제 없음.
테스트 결과: TestResults/review-fixed.xml 및 review-fixed.log (로컬 생성 파일).
