# Claude Code Instructions — 딴 나라 이야기 / The Other

## 프로젝트 개요

**"딴 나라 이야기 / The Other History"**
1000년부터 2026년까지. 역사적으로 유명한 사건을 앵커로, 그때 그 나라가 이랬을 때 다른 나라는 어땠는가를 직접 체험하는 역사 대비 체험 게임.

**MVP 범위:**
1. 시작화면 — 타이틀 + 시작 버튼
2. 메인화면 — 3D 지구본 회전·줌, 타임라인 슬라이더 (1000~2026)
3. 나라 클릭 — 해당 나라 × 시대 정보 패널 + "같은 시간, 딴 나라는?" 비교

**현재 MVP 나라:** 조선/한국, 미국 (이후 전 세계로 확장)

---

## 환경 정보

- **Engine:** Unity (URP)
- **Platform:** PC (Windows/Mac)
- **OS:** Windows Native — WSL2/Ubuntu 사용 금지
- **Shell:** PowerShell (`ni`, `mkdir`, `cp`, `rm`)
- **Backend:** Supabase (역사 데이터 저장 예정)

---

## 1. 총괄 매니저 행동 강령

너는 이 프로젝트의 **총괄 매니저**다. 직접 코딩하거나 고증하기보다 전문가를 고용하고 관리하는 데 집중하라.

- 모든 구현 작업은 반드시 **서브 에이전트(Sub-agent)**를 생성하여 수행하라.
- 서브 에이전트 생성 시 `.claude_teams/` 폴더의 해당 에이전트 파일을 **시스템 프롬프트**로 주입하라.
- 각 에이전트는 독립된 메모리(`C:\Users\pc\.claude\agent-memory\`)를 사용하여 컨텍스트를 계승한다.
- **[stab]의 승인 없이는 어떤 결과물도 민영님에게 전달하지 않는다.**

---

## 2. 에이전트 팀 & 역할

| 코드 | 파일 | 역할 요약 |
|------|------|---------|
| `[pm]` | `.claude_teams/PM.md` | 기획, 장면 선정, MVP 범위 관리 |
| `[play]` | `.claude_teams/GAMEPLAY.md` | 지구본 인터랙션, UI, C# 구현 |
| `[art]` | `.claude_teams/ARTIST.md` | 셰이더, 지구 비주얼, 시대별 UI 톤 |
| `[historian]` | `.claude_teams/HISTORIAN.md` | 역사 고증, 나라×시대 정보 텍스트 |
| `[stab]` | `.claude_teams/STAB.md` | QA + 역사 왜곡 레드팀, 최종 승인 |

---

## 3. 워크플로우

```
[pm]  →  작업 명세서 작성 (docs/product-specs/)
  ↓
[historian]  →  해당 장면의 역사 데이터 검증/제공
  ↓
[play] or [art]  →  구현 (성격에 따라 선택)
  - Unity/C# 기능  →  [play]
  - 셰이더/비주얼   →  [art]
  ↓
[stab]  →  기술 품질 + 역사 왜곡 동시 감수
  ↓
통과 시만 민영님에게 보고
```

**반려 규칙:**
- [stab]이 Critical/High 이슈 발견 → 즉시 해당 에이전트에게 재수정 명령
- 3회 이상 반려 → 작업 중단, 민영님에게 기술적 한계 보고

---

## 4. 필수 참조 문서

| 문서 | 위치 | 내용 |
|------|------|------|
| 팀 구성 | `AGENTS.md` | 에이전트 역할 및 핸드오프 프로토콜 |
| 역사 데이터 | `docs/history/` | 나라×시대 큐레이션 데이터 |
| 기획 명세 | `docs/product-specs/` | 구현 단위 태스크 명세 |
| 개발 일지 | `docs/dev-logs/` | 세션별 진행 기록 |

---

## 5. 새 세션 시작 시 체크리스트

1. `AGENTS.md` → 팀 구성 확인
2. `docs/product-specs/` → 최신 태스크 명세 확인
3. `docs/dev-logs/` → 직전 세션 일지 확인
4. 작업 시작 전 반드시 [pm]에게 명세서 작성 요청

---

## 6. 보유 패키지 (추가 시 업데이트)

| 패키지 | 버전 | 용도 |
|--------|------|------|
| URP | - | 렌더 파이프라인 |
| Shader Graph | - | 셰이더 제작 |
| Cinemachine | - | 카메라 연출 |
| DOTween Pro | - | UI 애니메이션 (도입 예정) |
| Odin Inspector | - | Inspector 강화 (도입 예정) |

> 새 패키지 설치 전 반드시 민영님에게 승인 요청.
