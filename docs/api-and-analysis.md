# PACE API & phân tích repo

## Kiến trúc (Clean Architecture)

```
PACE.API ──► PACE.Infrastructure ──► PACE.Application ──► PACE.Domain
   └──────────────────────────────────► PACE.Application
```

| Project | Nội dung | Phụ thuộc |
|---|---|---|
| `PACE.Domain` | `Entities/`, `Enums/` | không phụ thuộc gì |
| `PACE.Application` | `Services/` (CRUD), `Dtos/`, `Mappings/`, `Interfaces/Repositories/` (IGenericRepository, I*Repository, IUnitOfWork), `Common/` (exception, PagedResult, read model, ICurrentUserService, IUserImportReader), `Analysis/` (template, khớp API, dựng luồng, Mermaid + abstraction `IGitCloneService`, `IRepositoryScanner`, `ILanguageParser`) | Domain |
| `PACE.Infrastructure` | `Persistence/` (AppDbContext, Configurations, Migrations), `Repositories/` (GenericRepository, *Repository, UnitOfWork), `Analysis/` (git clone, quét repo, parser Roslyn + tree-sitter), `Identity/` (JWT), `Excel/` (ClosedXML) | Application |
| `PACE.API` | Controller, `GlobalExceptionHandler`, `CurrentUserService`, Swagger, `Program.cs` | Application, Infrastructure |

Service chỉ làm việc với interface trong Application; mọi truy vấn EF và thư viện ngoài nằm ở Infrastructure. DI: `AddApplication()` + `AddInfrastructure(configuration)`.

Migration:

```
dotnet ef migrations add <Ten> --project PACE.Infrastructure --startup-project PACE.API
dotnet ef database update --project PACE.Infrastructure --startup-project PACE.API
```

## Cấu hình (`appsettings.Development.json`)

```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "Jwt": { "Key": "chuỗi ngẫu nhiên >= 32 ký tự" },
  "Auth": { "SeedAdminEmails": ["email-admin@fpt.edu.vn"] },
  "Analysis": { "GitHubToken": "", "CloneTimeoutSeconds": 180, "MaxFlowDepth": 12, "MaxStepsPerFlow": 250 }
}
```

- `Auth:SeedAdminEmails`: API khởi động sẽ tạo các admin này nếu chưa có.
- `Analysis:GitHubToken`: chỉ cần khi repo sinh viên để private. Máy chạy API phải cài `git`.

## Đăng nhập

`POST /api/auth/login` `{ "email": "..." }` → `accessToken`. Trên Swagger bấm **Authorize** và dán token.
Hiện chỉ đăng nhập bằng email (chưa chốt B3: email / email+OTP / Google).

## Endpoint

| Bảng | Admin | Student |
|---|---|---|
| users | `GET/POST /api/users`, `GET/PUT/DELETE /api/users/{id}`, `POST /api/users/import` (Excel .xlsx: cột Email + cột Repo/Link GitHub, tuỳ chọn cột Tên nhóm) | `GET /api/auth/me` |
| analysis_runs | `POST /api/analysis-runs?force=false` (gửi link repo → tạo repo nếu chưa có rồi phân tích luôn), `GET /api/analysis-runs`, `GET/PUT/DELETE /api/analysis-runs/{id}` | `GET /api/analysis-runs/me` |
| businesses | `GET /api/businesses?analysisRunId=`, `GET/PUT /api/businesses/{id}` (đổi tên), `GET /api/businesses/{id}/flows` | |
| method_sources | `GET /api/method-sources?analysisRunId=&businessId=&language=&search=`, `GET /api/method-sources/{id}` | |
| business_flows, data_flow_edges | `GET /api/business-flows/{id}` (Mermaid + danh sách cạnh) | |
| rubric_criteria | `POST /api/rubric-criteria`, `PUT/DELETE /api/rubric-criteria/{id}` | `GET /api/rubric-criteria` |

Chưa làm (chờ chốt): generated_questions, student_answers, rubric_scores (sinh câu hỏi, nộp bài, feedback, chấm rubric).

## Phân tích repo (`POST /api/analysis-runs`)

1. Clone `--depth 1` (hỗ trợ link `/tree/<branch>/<thư mục>`), git không bao giờ hỏi mật khẩu, có timeout.
2. Đọc `template_business.json` trong repo (thư mục gốc hoặc thư mục con như `src/`; thiếu file → 400). Hỗ trợ 2 schema:
   - A: `{ "controller": "BudgetController", "method": "POST CreateBudget" }`
   - B: `{ "controller": "LocationController", "http_method": "GET", "function": "getAllProvinces", "path": "/api/location" }`
   Nghiệp vụ trùng tên (không phân biệt hoa thường) được gộp.
3. Nhận stack: `.csproj/.sln` → .NET, `pom.xml/build.gradle` → Spring Boot/Java, `package.json` backend → Node.js (bỏ qua frontend React/Vue/Angular..., test, migration, build output).
4. Parse:
   - C#: Roslyn + semantic model (một compilation cho cả repo), interface → implementation, MediatR `Send/Publish` → Handler, class lồng nhau (`Create.Handler`).
   - Java (Spring): tree-sitter; kiểu biến lấy từ field/tham số/biến cục bộ; `@GetMapping/@PostMapping/@RequestMapping`.
   - JS/TS: tree-sitter; Express (`router.get(...)`, `router.route('/').post(...)`, handler bọc `catchAsync(...)`), `require`/`import`, re-export qua `index.js`, NestJS (`@Controller`, `@Get`, DI qua constructor).
5. Khớp API → hàm điểm vào theo class + tên hàm (bỏ hậu tố `Async`, `Controller`, `Impl`), nếu không được thì theo `http_method + path`.
6. Dựng luồng từ hàm điểm vào: cạnh `Call` ("Truyền: tham số"), `Return` ("Trả ra: kiểu/giá trị"), `ConditionContext` (if/else, switch, ternary, vòng lặp, catch). Bỏ getter/setter tầm thường; giới hạn độ sâu/số bước.
7. Sinh Mermaid `flowchart TD`.
8. Ghi DB trong một transaction: xoá dữ liệu phân tích cũ của repo rồi ghi `method_sources`, `businesses`, `business_methods`, `business_flows`, `data_flow_edges`. Kết quả trả về kèm danh sách cảnh báo (API không khớp hàm, nghiệp vụ không có code, luồng bị cắt...).

Mỗi repo chỉ chạy một lần phân tích tại một thời điểm. Repo đã có câu hỏi sẽ bị chặn (409) trừ khi gửi `force=true`, vì xoá nghiệp vụ cũ kéo theo câu hỏi/câu trả lời (cascade).
