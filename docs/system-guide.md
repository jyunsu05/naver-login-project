# naver-login-project 시스템 가이드

이 문서는 **현재 이 레포(`jyunsu05/naver-login-project`)** 기준으로, 시스템 구조·흐름·초기화·API를 정리한 가이드입니다.  
외부 용어집의 **시스템 설계(3-tier, 토큰 분리, Full Reset 층)** 는 참고하되, **파일명·API·흐름은 아래 내용이 정본**입니다.

---

## 1. 전체 구성 (3-tier)

```
[Unity 게임 client/]  ←sessionToken→  [Node 서버 sarver/ :3000]  ←OAuth→  [Naver]
        ↑                                      ↑
   로그인 버튼                          MySQL users, JWT, 토큰 암호화
   PlayerPrefs                          브라우저 브리지 파일
   (선택) :7777 HttpListener            Chrome 쿠키/스토리지 정리
```

| 구성 | 역할 |
|------|------|
| **Unity (`client/`)** | 네이버 로그인 버튼, `sessionToken` 저장, 시작 시 `/auth/me` 자동 로그인 |
| **서버 (`sarver/`)** | OAuth 중계, DB 저장, JWT 발급, Naver 토큰 암호화 보관, 개발용 초기화 |
| **Naver** | 실제 로그인·OAuth 권한 동의 |
| **Chrome** | 로그인·서비스 동의 UI (Unity WebView 아님) |

---

## 2. 폴더·핵심 파일

| 경로 | 설명 |
|------|------|
| `client/Assets/test.cs` | 메인 로그인 로직 (Chrome 열기, 브리지 폴링) |
| `client/Assets/NaverLoginCallbackListener.cs` | PlayerPrefs, HTTP 헬퍼, `:7777` HttpListener (보조 경로) |
| `client/Assets/Editor/NaverLoginSessionToolsWindow.cs` | `Naver Login > Dev Reset Tools` |
| `sarver/app.js` | Express 라우트 전체 |
| `sarver/naver-auth.js` | Naver OAuth, `grant_type=delete` 토큰 폐기 |
| `sarver/user-tokens.js` | JWT 세션, DB 토큰, 로그아웃·초기화 |
| `sarver/local-session-bridge.js` | `%USERPROFILE%\.naver-login-project\local-session.json` |
| `sarver/service-consent.js` | 서비스 이용 동의 쿠키 (`naver_service_consent`) |
| `sarver/dev-full-reset.js` | 전체 초기화 (Naver 폐기 + DB + 브리지 + 브라우저) |
| `sarver/clear-naver-browser-data.js` | Chrome/Edge naver·localhost 쿠키/스토리지 삭제 |
| `sarver/login-consent.html` | 브라우저 **서비스 이용 동의** 페이지 |

> 서버 폴더명은 `sarver/` 입니다 (`server/` 아님).

---

## 3. 포트

| 포트 | 프로세스 | 역할 |
|------|----------|------|
| **3000** | `node app.js` | OAuth, REST API, HTML 페이지 |
| **7777** | Unity HttpListener | OAuth 성공 후 token 직접 수신 (보조, `?delivery=unity` 시) |

---

## 4. 로그인 흐름 (현재 기본: Chrome + 브리지)

```
1. Unity [네이버 로그인] 클릭
2. Chrome → GET /auth/naver
3. (서비스 동의 쿠키 없으면) → /login/consent → 동의 → /auth/naver
4. Naver OAuth 로그인·권한 동의
5. Naver → GET /auth/naver/callback?code=...
6. 서버: DB 저장 + JWT sessionToken 발급 + local-session.json 기록
7. 브라우저: login-success.html 표시
8. Unity: GET /auth/dev/session 폴링 → sessionToken → PlayerPrefs 저장
9. Unity: POST /auth/me → 자동 로그인 확인
```

### 동의 화면은 두 종류

| 종류 | 어디서 | 다시 보이게 하려면 |
|------|--------|-------------------|
| **서비스 이용 동의** | `/login/consent` (우리 페이지) | localhost 쿠키 삭제 또는 `/login/consent/revoke` |
| **Naver OAuth 동의** | Naver 로그인 화면 | `grant_type=delete` 토큰 폐기 + (권장) 브라우저 naver 쿠키 삭제 |

---

## 5. 토큰·데이터 저장 위치

| 데이터 | 발급 | 저장 위치 | Unity가 알아야 하나? |
|--------|------|-----------|---------------------|
| Naver `access_token` | Naver | MySQL `users` (암호화) | ❌ |
| Naver `refresh_token` | Naver | MySQL `users` (암호화) | ❌ |
| `sessionToken` (JWT) | 우리 서버 | Unity **PlayerPrefs** `naver_session_token` | ✅ |
| 서비스 동의 | 우리 서버 | 브라우저 쿠키 `naver_service_consent` | ❌ |
| 브라우저 로그인 브리지 | 우리 서버 | `~/.naver-login-project/local-session.json` | ❌ (Unity가 폴링) |

---

## 6. API 요약

### 인증 (운영 흐름)

| 메서드 | 경로 | 설명 |
|--------|------|------|
| GET | `/auth/naver` | Naver OAuth 시작 (동의 쿠키 없으면 `/login/consent`로) |
| GET | `/auth/naver/callback` | Naver 콜백, JWT 발급, 브리지 기록 |
| POST | `/auth/me` | `sessionToken` 검증, 자동 로그인 |
| POST | `/auth/refresh` | Naver 토큰 갱신 + JWT 재발급 |
| POST | `/auth/logout` | Naver 토큰 폐기 시도 + 세션 무효화 |

