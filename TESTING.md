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

## Unity 수동 테스트

### 사전 준비
1. `sarver/.env`에 `SESSION_JWT_SECRET`, Naver OAuth, MySQL 설정
2. `cd sarver && npm start`
3. Unity에서 `client` 폴더 열기 → `SampleScene` → Play

### 시나리오 1: 첫 로그인 (OAuth)
- [ ] Play 후 Console에 `[Naver] Unity 콜백 리스너 시작` 로그
- [ ] "네이버 로그인" 버튼 클릭 → WebView 표시
- [ ] 네이버 로그인 완료 → `7777` 콜백으로 token 수신
- [ ] Console에 uid/email/name 출력

### 시나리오 2: 재실행 자동 로그인
- [ ] Play 중지 후 다시 Play
- [ ] Console에 `/auth/me 자동 로그인 시도` 로그
- [ ] OAuth WebView 없이 사용자 정보 출력

### 시나리오 3: 로그아웃 후 재인증
- [ ] "로그아웃" 버튼 클릭
- [ ] "네이버 로그인" 클릭 → OAuth WebView 다시 표시
