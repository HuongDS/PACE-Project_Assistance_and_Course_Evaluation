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
| analysis_runs | `POST /api/analysis-runs?force=false` (gửi link repo → tạo repo nếu chưa có rồi phân tích luôn), `GET /api/analysis-runs`, `GET/PUT/DELETE /api/analysis-runs/{id}`, `GET/PUT /api/analysis-runs/{id}/template` | `GET /api/analysis-runs/me` |
| businesses | `GET /api/businesses?analysisRunId=`, `GET/PUT /api/businesses/{id}` (đổi tên), `GET /api/businesses/{id}/flows` | |
| method_sources | `GET /api/method-sources?analysisRunId=&businessId=&language=&search=`, `GET /api/method-sources/{id}` | |
| business_flows, data_flow_edges | `GET /api/business-flows/{id}` (Mermaid + danh sách cạnh) | |
| rubric_criteria | `POST /api/rubric-criteria`, `PUT/DELETE /api/rubric-criteria/{id}` | `GET /api/rubric-criteria` |
| template nghiệp vụ | `POST /api/templates/generate`, `POST /api/templates/export`, `POST /api/templates/generate-from-excel[/download]` | |

Chưa làm (chờ chốt): generated_questions, student_answers, rubric_scores (sinh câu hỏi, nộp bài, feedback, chấm rubric).

## Tạo template nghiệp vụ (`POST /api/templates/generate`)

Dùng khi repo chưa có `template_business.json`. Luồng: **tạo bản nháp → admin chỉnh → chốt**.

1. `POST /api/templates/generate` `{ "repoUrl": "...", "useAi": true, "regenerate": false }`
   - `regenerate=false` và repo đã có template admin chốt trong DB → trả về template đó, không clone (`source = Confirmed`).
   - `regenerate=false` và repo có sẵn `template_business.json` → trả về nguyên file đó, `source = Repository`.
   - Ngược lại: clone, parse, liệt kê mọi endpoint (controller + http_method + function + path), rồi gom thành nghiệp vụ:
     - `useAi=true` và đã cấu hình `Gemini:ApiKey` → Gemini gom endpoint theo chức năng người dùng, đặt tên/mô tả tiếng Việt (`source = Ai`).
       Kết quả AI được kiểm tra: id không có trong repo bị bỏ, id trùng chỉ giữ lần đầu, endpoint bị AI bỏ sót được gom theo controller (đều có cảnh báo).
     - Không dùng AI, chưa cấu hình hoặc Gemini lỗi → mỗi controller là một nghiệp vụ (`source = Controller`).
   - Trả về `businesses` (đúng schema B), số nghiệp vụ/API và `warnings`. Bản nháp không ghi vào DB.
2. Admin chỉnh `businesses` trên giao diện (đổi tên, chuyển API giữa nghiệp vụ, xoá API).
3. Chốt (template được lưu vào `analysis_runs.BusinessTemplateJson`, `TemplateSource = Confirmed`):
   - `POST /api/analysis-runs` gửi kèm `"businesses": [...]` → phân tích bằng template này và lưu lại; hoặc
   - `PUT /api/analysis-runs/{id}/template` `{ "businesses": [...] }` → chỉ lưu (repo đã tồn tại), lần phân tích sau sẽ dùng; hoặc
   - `POST /api/templates/export` `{ "businesses": [...] }` → tải file `template_business.json` để commit vào repo.

### Tạo template cho nhiều repo từ file Excel

`POST /api/templates/generate-from-excel?useAi=true&regenerate=false` (multipart, field `file` = `.xlsx`, Admin).

- Cột link repo được nhận theo tiêu đề chứa `repo`, `github`, `link` hoặc `url`. Nếu file không có tiêu đề mà chỉ gồm danh sách link thì dòng đầu cũng được dùng. Ô dạng hyperlink (hiển thị tên, link thật nằm trong hyperlink) cũng được đọc. Các cột khác (Email, Tên nhóm...) bị bỏ qua nên dùng lại được file import user.
- Mỗi repo chạy như `POST /api/templates/generate` (cùng thứ tự: template đã chốt trong DB, rồi file trong repo, rồi tạo mới), tối đa 3 repo chạy song song, mỗi repo một scope DB riêng.
- Tối đa 50 repo mỗi lần; vượt quá trả 400 và không clone gì. Link trùng (không phân biệt hoa thường, bỏ `.git`) chỉ xử lý một lần, các dòng sau có `status = Skipped`.
- Lỗi ở một repo (link sai, repo private/không tồn tại, không có endpoint...) không làm hỏng các repo khác: repo đó có `status = Failed` và `error`.
- Kết quả: `{ total, succeeded, failed, skipped, items[{ row, repoUrl, status, error, template }] }`, `items` giữ đúng thứ tự dòng trong Excel; `template` giống response của `generate`. Không ghi gì vào DB.
- `POST /api/templates/generate-from-excel/download` nhận cùng tham số nhưng trả file `templates.zip`: mỗi repo thành công một thư mục `owner_repo/template_business.json`, kèm `_report.txt` ghi kết quả từng dòng. Muốn sửa rồi chốt từng repo thì dùng response JSON với `POST /api/analysis-runs`.

Cấu hình: `"Gemini": { "ApiKey": "", "Model": "gemini-3.1-flash-lite", "TimeoutSeconds": 120 }`. Chỉ gửi tên/route của endpoint lên Gemini, không gửi mã nguồn.

### Lưu template (migration `AddAnalysisRunBusinessTemplate`)

Bảng `analysis_runs` thêm 3 cột nullable: `BusinessTemplateJson` (jsonb), `TemplateSource` (`Repository` | `Confirmed`), `TemplateUpdatedAt`.
Migration chỉ thêm cột, không sửa/xoá dữ liệu cũ; repo cũ có giá trị NULL và vẫn đọc file trong repo như trước.
`Confirmed` được ưu tiên hơn file trong repo khi phân tích lại; `Repository` thì lần sau đọc lại file mới nhất trong repo.

## Phân tích repo (`POST /api/analysis-runs`)

1. Clone `--depth 1` (hỗ trợ link `/tree/<branch>/<thư mục>`), git không bao giờ hỏi mật khẩu, có timeout.
2. Lấy template theo thứ tự: `businesses` trong request → template đã chốt trong DB → `template_business.json` trong repo (thư mục gốc hoặc thư mục con như `src/`; thiếu file → 400). Hỗ trợ 2 schema:
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
8. Ghi DB trong một transaction: xoá dữ liệu phân tích cũ của repo rồi ghi `method_sources`, `businesses`, `business_methods`, `business_flows`, `data_flow_edges`, và lưu template đã dùng vào `analysis_runs` (`GET /api/analysis-runs/{id}/template` để xem). Kết quả trả về kèm danh sách cảnh báo (API không khớp hàm, nghiệp vụ không có code, luồng bị cắt...).

Mỗi repo chỉ chạy một lần phân tích tại một thời điểm. Repo đã có câu hỏi sẽ bị chặn (409) trừ khi gửi `force=true`, vì xoá nghiệp vụ cũ kéo theo câu hỏi/câu trả lời (cascade).
