# Checklist Đối Chiếu & Hoàn Thiện Hệ Thống AIPOA (PACE)

Tài liệu này tổng hợp kết quả đối chiếu trực tiếp giữa **Bản đăng ký đồ án AIPOA** và 2 kho mã nguồn hiện tại:
- **Backend (`PACE-Project_Assistance_and_Course_Evaluation`)**: .NET 9 Clean Architecture (`PACE.API`, `PACE.Application`, `PACE.Domain`, `PACE.Infrastructure`), PostgreSQL, Upstash Redis.
- **Frontend (`PACE-FE`)**: Next.js 14 (App Router), TypeScript, Tailwind CSS.

> [!IMPORTANT]
> **Đánh giá tổng quan hiện trạng:**
> Hệ thống hiện tại có nền tảng từ luồng phân tích tĩnh mã nguồn (`AnalysisRun` -> `Business` -> `BusinessFlow` / Mermaid Graph) và chấm câu trả lời tự luận bất đồng bộ. Gần đây nhóm đã thêm migration DB (`20261111122558_UpdateDbSchemaTo2026.cs`) và một số controller mỏng (`TeachingController`, `EvidenceController`, `ReportsController`), nhưng **phần lớn nghiệp vụ cốt lõi của đồ án AIPOA** (Phân tích lịch sử đóng góp Git/Jira theo thời gian, RAG với Vector DB, Phòng vấn đáp trực tuyến WebRTC + STT + Giám sát môi trường, AI điều phối hỏi đáp trực tiếp, và AI Teaching Assistant báo cáo 15 phút trước giờ học) **đang ở trạng thái Mock/Hardcode, chỉ có bảng DB rỗng, hoặc chưa được triển khai**.

---

## Tóm Tắt Những Phần Đã Làm & Hoạt Động Tốt ✅

### Backend (BE)
- [x] **Xác thực & Phân quyền cơ bản**: Đăng nhập Email/Password, Google OAuth (Student/Staff), đăng ký qua OTP (Upstash Redis + MailKit), Refresh Token ([AuthController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/AuthController.cs), [AuthService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/AuthService.cs)).
- [x] **Quản lý Danh sách Lớp & Sinh viên**: Xem danh sách lớp, chi tiết lớp, thêm/xóa sinh viên theo MSSV, Import/Export danh sách sinh viên bằng file Excel `.xlsx` có kiểm tra bảo mật chống ZIP bomb ([ClassController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/ClassController.cs), [ExcelUploadGuard.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Excel/ExcelUploadGuard.cs)).
- [x] **Quản lý Người dùng (Admin)**: CRUD người dùng và import hàng loạt qua Excel ([UsersController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/UsersController.cs)).
- [x] **Phân tích Tĩnh Mã nguồn (Static Code Scanner) & Vẽ Luồng Nghiệp Vụ**: Clone repo (`--depth 2`), quét mã nguồn C# (Roslyn), Java & JS/TS (Tree-sitter), gom nhóm API thành các `Business` (bằng Gemini hoặc heuristic) và dựng đồ thị luồng gọi hàm Mermaid ([RepoAnalysisService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/RepoAnalysisService.cs), [RepositoryScanner.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Analysis/RepositoryScanner.cs)).
- [x] **Upload Đặc tả Draw.io XML & Sinh câu hỏi theo Luồng Nghiệp vụ**: Parse file Draw.io XML và gọi OpenAI sinh câu hỏi tình huống dựa trên code hoặc đồ thị Mermaid ([QuestionsController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/QuestionsController.cs), [QuestionGenerationService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/QuestionGenerationService.cs)).
- [x] **Audit Log cơ bản**: Tự động ghi nhận sự kiện thêm/sửa/xóa một số bảng chính trong `SaveChangesAsync` ([AppDbContext.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Persistence/AppDbContext.cs)) và xem danh sách qua `GET /api/audit-events`.

