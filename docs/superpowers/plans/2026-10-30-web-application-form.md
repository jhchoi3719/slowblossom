# 홈페이지 신청서 (네이버폼 대체) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 회원이 홈페이지에서 직접 참가 신청을 하면 `/applications` 해당 행사에 "미확정"으로 바로 들어오게 해서, 네이버폼 → 붙여넣기 작업을 없앤다.

**Architecture:** 공개 페이지 `/apply/{eventId}` (Static SSR Razor, `SurveyLayout`)가 multipart 폼을 `POST /apply/{eventId}/submit` 최소 API로 보낸다. 엔드포인트는 `ApplicationFormValidator`로 검증하고, 사진 3장은 공개 `/uploads` 밖의 `{DATA_DIR}/private/applications/`에 저장, `ParticipantApplication` 한 행을 만들고 텔레그램 알림을 보낸다. 관리자는 기존 `/applications` 카드에서 새 항목과 사진(관리자 전용 엔드포인트)을 본다.

**Tech Stack:** ASP.NET Core 8 Blazor Static SSR, EF Core 8 + SQLite (수동 `ALTER TABLE` 마이그레이션), ClosedXML, xUnit(새 테스트 프로젝트).

**Spec:** 이 대화에서 승인된 설계 (아래 Global Constraints에 요약). 네이버폼 원문 문구는 `docs/superpowers/plans/2026-10-30-naver-form-source.md`.

## Global Constraints

- 질문 22개와 동의 문구는 네이버폼 원문 그대로(`naver-form-source.md`). 질문은 코드에 고정, 행사별로 바뀌는 건 제목·안내문(위쪽 블록)뿐.
- 행사별 설정: `ApplyOpen`(신청 받기 on/off), `ApplyTitle`, `ApplyNotice`(여러 줄 텍스트). `ApplyNotice`가 비어 있으면 네이버폼 "안내사항" 원문을 기본값으로 보여준다.
- "기존 참가자예요" 체크 시 사진·서류 3장 선택 사항, 나머지 질문은 그대로 필수.
- 서류 사진은 관리자 로그인(`AuthRoles.Admin`) 상태에서만 볼 수 있다. `/uploads` 정적 경로 아래 저장 금지.
- 사진은 브라우저에서 긴 변 1600px, JPEG 품질 0.82로 줄여서 업로드. 서버는 장당 10MB 초과 거부, 확장자 jpg/jpeg/png만 허용.
- 관심사 최대 3개, 원하는 연령대 1개(22-27 / 28-32 / 33 이상), 참여경로 1개(인스타 / 블로그 / 지인), 음주·흡연 O/X.
- 같은 행사에 같은 연락처(숫자만 비교)로 두 번째 신청은 거부: "이미 신청하셨어요. 수정이 필요하면 카카오톡 채널로 연락 주세요."
- 텔레그램 알림 실패해도 신청은 저장된다.
- 네이버폼 메일 알림, 일괄 추가 기능은 그대로 둔다.

## Review Focus

- 휴대폰에서 10MB 사진 3장 선택 → 압축 후 업로드가 Render 요청 한도/시간 안에 끝나야 함 (`site.js` 압축이 실패하면 원본 업로드로 대체, 서버 10MB 컷).
- 검증 실패로 돌아왔을 때 입력한 글자가 사라지지 않아야 함 (세션 draft 복원; 파일은 다시 선택 안내).
- `ApplyOpen=false`인 행사 주소로 직접 POST해도 저장되지 않아야 함.
- 비로그인 사용자가 `/applications/file/{id}/{kind}`를 열면 로그인 페이지로 가야 함.
- 신청서 삭제 시 저장된 사진 파일도 삭제.

각 항목의 테스트/확인은 해당 Task에 들어 있다.

---

### Task 1: 데이터 모델과 마이그레이션

**Files:**
- Modify: `Models/ParticipantApplication.cs`, `Models/Event.cs`
- Modify: `Data/DatabaseInitializer.cs` (`ApplicationColumnMigrations` 딕셔너리, `MigrateEventsAsync`)
- Modify: `RotationDating.Web.csproj` (`<DefaultItemExcludes>$(DefaultItemExcludes);tests/**</DefaultItemExcludes>`), `.dockerignore` (`tests/`)
- Create: `tests/RotationDating.Web.Tests/RotationDating.Web.Tests.csproj` (xUnit, net8.0, ProjectReference `../../RotationDating.Web.csproj`)

