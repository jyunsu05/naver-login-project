# 통합 테스트 체크리스트

## 서버 자동 테스트

```bash
cd sarver
npm install
npm run auth:test
```

예상 결과:
- OK - 토큰 암호화/복호화
- OK - JWT 발급/검증
- OK - DB 스키마 마이그레이션

`.env`에 MySQL 설정이 필요합니다.

## 사전 준비

1. `sarver/.env` — `SESSION_JWT_SECRET`, Naver OAuth, `MYSQL_DATABASE` 등 설정
2. 서버 실행: `cd sarver && npm start`
3. Unity: `client` 폴더 → `SampleScene` → Play

시스템 구조·API·초기화: [docs/system-guide.md](docs/system-guide.md)  
다른 프로젝트 이식·Google/Kakao 등 확장: [docs/web-login-architecture.md](docs/web-login-architecture.md)

---

## Unity 수동 테스트

### 시나리오 1: 첫 로그인 (Chrome + 동의)

- [ ] Play 후 **네이버 로그인** 클릭 → **Chrome** 열림
- [ ] `/login/consent` **서비스 이용 동의** 표시 (최초 또는 쿠키 없을 때)
- [ ] 동의 후 Naver OAuth 로그인 완료
- [ ] 브라우저에 로그인 성공 페이지 표시
- [ ] Unity Console: 브리지에서 `sessionToken` 저장, `/auth/me` 성공

### 시나리오 2: 재실행 자동 로그인

- [ ] Play 중지 후 다시 Play
- [ ] OAuth·Chrome 없이 `/auth/me` 자동 로그인
- [ ] Console에 사용자 정보 출력

### 시나리오 3: 로그아웃 후 재인증

- [ ] **로그아웃** 클릭 (`POST /auth/logout` + Naver 토큰 폐기 시도)
- [ ] **네이버 로그인** 다시 클릭 → Chrome OAuth 진행

### 시나리오 4: 전체 초기화 후 처음부터

- [ ] `Naver Login > Dev Reset Tools` → **전체 초기화 실행**
- [ ] (권장) Chrome/Edge 종료 후 실행
- [ ] 다시 Play → **서비스 이용 동의** + **Naver 로그인** 처음부터

부분 초기화: **현재 유저만 초기화** (`POST /auth/dev/reset` 동일)

---

## 터미널에서 전체 초기화

```powershell
cd sarver
npm run dev:reset
```

브라우저 잠금 시:

```powershell
npm run dev:reset:force
```