### Frontend (FE)
- [x] **Luồng Đăng nhập / Đăng ký / Phân quyền Layout**: Đăng nhập, Google OAuth, đăng ký 2 bước có kiểm tra email + OTP, tự động refresh token và chia 3 nhóm route `(student)`, `(lecturer)`, `(admin)` ([AuthContext.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/contexts/AuthContext.tsx), [AppShell.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/components/app/AppShell.tsx)).
- [x] **Quản lý Lớp học & Sinh viên (Giảng viên & Admin)**: Xem lớp, tìm kiếm/phân trang sinh viên, thêm/xóa sinh viên, Import/Export Excel ([ClassManager.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/components/app/ClassManager.tsx)).
- [x] **Quản lý Repository & Xem kết quả phân tích API/Business (Sinh viên, Giảng viên, Admin)**: Thêm/sửa/xóa GitHub repo, chạy phân tích tĩnh, xem danh sách thành viên nhóm và danh sách Business/API trích xuất được ([RepositoryManager.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/components/app/RepositoryManager.tsx), [projects/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/projects/page.tsx)).

---

## PHẦN 1: CHECKLIST CÁC MỤC CẦN SỬA (BUGS, HARDCODE, LỖ HỔNG & XUNG ĐỘT THIẾT KẾ) 🔧

### 1.1. Backend (BE) — Cần sửa ngay
- [x] **Xóa tài khoản Backdoor hardcode trong `AuthService`**:
  - Đã xóa toàn bộ logic tài khoản backdoor (lecturer, student, manager) trong `AuthService.cs`, chỉ cho phép xác thực người dùng thật qua DB và BCrypt / Admin Credentials.
- [x] **Gỡ bỏ thông tin nhạy cảm (Secret) bị lộ trong `appsettings.json`**:
  - Đã gỡ bỏ chuỗi kết nối và mật khẩu Upstash Redis khỏi `appsettings.json` và xóa hardcode fallback trong `UpstashRedisOtpCacheService.cs`. Ném ngoại lệ khi thiếu cấu hình.
- [x] **Sửa lỗi cấu hình AI Provider và tên Model không hợp lệ**:
  - Đã hiện thực `GeminiQuestionLlmClient`, cấu hình DI động theo `QuestionGeneration:Provider` ("Gemini" vs "OpenAI"), và cập nhật tên model hợp lệ `gpt-4o-mini`.
- [x] **Bật lại và cấu hình đúng phân quyền `[Authorize]` cho `QuestionsController`**:
  - Đã bật `[Authorize(Roles = Roles.Staff)]` cho `QuestionsController.cs`.