**Interfaces — Produces:**
- `ParticipantApplication` 새 속성 (모두 nullable TEXT/INTEGER 컬럼): `string? KakaoId`, `string? Education`, `string? ReferralSource`, `string? Motivation`, `string? SelfIntro`, `bool IsReturning` (INTEGER NOT NULL DEFAULT 0), `string? PhotoFile`, `string? EmploymentProofFile`, `string? MarriageCertFile` (저장 파일명만, 경로 아님), `string? Source` (`"web"` 또는 null), `DateTime? AgreedAt`.
- `Event` 새 속성: `bool ApplyOpen` (INTEGER NOT NULL DEFAULT 0), `string? ApplyTitle`, `string? ApplyNotice`.

- [ ] Step 1: 속성 추가, `ApplicationColumnMigrations`에 11개 `ALTER TABLE ParticipantApplications ADD COLUMN ...`, `MigrateEventsAsync`에 3개 컬럼 추가 (기존 패턴 그대로).
- [ ] Step 2: 테스트 프로젝트 생성, `dotnet build` 와 `dotnet test` (테스트 0개) 성공 확인.
- [ ] Step 3: 앱 실행 → 기존 DB로 기동 시 로그 "Database initialization completed." 확인.
- [ ] Step 4: Commit `feat: add web application fields to applications and events`

### Task 2: 신청서 검증

**Files:**
- Create: `Services/ApplicationForm/ApplicationFormInput.cs`, `Services/ApplicationForm/ApplicationFormValidator.cs`, `Services/ApplicationForm/ApplicationFormOptions.cs`
- Test: `tests/RotationDating.Web.Tests/ApplicationFormValidatorTests.cs`

**Interfaces — Produces:**
- `ApplicationFormOptions`: `static readonly string[] AgeRanges = ["22-27","28-32","33 이상"]`, `Interests = ["여행","운동","맛집","전시/공연","독서","게임","재테크","반려동물","음악"]`, `ReferralSources = ["인스타","블로그","지인"]`, `const int MaxInterests = 3`.
- `ApplicationFormInput` (평범한 클래스, 세션 직렬화 가능): `Name, BirthDate, Gender, Residence, Phone, KakaoId, Education, Workplace, PreferredAgeRange, ReferralSource, Motivation, SelfIntro` (string), `List<string> Interests`, `bool? Drinking, Smoking`, `bool IsReturning`, `List<DateTime> AvailableDates`, `bool AgreeSelection, AgreeMarriage, AgreePhoto, AgreeRefund, AgreePrivacy`, `bool HasPhoto, HasEmploymentProof, HasMarriageCert`.
- `static class ApplicationFormValidator`: `List<string> Validate(ApplicationFormInput input, Event evt)` — 오류 메시지(한국어) 목록, 비면 통과. `static string? NormalizeBirthDate(string? raw)` — 숫자만 남겨 8자리 `yyyyMMdd`이고 유효한 날짜면 반환, 아니면 null. `static string NormalizePhone(string? raw)` — 숫자만.

- [ ] Step 1: 실패하는 테스트 작성:
  - `Valid_input_passes` → 빈 목록
  - `Missing_name_fails` → `"이름을 입력해 주세요."` 포함
  - `BirthDate_must_be_8_digits` → `"19950219"` 통과, `"950219"` 실패 (`"생년월일 8자리를 입력해 주세요."`)
  - `Four_interests_fail` → `"관심사는 3개까지 선택할 수 있어요."`
  - `Unknown_age_range_fails`
  - `Any_unchecked_agreement_fails` (5개 각각)
  - `New_applicant_needs_three_photos` → 사진 하나 빠지면 실패; `Returning_applicant_skips_photos` → `IsReturning=true`면 사진 없어도 통과
  - `DatePoll_event_needs_one_available_date` → 후보일 중 하나 이상, 후보에 없는 날짜는 실패; FixedDate 행사는 날짜 검사 안 함
  - `Phone_must_be_10_or_11_digits`
- [ ] Step 2: `dotnet test` → 컴파일 실패 확인
- [ ] Step 3: 구현
- [ ] Step 4: `dotnet test` → 전부 PASS
- [ ] Step 5: Commit `feat: validate web application form input`

### Task 3: 비공개 사진 저장소

**Files:**
- Create: `Services/ApplicationForm/ApplicationFileStore.cs`
- Modify: `Program.cs` (DI 등록 `AddSingleton<ApplicationFileStore>()`)
- Test: `tests/RotationDating.Web.Tests/ApplicationFileStoreTests.cs`

