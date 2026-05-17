# CS2KR.Admin

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.367+-blue)](https://github.com/roflmuffin/CounterStrikeSharp)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)

[CS2.KR](https://cs2.kr) 에서 사용하는 통합 어드민 플러그인. CounterStrikeSharp 기반으로 **CS2-SimpleAdmin 을 완전 대체**한다.

## 핵심 특징

- **웹 ↔ 인게임 즉시 동기화** (1초 폴링, RCON 미사용). `sa_admin_events` 큐 테이블 기반.
- **전역 밴** — 모든 밴은 `server_id = NULL` 로 발급되어 모든 서버에 적용.
- **서버 자동 식별** — 새 서버 추가 시 코드 수정 불필요. `sa_servers` 자동 매칭 또는 NULL 모드.
- **CS2-SimpleAdmin 스키마 호환** — 기존 `sa_*` 테이블 그대로 사용. 데이터 손실 없는 drop-in 교체.
- **권한 시스템** — `sa_admins` + `sa_admins_flags` + `sa_groups` + `#그룹` 참조 확장.
- **CenterHtmlMenu** — 한글 인게임 어드민 메뉴 (`css_admin`).
- **Discord 실시간 로그** — 모든 액션 단일 webhook 으로 발신.

## 요구사항

- CounterStrikeSharp 1.0.367 이상
- .NET 8.0
- MariaDB/MySQL 10.3+ (CS2-SimpleAdmin 스키마)

## 명령어

| 명령 | 권한 | 설명 |
|---|---|---|
| `css_ban <대상> <분> <사유>` | `@css/ban` | 밴 발급 (0 = 영구) |
| `css_unban <steamid> [사유]` | `@css/unban` | 언밴 |
| `css_kick <대상> [사유]` | `@css/kick` | 킥 |
| `css_mute <대상> <분> <사유>` | `@css/chat` | 음성 차단 |
| `css_gag <대상> <분> <사유>` | `@css/chat` | 채팅 차단 |
| `css_silence <대상> <분> <사유>` | `@css/chat` | 음성+채팅 차단 |
| `css_unmute / css_ungag / css_unsilence <id>` | `@css/chat` | 해제 |
| `css_slay <대상>` | `@css/slay` | 자살 처리 |
| `css_slap <대상> [데미지]` | `@css/slay` | 슬랩 |
| `css_changemap <맵>` / `css_map <맵>` | `@css/changemap` | 맵 변경 |
| `css_rcon <명령>` | `@css/rcon` | RCON 실행 |
| `css_who` | - | 접속한 어드민 목록 |
| `css_admins` | - | 전체 어드민 목록 |
| `css_admin` | - | 인게임 어드민 메뉴 |
| `css_say / css_csay / css_psay` | `@css/chat` | 메시지 |
| `css_reload_admins / css_reload_bans` | `@css/root` | 캐시 리로드 |

타겟 토큰: `@all`, `@me`, `@!me`, `@ct`, `@t`, `@dead`, `@alive`, `#<userid>`, `<steamid64>`, 또는 이름 부분 매치.

## 빌드

```bash
git clone https://github.com/CS2KR/CS2KR.Admin.git
cd CS2KR.Admin
dotnet build -c Release
```

산출물: `bin/Release/net8.0/CS2KR.Admin.dll` + 의존 dll.

## 설치

CS2 서버 호스트에 다음 구조로 배포:

```
csgo/addons/counterstrikesharp/
├── plugins/CS2KR.Admin/
│   ├── CS2KR.Admin.dll
│   ├── MySqlConnector.dll
│   └── (기타 의존 dll)
└── configs/plugins/CS2KR.Admin/
    └── CS2KR.Admin.json     ← CS2KR.Admin.example.json 복사 후 자격증명 입력
```

`bin/Release/net8.0/` 전체를 `plugins/CS2KR.Admin/` 로 복사하면 가장 간단.

## 설정 (`CS2KR.Admin.json`)

```jsonc
{
  "Database": {
    "Host": "127.0.0.1",
    "Port": 3306,
    "Database": "cs2kr",
    "Username": "cs2kr",
    "Password": "..."
  },
  "Discord": {
    "WebhookUrl": "https://discord.com/api/webhooks/.../...",
    "Enabled": true
  },
  "ServerIdOverride": 0,
  "PollIntervalSeconds": 1.0,
  "BanReasons":  ["핵 사용", "스크립트 사용", "비매너", "광고", "트롤링"],
  "MuteReasons": ["욕설", "스팸", "비매너", "광고"],
  "BroadcastBans": true,
  "ChatPrefix": " [CS2KR] "
}
```

## 즉시 동기화 인프라 (웹 측 통합)

플러그인이 폴링하는 이벤트 큐 테이블 `sa_admin_events` 를 웹 측에서 채워야 한다:

```sql
CREATE TABLE sa_admin_events (
  id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  event_type       VARCHAR(32) NOT NULL,
  target_steamid   VARCHAR(64) NULL,
  target_record_id BIGINT NULL,
  payload          JSON NULL,
  server_id        INT NULL,
  created_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX (id), INDEX (created_at)
) ENGINE=InnoDB;
```

웹 관리자가 ban/mute/admin 발급 시 `sa_bans`/`sa_mutes`/`sa_admins` INSERT 직후 `sa_admin_events` 에도 행 INSERT. 모든 게임서버 플러그인이 1초 폴링으로 감지하고 즉시 enforce.

이벤트 타입: `ban`, `unban`, `ban_edit`, `mute`, `unmute`, `mute_edit`, `kick`, `reload_admins`.

## CS2-SimpleAdmin → CS2KR.Admin 마이그레이션

1. `sa_admin_events` 마이그레이션 적용.
2. 웹 컨트롤러에서 ban/mute/admin 변경 시 이벤트 발행 추가.
3. 플러그인 빌드.
4. **한 대의 CS2 서버**에서 검증:
   - 정지 → `plugins/CS2-SimpleAdmin/` 제거 → `plugins/CS2KR.Admin/` 투입 → config → 시작.
   - 어드민 SteamID 로 `css_admin` 동작 확인.
   - 웹에서 테스트 SteamID 밴 발급 → 2초 내 인게임 반영.
5. 나머지 서버 순차 배포.

기존 `sa_bans/sa_mutes/sa_admins/sa_groups` 행은 그대로 — **데이터 손실 없음**.

## 아키텍처

```
┌──────────────┐  INSERT sa_bans + sa_admin_events  ┌──────────────┐
│   웹 관리자   │ ─────────────────────────────────► │   MariaDB    │
└──────────────┘                                     └──────┬───────┘
                                                            │
                                      ┌──── 1초 폴링 ───────┤
                                      │                     │
                ┌──────────┐  ┌───────▼──┐  ┌──────────┐ ┌──▼───────┐
                │ CS2 서버 1│  │ CS2 서버 2│  │ CS2 서버 N│ │ … server │
                │ 플러그인  │  │ 플러그인  │  │ 플러그인  │ │  CS2KR  │
                └────┬─────┘  └────┬─────┘  └────┬─────┘ │ .Admin   │
                     ▼             ▼              ▼       └──────────┘
                  킥/뮤트       킥/뮤트         킥/뮤트
                  Discord       Discord         Discord
```

- 각 플러그인 인스턴스는 메모리에 `lastSeenEventId` 커서 유지.
- 폴링 쿼리는 인덱스 hit (`WHERE id > @cursor`).
- 모든 액션 idempotent (이미 킥된 플레이어 재킥은 no-op).
- 클록 비교는 DB `NOW()` 사용 (clock skew 방지).

## 트러블슈팅

- **권한이 인게임에서 안 잡힘**: `sa_admins_flags` 에 `#그룹이름` 행이 있는지 확인. `sa_admins.group_id` 만 채우면 권한 부여 안 됨 (CS2-SimpleAdmin 의 함정).
- **밴이 즉시 안 됨**: `sa_admin_events` 에 행이 INSERT 되는지, 플러그인 로그에 "이벤트 커서 초기화" 가 보이는지 확인.
- **`sa_servers 매칭 실패`**: 로그에 보이면 NULL 글로벌 모드로 동작 — 정상. 명시적으로 server_id 를 부여하려면 `ServerIdOverride` 설정.

## 라이선스

MIT License. [LICENSE](LICENSE) 참조.

## 기여

이슈/PR 환영. CS2KR 본 사이트는 [github.com/CS2KR](https://github.com/CS2KR).