- [ ] **Sửa lỗi xuất PDF Báo cáo bị mất tiếng Việt (corrupt thành dấu `?`) và thiếu kết luận AI**:
  - Trong [ReportsController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/ReportsController.cs#L17-L40), hàm `ReportPdf.Create` đang lọc bỏ mọi ký tự non-ASCII (`c is >= ' ' and <= '~' ? c : '?'`) làm hỏng toàn bộ tên sinh viên và nhận xét tiếng Việt; đồng thời đang hardcode `gradeLetter = null` và chuỗi `"Grade letters and AI conclusions are unavailable."`.
- [ ] **Hợp nhất các bảng Database bị trùng lặp và rời rạc**:
  - Hiện có **2 mô hình nhóm/dự án song song**: `CoursesController` dùng `Course` -> `CourseProject` -> `AnalysisRun` ([TeachingController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/TeachingController.cs#L61)), trong khi `ClassRepository` dùng `Class` -> `Group` -> `GroupMember` -> `AnalysisRun` ([ClassRepository.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Repositories/ClassRepository.cs)).
  - Hiện có **2 mô hình phiên vấn đáp song song**: `AssessmentSession` / `SessionStudent` trong [Teaching.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Domain/Entities/Teaching.cs) và `OralExam` / `StudentOralSession` / `OralQuestion` / `StudentOralAnswer` trong [OralExam.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Domain/Entities/OralExam.cs) (nhóm bảng `OralExam` đã tạo trong DB nhưng chưa được sử dụng ở bất kỳ service/controller nào).
- [ ] **Loại bỏ cơ chế clone nông (`--depth 2`) khi thu thập bằng chứng lịch sử Git**:
  - [GitCloneService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Analysis/GitCloneService.cs) và [EvidenceController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/EvidenceController.cs#L49-L68) chỉ clone `--depth 2` và chạy `git log -1`, khiến API lấy diff của mọi commit khác ngoài `HEAD` đều trả về lỗi `404 Commit outside shallow evidence snapshot`, và hardcode `contributions = new object[0]`.
- [ ] **Dọn dẹp logic khảo sát cũ (A/B Testing 4 câu hỏi ngẫu nhiên) hoặc tách riêng khỏi luồng Vấn đáp**:
  - [StudentSubmissionService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/StudentSubmissionService.cs#L184-L262) đang ép buộc trả lời đúng 4 câu hỏi và trộn ngẫu nhiên 4 định dạng feedback (`Sandwich`, `Rubric`, `Explainable`, `Researching`) phục vụ khảo sát nghiên cứu trước đây, không khớp với luồng chấm theo Rubric của buổi vấn đáp trực tuyến.
- [ ] **Hiện thực hóa hoặc xóa class rỗng `EvaluationLlmService`**:
  - [EvaluationLlmService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/EvaluationLlmService.cs#L10-L12) hiện là một class trống hoàn toàn.

### 1.2. Frontend (FE) — Cần sửa ngay
- [ ] **Bổ sung bảo vệ route Admin bị thiếu trong Middleware**:
  - Trong [middleware.ts](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/middleware.ts), mảng `ADMIN_ROUTES` mới chỉ khai báo `["/system-audit", "/users"]`, bỏ sót `/classes` và `/repositories` thuộc phân hệ `(admin)`.
- [ ] **Thay thế dữ liệu Mock hardcode ở trang Dự án của Sinh viên**:
  - Trong [projects/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/projects/page.tsx#L278-L356), hai tab **"Nhiệm vụ" (`tasks`)** và **"Deadline" (`deadlines`)** đang dùng mảng dữ liệu giả định (`sampleTasks`, `sampleDeadlines`) cố định mà chưa gọi API thực tế.
- [ ] **Kết nối FE với các API BE đã có sẵn nhưng FE chưa gọi**:
  - BE đã có các API trong `TeachingController`, `EvidenceController`, `ReportsController`, `UsersController` (`/api/courses`, `/api/assessments/sessions`, `/api/evidence/projects/...`, `/api/reports/...`, `/api/audit-events`, `/api/users`), nhưng FE mới chỉ viết service gọi 3 nhóm API (`auth.ts`, `classes.ts`, `analysisRuns.ts`).

---

## PHẦN 2: CHECKLIST CÁC TÍNH NĂNG ĐANG LÀM DỞ / CHỈ CÓ KHUNG (CẦN HOÀN THIỆN) 🚧

### 2.1. Backend (BE) — Cần hoàn thiện
- [ ] **Viết CRUD Services & Controllers cho các bảng đã tạo trong Migration `UpdateDbSchemaTo2026`**:
  - Các bảng `Semester`, `ClassSchedule`, `ClassSession`, `Attendance`, `Room`, `TimeSlot`, `Group`, `GroupMember`, `StudentProfile`, `LecturerProfile`, `OralExam`, `OralQuestion`, `StudentOralSession`, `StudentOralAnswer` đã có trong DB ([20261111122558_UpdateDbSchemaTo2026.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Persistence/Migrations/20261111122558_UpdateDbSchemaTo2026.cs)) nhưng hiện **chưa có API thêm/sửa/xóa** (mới chỉ nạp dữ liệu qua script Python `scripts/seed_fe_demo.py`).
  - Bổ sung API tạo/cập nhật/xóa Lớp học (`ClassController` hiện chỉ có `GET` lớp và quản lý danh sách sinh viên).
- [ ] **Hoàn thiện API Chi tiết Khóa học & Cấu hình Rubric Phiên đánh giá**:
  - Thay thế các giá trị đang hardcode rỗng trong [TeachingController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/TeachingController.cs#L61-L113):
    - `milestones = new object[0]` -> trả về danh sách Milestones thực tế.
    - `levels = new object[0]` trong tiêu chí Rubric -> hỗ trợ đầy đủ các mức điểm (Rubric levels/bands) và mô tả (grading guide).
    - Trạng thái kết nối sinh viên (`isAudioActive = false, isVideoActive = false, connectionQuality = "disconnected"`) -> kết nối với trạng thái WebRTC/SignalR thực tế.
- [ ] **Mở rộng Phân tích AST/CFG (`/api/evidence/projects/{projectId}/ast-cfg`)**:
  - Hiện tại [EvidenceController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/EvidenceController.cs#L79-L104) chỉ hỗ trợ C#, báo lỗi `422` nếu hàm có tham chiếu kiểu dữ liệu ngoài hàm, và đang để `cyclomaticComplexity = null`. Cần tính toán độ phức tạp Cyclomatic và hỗ trợ thêm các ngôn ngữ mục tiêu trong phạm vi MVP (Java, JS/TS).
- [ ] **Hoàn thiện Health Check thực tế cho `/health`**:
  - Trong [Program.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Program.cs#L99), `builder.Services.AddHealthChecks()` chưa đăng ký kiểm tra kết nối PostgreSQL, Upstash Redis, Git CLI, và các AI Providers.

### 2.2. Frontend (FE) — Cần thay thế các trang `<ComingSoon />` bằng giao diện thực
Hiện có **5 trang chính** trên FE đang chỉ hiển thị component giữ chỗ `<ComingSoon />` ([ComingSoon.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/components/app/ComingSoon.tsx)):
- [ ] **Phòng vấn đáp (Sinh viên)**: [src/app/(student)/assessment-room/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/assessment-room/page.tsx).
- [ ] **Phiên vấn đáp (Giảng viên)**: [src/app/(lecturer)/assessments/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/assessments/page.tsx).
- [ ] **Báo cáo (Giảng viên)**: [src/app/(lecturer)/reports/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/reports/page.tsx) (cần nối với `/api/reports/{id}` và `/api/reports/{id}/export/pdf`).
- [ ] **Quản lý Người dùng (Admin)**: [src/app/(admin)/users/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(admin)/users/page.tsx) (cần nối với `/api/users` và `/api/users/import`).
- [ ] **Nhật ký Hệ thống (Admin)**: [src/app/(admin)/system-audit/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(admin)/system-audit/page.tsx) (cần nối với `/api/audit-events`).

---

## PHẦN 3: CHECKLIST CÁC TÍNH NĂNG CHƯA LÀM (CẦN XÂY DỰNG MỚI) 🚀

### 3.1. Quản lý Học vụ, Nhóm, Đề tài, Milestone & Đóng băng bài nộp (Frozen Snapshot)
- **Backend (BE):**
  - [ ] Thiết kế bảng và API quản lý **Milestones & Deadlines** cho từng khóa học/lớp học (trọng số, hạn nộp, danh sách sản phẩm yêu cầu, độ phủ CLO/Topic, hướng dẫn chấm và chỉ dẫn hỏi đáp riêng cho AI).
  - [ ] Xây dựng luồng **Sinh viên tự tạo nhóm & đăng ký đề tài**: mời/chọn thành viên, bầu nhóm trưởng, chọn đề tài có sẵn hoặc đề xuất đề tài mới, gửi yêu cầu phê duyệt và yêu cầu đổi thành viên/đề tài.
  - [ ] Xây dựng cơ chế **Nộp bài theo Milestone & Đóng băng bằng chứng (Frozen Evidence Snapshot)**:
    - Cho phép upload gói tài liệu dự án (SRS, thiết kế, DB schema/migrations, API docs) kèm mã `commit SHA`.
    - Khóa (freeze) trạng thái mã nguồn, tài liệu và trạng thái Jira/GitHub tại thời điểm trước buổi đánh giá để thay đổi sau đó không làm sai lệch căn cứ chấm.
- **Frontend (FE):**
  - [ ] Giao diện Sinh viên xem danh sách đề tài, tạo nhóm, mời thành viên, đăng ký/đề xuất đề tài và gửi yêu cầu thay đổi.
  - [ ] Giao diện Giảng viên cấu hình Milestones, Deadlines, Rubric, CLO và duyệt/từ chối đăng ký nhóm & đề tài.
  - [ ] Giao diện Sinh viên nộp gói sản phẩm theo từng Milestone (chọn commit SHA, upload tài liệu dự án) và xem lịch sử nộp bài.

### 3.2. Tích hợp GitHub/Jira & Phân tích Đóng góp / Tiến hóa Code (Contribution Intelligence)
- **Backend (BE):**
  - [ ] **Tích hợp Jira API (Read-only)**: Lấy danh sách issues, trạng thái, lịch sử chuyển trạng thái, người được giao (assignee) và sprint.
  - [ ] **Ánh xạ định danh (Identity Mapping)**: Liên kết tài khoản Sinh viên trong hệ thống với username/email GitHub và tài khoản Jira.
  - [ ] **Phân tích lịch sử Git đầy đủ & Code Evolution**:
    - Phân tích toàn bộ lịch sử commit và actual diffs ở mức file/class/method theo thời gian.
    - Phân biệt rõ **đóng góp lịch sử** (người viết code ban đầu) và **hiện trạng mã nguồn** (người sửa đổi/viết lại sau đó).
    - Chụp snapshot định kỳ lịch sử phát triển sau khi dự án được duyệt.
  - [ ] **Đối chiếu đa nguồn & Phát hiện bất thường (Anomaly Detection)**:
    - Liên kết ngữ nghĩa giữa Jira task <-> Pull Request <-> Commit <-> Code Diff.
    - Phát hiện và gắn cờ các trường hợp bất thường: đẩy lượng lớn code vào phút chót (large late code introduction), lịch sử bị xóa/squash/rebase/force-push, dùng chung tài khoản commit, hoặc code không có task Jira tương ứng.
  - [ ] **Tổng hợp Hồ sơ Bằng chứng Đóng góp (`Contribution Evidence Profile`)** cho từng sinh viên.
- **Frontend (FE):**
  - [ ] Giao diện Sinh viên kết nối/ánh xạ tài khoản GitHub và Jira cá nhân.
  - [ ] Dashboard cho Giảng viên theo dõi tiến độ nhóm: lịch sử Jira task, hoạt động GitHub, biểu đồ tiến hóa code (Code Evolution), Hồ sơ đóng góp (`Contribution Evidence Profile`) của từng sinh viên, và danh sách cảnh báo lịch sử phát triển bất thường.

### 3.3. Xử lý Bằng chứng, Cơ sở Tri thức Dự án & Hệ thống RAG
- **Backend (BE):**
  - [ ] **Bộ lọc file & Quét che giấu thông tin nhạy cảm (Secret Redaction Pipeline)**:
    - Kiểm tra định dạng/dung lượng file, loại bỏ thư mục build/dependencies, tự động quét và che giấu API keys, mật khẩu, token trong code/config trước khi lưu hoặc gửi sang LLM.
    - Lưu log các file được chấp nhận, bị bỏ qua hoặc lỗi.
  - [ ] **Trích xuất Tài liệu & Đối chiếu chéo (Cross-Source Conflict Detection)**:
    - Đọc nội dung tài liệu nộp (DOCX/PDF/Markdown, SQL/DB schema, OpenAPI).
    - Tự động đối chiếu các tuyên bố trong tài liệu với source code và DB schema thực tế; bảo lưu và xuất danh sách các điểm mâu thuẫn cho giảng viên.
  - [ ] **Provenance-Aware Chunking & Vector Database Indexing (RAG)**:
    - Chia nhỏ source code, tài liệu, DB schema và lịch sử Git/Jira kèm metadata nguồn gốc (project, submission version, đường dẫn file, tên hàm/class, trang/mục tài liệu).
    - Tạo embeddings và đánh chỉ mục vào **Vector Database** (chạy ngầm bất đồng bộ trước buổi đánh giá).
    - Xây dựng bộ truy xuất RAG lấy đúng ngữ cảnh liên quan đến sinh viên, tiêu chí Rubric, lịch sử câu hỏi và chỉ dẫn của giảng viên (thay vì nhồi toàn bộ code vào prompt như hiện tại).

### 3.4. Hệ thống AI Hỗ trợ Vấn đáp (AI-Assisted Oral Assessment)
- **Backend (BE):**
  - [ ] **Sinh câu hỏi ứng viên 2 tầng (Project-wide & Individual Contribution Verification)**:
    - Cập nhật prompt và pipeline sinh câu hỏi để vừa tạo câu hỏi hiểu biết chung toàn dự án, vừa tạo câu hỏi **xác minh đóng góp cá nhân** dựa trên task Jira và lịch sử code diff của riêng từng sinh viên (kèm tiêu chí Rubric, độ khó, bằng chứng trích dẫn và lý do).
  - [ ] **Tích hợp Speech-to-Text (STT) & Phân tách người nói (Speaker Attribution)**:
    - Nhận luồng âm thanh trả lời của sinh viên, chuyển thành văn bản có gắn timestamp và hỗ trợ giảng viên xác nhận/sửa tên người nói khi có nhiều giọng nói.
  - [ ] **Phân tích câu trả lời trực tiếp & Điều hướng thích ứng (Adaptive Questioning)**:
    - Sau mỗi câu trả lời, LLM phân tích độ chính xác, chiều sâu lập luận, độ khớp với bằng chứng và độ phủ Rubric, từ đó đề xuất hành động tiếp theo: `CLARIFY`, `PROBE`, `DEEPEN`, `REDIRECT`, `NEXT_TOPIC`, hoặc `NEXT_STUDENT`.
    - Theo dõi độ phủ (Coverage tracking) và cảnh báo nếu một sinh viên hoặc tiêu chí Rubric chưa được hỏi đủ.
  - [ ] **Gợi ý chấm điểm Rubric có trích dẫn bằng chứng (Evidence-Grounded Rubric Assessment)**:
    - Tự động gợi ý mức điểm/nhận xét theo từng tiêu chí Rubric, gắn kèm link trích dẫn đến timestamp câu trả lời của sinh viên và đoạn code/tài liệu/Jira/Git commit liên quan (đánh dấu rõ nếu thiếu bằng chứng).
- **Frontend (FE):**
  - [ ] **Giao diện Điều khiển Vấn đáp cho Giảng viên (Lecturer Live-Assessment Cockpit)**:
    - Khung gợi ý câu hỏi từ AI (hiển thị câu hỏi chung & câu hỏi cá nhân, lý do, bằng chứng); các nút chọn hỏi, sửa câu hỏi, bỏ qua, yêu cầu sinh thêm câu hỏi/câu hỏi phụ (follow-up), hoặc nhập câu hỏi thủ công.
    - Khung hiển thị bản gỡ băng thời gian thực (Real-time STT Transcript) có chỉnh sửa người nói.
    - Khung hiển thị phân tích câu trả lời tức thì của AI, thẻ gợi ý hành động tiếp theo (`CLARIFY`, `PROBE`...) và thanh theo dõi độ phủ Rubric/sinh viên.
  - [ ] **Giao diện Duyệt kết quả, Đối chiếu Bằng chứng & Chốt điểm**:
    - Cho phép Giảng viên bấm vào từng nhận định chấm điểm của AI để xem đối chiếu song song đoạn transcript/video trả lời của sinh viên và đoạn code/tài liệu/commit diff được trích dẫn.
    - Cho phép chỉnh sửa/ghi đè (override) điểm của AI, thêm ghi chú, chốt điểm và công bố cho sinh viên.
  - [ ] **Giao diện Xem kết quả & Báo cáo tiến độ (Sinh viên & Giảng viên)**:
    - Trang Sinh viên xem điểm quá trình đã công bố và nhận xét chi tiết.
    - Trang Giảng viên xem và xuất Báo cáo Phiên đánh giá (Assessment Report) và Báo cáo Tiến độ Học kỳ (Semester Progress Report) hỗ trợ đầy đủ tiếng Việt.

### 3.5. Phòng Vấn đáp Trực tuyến, Truyền luồng WebRTC & Giám sát Môi trường
- **Backend (BE):**
  - [ ] **Quản lý Hàng đợi Phòng chờ (Waiting Queue) & Khôi phục Phiên (Session Persistence)**:
    - Quản lý trạng thái sinh viên vào hàng đợi, kiểm tra điều kiện môi trường trước khi cho phép giảng viên Admit vào phòng, và lưu trạng thái phiên liên tục để khôi phục ngay nếu rớt mạng.
  - [ ] **Tích hợp Signaling / Media Server (WebRTC) & Lưu trữ bản ghi**:
    - Hỗ trợ truyền luồng Webcam, Microphone, Screen Share từ sinh viên sang giảng viên và lưu trữ file ghi hình/âm thanh theo chính sách lưu trữ.
  - [ ] **Ghi nhận Sự kiện Giám sát Môi trường & Phân tích AI (OCR / Audio Monitoring)**:
    - API nhận và lưu log các sự kiện trình duyệt có gắn timestamp: `TAB_FOCUS_LOST/GAINED`, `WINDOW_BLURRED/FOCUSED`, `SCREEN_CAPTURE_INTERRUPTED`, `MICROPHONE_INTERRUPTED`.
    - Tích hợp mô-đun phát hiện giọng nói phụ (secondary voices), khoảng lặng kéo dài (prolonged silence) và nhận diện chữ bất thường trên màn hình qua **OCR** để gắn cờ sự kiện cho giảng viên xem lại.
- **Frontend (FE):**
  - [ ] Cài đặt thư viện kết nối thời gian thực (WebRTC / LiveKit / SignalR / Socket) vào `PACE-FE`.
  - [ ] **Màn hình Kiểm tra Môi trường (Environment Integrity Check) & Phòng chờ (Sinh viên)**:
    - Kiểm tra quyền Microphone, Webcam và bắt buộc chia sẻ toàn màn hình trước khi vào hàng đợi.
  - [ ] **Giao diện Phòng thi Trực tuyến (Sinh viên)**:
    - Phát luồng màn hình + webcam + mic, xem câu hỏi hiện tại, mở code/tài liệu để trình bày/demo, và bộ lắng nghe sự kiện chuyển tab/mất focus/ngắt chia sẻ màn hình gửi về BE.
  - [ ] **Khung Xem Stream & Timeline Giám sát (Giảng viên)**:
    - Xem trực tiếp màn hình & webcam của sinh viên, tua lại video theo mốc thời gian (timestamp) của từng câu trả lời hoặc từng sự kiện cảnh báo môi trường/OCR/âm thanh.

### 3.6. Mô-đun Trợ giảng AI (AI Teaching Assistant)
- **Backend (BE):**
  - [ ] Xây dựng Background Scheduler / Cronjob chạy định kỳ trước mỗi ca học **15 phút** (dựa trên lịch `ClassSchedule` / `TimeSlot`):
    - Tổng hợp danh sách các task trễ hạn hoặc chưa hoàn thành của từng nhóm.
    - Đánh giá giai đoạn hiện tại và mức độ tiến độ chung của dự án.
    - Phát hiện và cảnh báo các nhóm không hoạt động trên hệ thống (System inactivity alerts).
    - Gửi thông báo/bản tin (Briefing) cho giảng viên qua Web UI và Email.
- **Frontend (FE):**
  - [ ] Giao diện hiển thị Bản tin Trợ giảng AI (Pre-class AI Briefing) trên Dashboard của Giảng viên trước giờ lên lớp.

### 3.7. Phân hệ Quản trị Hệ thống (System Administration)
- **Backend (BE):**
  - [ ] Xây dựng bảng cấu hình và API cho phép Admin thiết lập động: định dạng file cho phép, giới hạn dung lượng upload, thời gian lưu trữ dữ liệu (retention policy), cấu hình AI/LLM/STT providers và chính sách giám sát.
  - [ ] Xây dựng hệ thống quản lý Background Jobs (theo dõi tiến trình clone repo, phân tích AST, tạo embeddings) và API thống kê dung lượng lưu trữ (Storage usage), lỗi tích hợp (Integration failures).
- **Frontend (FE):**
  - [ ] Hoàn thiện trang Quản lý Người dùng & Phân quyền (`/users`).
  - [ ] Xây dựng trang Cấu hình Hệ thống & Tích hợp (File limits, Retention policy, AI/GitHub/Jira credentials).
  - [ ] Hoàn thiện trang Giám sát Hệ thống & Audit Logs (`/system-audit`): hiển thị sức khỏe các service, trạng thái background jobs, dung lượng lưu trữ và bộ lọc tra cứu nhật ký hệ thống.

---

## PHẦN 4: CHECKLIST DỊCH VỤ BÊN THỨ 3, HẠ TẦNG & TÀI LIỆU ĐỒ ÁN 🌐

### 4.1. Đăng ký & Cấu hình Dịch vụ bên thứ 3
- [ ] **Vector Database**: Cài đặt tiện ích mở rộng `pgvector` ngay trên PostgreSQL hiện có (khuyến nghị vì gọn nhẹ, đồng bộ với EF Core) hoặc đăng ký Qdrant Cloud / Pinecone để lưu trữ embeddings cho RAG.
- [ ] **Dịch vụ Speech-to-Text (STT)**: Đăng ký API nhận diện giọng nói hỗ trợ tiếng Việt & timestamps (ví dụ: OpenAI Whisper / Realtime API, Deepgram, hoặc Google Cloud Speech-to-Text).
- [ ] **Dịch vụ WebRTC / Media Server**: Đăng ký LiveKit Cloud / Agora / Daily.co (hoặc cấu hình STUN/TURN server + SignalR) để truyền và ghi lại luồng màn hình, webcam, âm thanh.
- [ ] **Dịch vụ OCR & Audio Analysis**: Tích hợp Tesseract/PaddleOCR (hoặc Google Cloud Vision /gpt-4o-mini vision) để quét văn bản bất thường trên màn hình và thư viện VAD/diarization để phát hiện giọng nói thứ hai.
- [ ] **Jira Atlassian Developer App & GitHub OAuth/App**: Đăng ký ứng dụng trên Atlassian Developer Console và GitHub Developer Settings để lấy thông tin kết nối Read-only cho các nhóm sinh viên.
- [ ] **Object Storage (Lưu trữ Video/File nộp)**: Cấu hình Cloudflare R2, AWS S3, hoặc MinIO để lưu trữ các bản ghi màn hình/audio và gói tài liệu nộp theo Milestone.

### 4.2. Bộ Tài liệu Đồ án & Kiểm thử (Deliverables theo mục 3.2.c & 3.2.e)
- [ ] **Tài liệu Đặc tả & Thiết kế**:
  - [ ] User Requirements & Software Requirements Specification (URS & SRS).
  - [ ] Use Case Model & Detailed Workflows.
  - [ ] Domain/Data Model & Relational Database Design (sau khi đã chuẩn hóa các bảng bị trùng lặp).
  - [ ] System Architecture Design & Detailed Design.
  - [ ] Project Evidence & Contribution Intelligence Design.
  - [ ] AI System Design (RAG, Question Planning, Adaptive Follow-up, Rubric Assessment, Citations & Guardrails).
  - [ ] Web-Based Assessment Monitoring Design.
  - [ ] API / Integration Documentation.
- [ ] **Kiểm thử & Đánh giá (QA & Pilot Evaluation)**:
  - [ ] Xây dựng bộ Test Cases kiểm thử chất lượng AI (độ liên quan của câu hỏi, độ bám sát bằng chứng, độ chính xác của trích dẫn, tỷ lệ ảo giác và mức độ đồng thuận chấm điểm với giảng viên).
  - [ ] Kiểm thử hiệu năng (độ trễ sinh câu hỏi và phân tích câu trả lời trực tiếp), bảo mật (RBAC, che giấu secret) và khả năng khôi phục phiên khi mất kết nối.
  - [ ] Hoàn thiện Test Plan, Test Report, Deployment Guide và User Manual.