**Interfaces — Produces:**
- `sealed class ApplicationFileStore(SiteStorageOptions storage)` — 디렉터리 `{RootPath}/private/applications` 생성.
  - `const long MaxBytes = 10 * 1024 * 1024`
  - `Task<(string? FileName, string? Error)> TrySaveAsync(IFormFile? file)` — 없음/빈 파일 → `(null,null)`; 크기 초과 → `"size"`; jpg/jpeg/png 아님 → `"type"`; 저장 이름 `{Guid:N}{ext}`.
  - `string? GetPath(string? fileName)` — 파일명에 `/`, `\`, `..` 있으면 null; 존재하지 않으면 null.
  - `void Delete(string? fileName)` — 조용히 무시.

- [ ] Step 1: 실패 테스트: 저장 후 `GetPath` 존재; 11MB → `"size"`; `.gif` → `"type"`; `GetPath("../x")` → null; `Delete` 후 `GetPath` null. (`FormFile`을 `MemoryStream`으로 생성, 임시 폴더 `SiteStorageOptions`)
- [ ] Step 2: 실패 확인 → Step 3: 구현 → Step 4: PASS
- [ ] Step 5: Commit `feat: store application photos outside public uploads`

### Task 4: 공개 신청 페이지와 제출

**Files:**
- Create: `Components/Pages/Apply.razor` (`@page "/apply/{EventId:int}"`, `@layout Layout.SurveyLayout`, `[AllowAnonymous]`)
- Create: `Components/Pages/ApplyDone.razor` (`@page "/apply/{EventId:int}/done"`)
- Create: `Components/Pages/ApplyList.razor` (`@page "/apply"` — `ApplyOpen` 행사 목록, 하나뿐이면 그 페이지로 redirect)
- Create: `Services/ApplicationForm/ApplicationDraftStore.cs` (세션 JSON 저장/복원/삭제, `SurveyResponseDraftStore`와 같은 방식)
- Create: `Services/ApplicationForm/ApplicationNoticeDefaults.cs` (`const string Notice` = 네이버폼 "안내사항" 원문, `static string Title(Event evt)` = `ApplyTitle` 없으면 `"슬로우블러썸 {TabLabel} 신청"`)
- Modify: `Program.cs` — `POST /apply/{eventId:int}/submit` (`.DisableAntiforgery()`, 익명)
- Modify: `wwwroot/site.js` — 신청 폼 사진 압축, 관심사 3개 제한, 기존 참가자 체크 시 사진 `required` 해제
- Modify: `wwwroot/app.css` — 신청 페이지 스타일 (`survey-public-*` 클래스 재사용, 모바일 우선)

**Interfaces — Consumes:** Task 1 속성, Task 2 `ApplicationFormValidator`/`ApplicationFormInput`/`ApplicationFormOptions`, Task 3 `ApplicationFileStore`. **Produces:** `ApplicationNotifier` 호출 지점(Task 5에서 연결) — 제출 엔드포인트 저장 직후.

페이지 구성 (위→아래): 제목, 안내문(`ApplyNotice` 또는 기본값, 줄바꿈 유지·HTML 인코딩), "기존 참가자예요" 체크박스(설명: "이전에 참가하신 분은 사진·서류 없이 신청할 수 있어요"), 질문 1–22 원문 순서. 1번은 이름/생년월일 두 칸으로 나눈다. DatePoll 행사면 7번 뒤에 "참여 가능한 날짜" 체크박스(후보일). 숨은 honeypot 입력 `website`(값이 있으면 조용히 done으로 redirect, 저장 안 함). 폼 `enctype="multipart/form-data"`.

제출 엔드포인트 순서: 행사 없음 또는 `ApplyOpen=false` → `/apply/{id}`(닫힘 안내) → honeypot → 폼을 `ApplicationFormInput`으로 변환 → 검증 실패 시 draft 저장 후 `/apply/{id}?error=1` (페이지 맨 위에 오류 목록) → 같은 행사·같은 `NormalizePhone` 존재 시 `?error=duplicate` → 파일 저장(오류 시 저장한 파일 지우고 `?error=file`) → `ParticipantApplication` 저장 (`Source="web"`, `AgreedAt=DateTime.Now`, `BirthDate`=정규화 값, `Interests`=`", "` 결합, DatePoll이면 `Availabilities`) → 알림(Task 5) → draft 삭제 → `/apply/{id}/done`.

`site.js` 압축: `input[type=file][data-compress]` change 시 `createImageBitmap` → canvas 긴 변 1600px → `toBlob('image/jpeg', 0.82)` → `DataTransfer`로 input 파일 교체(이름 `.jpg`). 실패 시 원본 유지.

- [ ] Step 1: 페이지·엔드포인트 구현
- [ ] Step 2: 로컬 실행, 관리자 로그인 → 행사 하나 `ApplyOpen` DB에서 켜기(Task 6 전이면 SQL) → 브라우저 모바일 크기로 `/apply/{id}` 열기
- [ ] Step 3: 확인: (a) 모든 항목 채워 제출 → done 페이지, `/applications`에 "미확정"으로 보임 (b) 관심사 4개 체크 불가 (c) 동의 하나 빼고 제출 → 오류 + 입력값 유지 (d) 같은 번호로 재신청 → duplicate 안내 (e) `ApplyOpen=false` 행사로 curl POST → 저장 안 됨 (f) 기존 참가자 체크 → 사진 없이 제출 성공 (g) 큰 사진 → 저장된 파일 1600px 이하
- [ ] Step 4: Commit `feat: public application form at /apply`

### Task 5: 텔레그램 알림

**Files:**
- Create: `Services/ApplicationForm/ApplicationNotifier.cs`
- Modify: `Program.cs` (DI `AddScoped<ApplicationNotifier>()`, 제출 엔드포인트에서 호출)
- Test: `tests/RotationDating.Web.Tests/ApplicationNotifierTests.cs`

**Interfaces — Produces:** `sealed class ApplicationNotifier(TelegramNotifier telegram, ILogger<ApplicationNotifier> logger)`; `static string BuildMessage(Event evt, ParticipantApplication app)`; `Task NotifyAsync(Event evt, ParticipantApplication app)` — 텔레그램 미설정이면 아무것도 안 함, 예외는 로그만.

메시지 형식:
```
🌸 새 신청서 ({행사 TabLabel})
{이름} · {성별} · {BirthYearLabel}
{연락처} / 카톡 {KakaoId}
{기존 참가자면 "기존 참가자"}
```

- [ ] Step 1: 실패 테스트 `BuildMessage_contains_name_event_and_returning_flag`
- [ ] Step 2–4: 실패 확인, 구현, PASS
- [ ] Step 5: Commit `feat: telegram notice for web applications`

### Task 6: 관리자 화면

**Files:**
- Modify: `Components/Pages/Applications.razor` (카드에 새 항목, "웹 신청"/"기존 참가자" 태그, 사진 썸네일 3개 → 새 탭 원본; 툴바에 "신청 페이지" 설정 카드)
- Modify: `Program.cs` — `GET /applications/file/{applicationId:int}/{kind}` (kind: `photo|employment|marriage`, Admin 전용, `Results.File(path, "image/jpeg"|"image/png")`, 응답 헤더 `Cache-Control: private, no-store`); `POST /applications/apply-settings` (Admin, `eventId, applyOpen, applyTitle, applyNotice`); `/applications/delete`에서 파일 3개 `ApplicationFileStore.Delete`
- Modify: `Services/ApplicationExcelExporter.cs` — 헤더/행에 카카오톡 ID, 최종학력, 참여경로, 신청동기, 자기소개, 기존 참가자(O/X), 신청 경로(web/수동) 추가

설정 카드: 신청 받기 토글, 제목 입력, 안내문 textarea(비어 있으면 기본 안내문 placeholder), 공개 링크 표시 + "링크 복사" 버튼, "미리보기" 링크.

- [ ] Step 1: 구현
- [ ] Step 2: 확인: (a) 설정 카드에서 신청 받기 켜기 → `/apply/{id}` 열림, 끄기 → 닫힘 안내 (b) Task 4 제출분이 카드에 모든 항목·사진과 함께 보임 (c) 로그아웃 상태로 사진 URL 열기 → 로그인 페이지 (d) 엑셀 다운로드에 새 열 (e) 신청서 삭제 → `private/applications` 파일 사라짐
- [ ] Step 3: Commit `feat: manage web applications in admin`

### Task 7: 마무리 검증

- [ ] `dotnet test` 전부 PASS, `dotnet build -c Release` 경고 신규 없음
- [ ] 모바일(375px)·데스크톱에서 `/apply/{id}` 스크린샷 확인
- [ ] `docker build` 가능하면 실행 (테스트 폴더 제외 확인), 불가하면 `dotnet publish -c Release`로 대체