### 브라우저·동의

| 메서드 | 경로 | 설명 |
|--------|------|------|
| GET | `/login/consent` | 서비스 이용 동의 페이지 |
| GET | `/login/consent/accept` | 동의 쿠키 저장 → `/auth/naver` |
| GET | `/login/consent/revoke` | 서비스 동의 쿠키 삭제 |
| GET | `/login/success` | OAuth 결과 페이지 (브리지 연동) |
| GET | `/auth/dev/session` | 브리지 sessionToken 조회 (로컬 전용) |

### 개발용 초기화 (로컬 `127.0.0.1` 전용)

| 메서드 | 경로 | 설명 |
|--------|------|------|
| POST | `/auth/dev/full-reset` | **전체** 초기화 (아래 §7) |
| POST | `/auth/dev/reset` | **현재 유저만** Naver 폐기 + DB 행 DELETE |
| POST | `/debug/reset` | 위와 동일 (외부 MD 호환 별칭) |
| DELETE | `/auth/dev/session` | 브리지 파일만 삭제 |

`POST /auth/dev/reset` body 예시:

```json
{ "sessionToken": "..." }
```

---

## 7. Full Reset (전체 초기화) — 4개 층

테스트를 **처음부터** 다시 하려면 아래가 **모두** 정리되어야 합니다.

| 순서 | 층 | 하는 일 | 도구 |
|------|-----|---------|------|
| 1 | Unity | PlayerPrefs `naver_session_token` 삭제 | Dev Reset Tools |
| 2 | Naver | `grant_type=delete` 로 access_token 폐기 | `dev-full-reset.js` |
| 3 | 서버 DB | `DELETE FROM users` | `dev-full-reset.js` |
| 4 | 브리지 | `local-session.json` 삭제 | `dev-full-reset.js` |
| 5 | 브라우저 | naver·127.0.0.1 쿠키/스토리지 삭제 (서비스 동의 포함) | `clear-naver-browser-data.js` |

### 실행 방법

**Unity:** `Naver Login > Dev Reset Tools` → **전체 초기화 실행**

**터미널:**

```powershell
cd sarver
npm run dev:reset
```

브라우저가 열려 쿠키 삭제가 실패할 때:

```powershell
npm run dev:reset:force
```

### 현재 유저만 초기화 (부분)

Dev Reset Tools → **현재 유저만 초기화**  
또는 `POST /auth/dev/reset` — DB에서 해당 유저만 삭제, 브라우저 쿠키는 건드리지 않음.

---

## 8. 환경 변수 (`.env`)

`sarver/.env.example` 참고. Git에 올리지 않습니다.

| 변수 | 용도 |
|------|------|
| `MYSQL_*` | MySQL 연결 (`MYSQL_DATABASE=gamedb`) |
| `NAVER_CLIENT_ID` / `NAVER_CLIENT_SECRET` | Naver OAuth (서버만) |
| `NAVER_CALLBACK_URL` | `http://127.0.0.1:3000/auth/naver/callback` |
| `SESSION_JWT_SECRET` | JWT sessionToken 서명 |
| `TOKEN_ENCRYPTION_KEY` | DB 내 Naver 토큰 암호화 (없으면 JWT secret 사용) |
| `UNITY_CALLBACK_URL` | `http://127.0.0.1:7777/naver-login/` (보조 경로) |

---

## 9. 헷갈리기 쉬운 것 (시스템)

### 쿠키 삭제 vs Naver 폐기 vs sessionToken 삭제

| 동작 | 효과 |
|------|------|
| Chrome naver 쿠키 삭제 | 브라우저 **Naver 로그인 상태** 해제 가능 |
| `sessionToken` 삭제 | Unity **자동 로그인만** 해제 |
| DB `users` DELETE | 서버 **사용자·토큰 기록** 삭제 |
| **`grant_type=delete`** | Naver **앱 연동 해제** → OAuth 동의 다시 가능 |
| 서비스 동의 쿠키 삭제 | `/login/consent` **다시 표시** |

**Naver OAuth 동의**를 다시 보려면 보통 **토큰 폐기(revoke/delete)** 가 필요합니다.  
**서비스 이용 동의**는 별도 쿠키이므로 localhost 쿠키 삭제로 초기화됩니다.

### 3000 vs 7777

| | 3000 | 7777 |
|---|------|------|
| **주체** | Node 서버 | Unity HttpListener |
| **역할** | OAuth, API, HTML | (선택) token URL 리다이렉트 수신 |
| **현재 기본** | Chrome 로그인·브리지의 중심 | 보조 (`delivery=unity`) |

---

## 10. 서버 명령어

```powershell
# 서버 시작
cd sarver
node app.js

# 포트 3000 사용 확인
netstat -ano | findstr ":3000"

# 서버 종료 (PID 확인 후)
Stop-Process -Id <PID>

# 자동 테스트
npm run auth:test

# 전체 초기화 (터미널)
npm run dev:reset
```

---

## 관련 문서

- [웹 로그인 아키텍처 (이식·다른 플랫폼 확장)](./web-login-architecture.md)
- [통합 테스트 체크리스트](../TESTING.md)

---

*이 문서는 `sarver/` + `client/` 현재 코드 기준입니다. 서버 재시작 후 API·초기화 동작을 변경했다면 반드시 `node app.js`를 다시 실행하세요.*
