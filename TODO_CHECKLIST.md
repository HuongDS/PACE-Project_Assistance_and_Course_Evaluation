# Checklist Đối Chiếu & Tiến Độ Hoàn Thiện Hệ Thống AIPOA (PACE)

*Cập nhật kiểm tra thực tế từ mã nguồn nhánh `feature/part1-bugfixes-hardening` (14/11/2026)*

> [!IMPORTANT]
> **Tổng kết tiến độ thực tế sau khi kiểm tra code & git log:**
> 1. **PHẦN 1.1 (Backend — Cần sửa ngay):**
>    - **Đã xong hoàn toàn (6/9 mục):** Xóa backdoor `AuthService`, gỡ lộ Redis secret, DI động Gemini/OpenAI + đổi model `gpt-4o-mini`, bật `[Authorize]` cho `QuestionsController`, bỏ ép 4 câu khảo sát ngẫu nhiên ở `StudentSubmissionService`, và xóa class rỗng `EvaluationLlmService`.
>    - **Đang làm dở / Cần sửa thêm (3/9 mục):**
>      - ⚠️ **Xuất PDF (`ReportsController.cs`)**: Đã bỏ lọc ASCII và tính `gradeLetter`, nhưng vẫn tạo PDF thô với font `/BaseFont /Courier` (Type1 ASCII) nên mở bằng trình đọc PDF vẫn bị lỗi font tiếng Việt.
>      - 🚨 **Hợp nhất bảng DB (`Teaching.cs` / `TeachingConfiguration.cs`)**: Đã thêm cột khóa ngoại `OralExamId` (`oral_exam_id`) vào `AssessmentSession` nhưng **chưa tạo EF Core Migration**, sẽ gây lỗi crash SQL `column s.oral_exam_id does not exist` khi gọi API phiên vấn đáp!
>      - ⚠️ **Lịch sử Git (`EvidenceController.cs`)**: Đã bỏ `--depth 2` và lấy 50 commits, nhưng 49 commits trước `HEAD` vẫn bị gán cứng `additions = 0, deletions = 0` và `contributions = new object[0]` vẫn rỗng.
> 2. **PHẦN 1.2 (Frontend — Cần sửa ngay):**
>    - **Đã xong (1/3 mục):** Bảo vệ route Admin trong `middleware.ts`.
>    - **Làm dở (2/3 mục):** Đã xóa mảng mock ở `projects/page.tsx` (tab `deadlines` đã nối API thật, nhưng tab `tasks` đang tự sinh giả lập ở client từ danh sách API của `AnalysisRun`); đã viết đủ 6 file service trong `src/lib/` nhưng còn 2 service (`courses.ts`, `evidence.ts`) chưa được gắn vào giao diện.
> 3. **PHẦN 2 (Hoàn thiện các tính năng khung):**
>    - **FE (2.2):** **Đã hoàn thành 5/5 trang `<ComingSoon />`** (`assessment-room`, `assessments`, `reports`, `users`, `system-audit` đều đã có giao diện thật nối API). Tuy nhiên trang `/courses` của Giảng viên (`src/app/(lecturer)/courses/page.tsx`) lại bị đổi sang `<ComingSoon />` trong commit `3f9873c`.
>    - **BE (2.1):** Mới làm một phần mục 2.1.1 (thêm CRUD `Semester` và Create/Import `Class`), còn 3 mục còn lại chưa làm.
> 4. **PHẦN 3 & PHẦN 4 (Các tính năng cốt lõi xây dựng mới):**
>    - Ngoài trang `/users` (mục 3.7 FE) đã hoàn thiện, **toàn bộ các module lớn ở Phần 3 (3.1 -> 3.7) và Phần 4 vẫn chưa bắt đầu**.

---

## PHẦN 1: CHECKLIST CÁC MỤC CẦN SỬA (BUGS, HARDCODE, LỖ HỔNG & XUNG ĐỘT THIẾT KẾ) 🔧

### 1.1. Backend (BE) — Đã xong 6/9 mục, còn 3 mục cần hoàn thiện nốt
- [x] **Xóa tài khoản Backdoor hardcode trong `AuthService`** *(Commit `1c4f533`)*:
  - Đã xóa toàn bộ tài khoản backdoor (`lecturer`, `student`, `manager`) trong [AuthService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/AuthService.cs).
- [x] **Gỡ bỏ thông tin nhạy cảm (Secret) bị lộ trong `appsettings.json`** *(Commit `1c4f533`)*:
  - Đã xóa chuỗi kết nối Upstash Redis khỏi [appsettings.json](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/appsettings.json) và xóa fallback hardcode trong [UpstashRedisOtpCacheService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Services/UpstashRedisOtpCacheService.cs).
- [x] **Sửa lỗi cấu hình AI Provider và tên Model không hợp lệ** *(Commit `1c4f533`)*:
  - Đã hiện thực [GeminiQuestionLlmClient.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/AI/GeminiQuestionLlmClient.cs), resolve DI động theo `QuestionGeneration:Provider` trong [DependencyInjection.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/DependencyInjection.cs) và cập nhật model `gpt-4o-mini`.
- [x] **Bật lại và cấu hình đúng phân quyền `[Authorize]` cho `QuestionsController`** *(Commit `1c4f533`)*:
  - Đã bật `[Authorize(Roles = Roles.Staff)]` cho [QuestionsController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/QuestionsController.cs).
- [x] **Sửa lỗi xuất PDF Báo cáo bị mất tiếng Việt và thiếu kết luận AI**:
  - Đã thay thế logic tạo PDF thô bằng thư viện `PdfSharpCore`, nhúng font TrueType Unicode (Arial) hỗ trợ đầy đủ tiếng Việt có dấu. Báo cáo PDF có đầy đủ định dạng chuẩn (`%PDF-`), điểm số, điểm chữ (`gradeLetter`), nhận xét từng tiêu chí Rubric, kết luận tổng hợp `aiConclusions` và lịch sử sự kiện Audit trong [ReportsController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/ReportsController.cs). Unit tests pass 100%.
- [x] **Hợp nhất các bảng Database bị trùng lặp và rời rạc**:
  - Đã kết nối `CourseProject` với `Group` trong [TeachingController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/TeachingController.cs), thêm khóa ngoại `OralExamId` (`oral_exam_id`) vào `AssessmentSession` ([TeachingConfiguration.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Persistence/Configurations/TeachingConfiguration.cs)).
  - Đã tạo EF Core Migration mới `20261114121657_AddOralExamToAssessmentSessions.cs` và cập nhật `AppDbContextModelSnapshot.cs` đồng bộ đầy đủ cột `oral_exam_id`, index và foreign key constraint.
- [x] **Loại bỏ cơ chế clone nông (`--depth 2`) khi thu thập bằng chứng lịch sử Git**:
  - Đã bỏ `--depth 2` trong [GitCloneService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Infrastructure/Analysis/GitCloneService.cs#L53).
  - Đã sử dụng `git log -n 50 --numstat` trong [EvidenceController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/EvidenceController.cs) để tính toán chính xác `additions`, `deletions`, `changedFilesCount` cho toàn bộ các commit và tổng hợp danh sách `contributions` theo từng tác giả (author name, email, commitCount, additions, deletions, changedFilesCount, commitRatio %, locRatio %).
- [x] **Dọn dẹp logic khảo sát cũ (A/B Testing 4 câu hỏi ngẫu nhiên)** *(Commit `28cfe33`)*:
  - Đã bỏ giới hạn ép trả lời đúng 4 câu và gán cố định `FeedbackFormat.Rubric` trong [StudentSubmissionService.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.Application/Services/Implementations/StudentSubmissionService.cs#L182-L264).
- [x] **Hiện thực hóa hoặc xóa class rỗng `EvaluationLlmService`** *(Commit `28cfe33`)*:
  - Đã xóa `EvaluationLlmService.cs` và `IEvaluationLlmService.cs`.

### 1.2. Frontend (FE) — Đã hoàn thành 100%
- [x] **Bổ sung bảo vệ route Admin bị thiếu trong Middleware** *(Commit `3a4a95e` & `3f9873c`)*:
  - Đã cập nhật `ADMIN_ROUTES` và `STAFF_SHARED_ROUTES` trong [middleware.ts](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/middleware.ts).
- [x] **Thay thế dữ liệu Mock hardcode ở trang Dự án của Sinh viên**:
  - Đã xóa toàn bộ mảng mock hardcode. Tab `deadlines` kết nối dữ liệu thật từ `/api/assessments/sessions` và `/api/classes/{id}`.
  - Tab `tasks` trong [projects/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/projects/page.tsx) kết nối trực tiếp với bằng chứng Git thực tế từ `evidenceService.getEvidence(runId)` để hiển thị các công việc đã commit thật của thành viên, kết hợp với các nhiệm vụ module/API phân tích được mà không sử dụng phép chia dư `%` hay trạng thái giả lập.
- [x] **Kết nối FE với các API BE đã có sẵn nhưng FE chưa gọi**:
  - Đã tạo đầy đủ 6 API client services trong `src/lib/` (`courses.ts`, `assessments.ts`, `evidence.ts`, `reports.ts`, `audit.ts`, `users.ts`).
  - Đã xây dựng giao diện hoàn chỉnh cho trang Quản lý Môn học & Phê duyệt đề tài tại [src/app/(lecturer)/courses/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/courses/page.tsx), kết nối đầy đủ các hàm của `courseService` (`getCourses`, `getCourseById`, `createCourse`, `assignProject`, `approveTopic`).
  - Đã tích hợp `evidenceService` vào [src/app/(student)/projects/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/projects/page.tsx) với tab mới "Bằng chứng Git & AST": hiển thị bảng thống kê đóng góp (`contributions`), danh sách lịch sử commit, modal xem Code Diff chi tiết (`getCommitDiffs`) và công cụ phân tích AST / Control Flow Graph (`getAstCfg`).

---

## PHẦN 2: CHECKLIST CÁC TÍNH NĂNG ĐANG LÀM DỞ / CHỈ CÓ KHUNG (CẦN HOÀN THIỆN) 🚧

### 2.1. Backend (BE) — Mới làm một phần mục 2.1.1
- [ ] **[ĐANG LÀM DỞ] Viết CRUD Services & Controllers cho các bảng đã tạo trong Migration `UpdateDbSchemaTo2026`**:
  - *Đã làm (`289b4c2`)*: Đã thêm [SemestersController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/SemestersController.cs) (`GET`, `POST`, `PUT` học kỳ) và bổ sung `POST /api/classes` (tạo lớp), `POST /api/classes/import` vào [ClassController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/ClassController.cs).
  - *Cần làm nốt*: Bổ sung `PUT`/`DELETE` cho `ClassController`, `DELETE` cho `SemestersController`, và viết CRUD Controllers/Services cho các bảng: `Group`, `GroupMember`, `ClassSchedule`, `ClassSession`, `Attendance`, `Room`, `TimeSlot`, `StudentProfile`, `LecturerProfile`, `OralExam`, `OralQuestion`, `StudentOralSession`, `StudentOralAnswer`.
- [ ] **Hoàn thiện API Chi tiết Khóa học & Cấu hình Rubric Phiên đánh giá**:
  - Thay thế các giá trị đang hardcode rỗng trong [TeachingController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/TeachingController.cs#L131-L235):
    - `milestones = new object[0]` -> trả về danh sách Milestones thực tế.
    - `levels = new object[0]` trong tiêu chí Rubric -> hỗ trợ đầy đủ các mức điểm (Rubric levels/bands) và mô tả (grading guide).
    - Trạng thái kết nối sinh viên (`isAudioActive = false, isVideoActive = false, connectionQuality = "disconnected"`) -> kết nối với trạng thái WebRTC/SignalR thực tế.
- [ ] **Mở rộng Phân tích AST/CFG (`/api/evidence/projects/{projectId}/ast-cfg`)**:
  - Hiện tại [EvidenceController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/EvidenceController.cs#L100-L140) vẫn chỉ hỗ trợ C#, báo lỗi `422` nếu hàm có tham chiếu kiểu dữ liệu ngoài hàm, và để `cyclomaticComplexity = null`. Cần tính toán độ phức tạp Cyclomatic và hỗ trợ thêm Java, JS/TS.
- [ ] **Hoàn thiện Health Check thực tế cho `/health`**:
  - Trong [Program.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Program.cs#L99), `builder.Services.AddHealthChecks()` vẫn trống. Cần đăng ký kiểm tra kết nối PostgreSQL, Upstash Redis, Git CLI, và các AI Providers.

### 2.2. Frontend (FE) — Đã thay thế 5/5 trang `<ComingSoon />`, phát sinh 1 trang `<ComingSoon />` mới
- [x] **Phòng vấn đáp (Sinh viên)** *(Commit `e362112`)*: Đã nối API xem phiên vấn đáp, tiêu chí Rubric và kết quả chấm tại [assessment-room/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(student)/assessment-room/page.tsx).
- [x] **Phiên vấn đáp (Giảng viên)** *(Commit `e362112`)*: Đã nối API tạo phiên, cấu hình Rubric, bắt đầu/kết thúc phiên và chấm điểm tại [assessments/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/assessments/page.tsx).
- [x] **Báo cáo (Giảng viên)** *(Commit `e362112`)*: Đã nối API `/api/reports/{id}` và `/api/reports/{id}/export/pdf` tại [reports/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/reports/page.tsx).
- [x] **Quản lý Người dùng (Admin)** *(Commit `e362112`)*: Đã nối API `/api/users` và `/api/users/import` tại [users/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(admin)/users/page.tsx).
- [x] **Nhật ký Hệ thống (Admin)** *(Commit `e362112`)*: Đã nối API `/api/audit-events` tại [system-audit/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(admin)/system-audit/page.tsx).
- [ ] **[PHÁT SINH MỚI] Trang Môn học của Giảng viên (`/courses`) đang bị chuyển thành `<ComingSoon />`**:
  - Trong commit `3f9873c`, trang [src/app/(lecturer)/courses/page.tsx](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/app/(lecturer)/courses/page.tsx) đã bị đổi thành `<ComingSoon />`. Cần xây dựng giao diện Quản lý Môn học / Đề tài và nối với `courseService` ([src/lib/courses.ts](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/lib/courses.ts)).

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
  - [ ] Giao diện Giảng viên cấu hình Milestones, Deadlines, Rubric, CLO và duyệt/từ chối đăng ký nhóm & đề tài (nối `courseService.approveTopic` vào UI).
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
  - [ ] Dashboard cho Giảng viên theo dõi tiến độ nhóm: kết nối `evidenceService` ([src/lib/evidence.ts](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-FE/src/lib/evidence.ts)) vào UI để hiển thị lịch sử Jira task, hoạt động GitHub, biểu đồ tiến hóa code (Code Evolution), Hồ sơ đóng góp (`Contribution Evidence Profile`) của từng sinh viên, và danh sách cảnh báo lịch sử phát triển bất thường.

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
  - [ ] **Báo cáo Tiến độ Học kỳ (Semester Progress Report)**:
    - Đã có giao diện xem điểm từng phiên (`/assessment-room`, `/reports`); cần bổ sung giao diện tổng hợp Báo cáo Tiến độ xuyên suốt cả học kỳ qua các Milestones cho Sinh viên và Giảng viên.

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
    - Nâng cấp `/assessment-room` để phát luồng màn hình + webcam + mic, xem câu hỏi hiện tại, mở code/tài liệu để trình bày/demo, và bộ lắng nghe sự kiện chuyển tab/mất focus/ngắt chia sẻ màn hình gửi về BE.
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
  - [x] Hoàn thiện trang Quản lý Người dùng & Phân quyền (`/users`) *(Commit `e362112`)*.
  - [ ] Xây dựng trang Cấu hình Hệ thống & Tích hợp (File limits, Retention policy, AI/GitHub/Jira credentials).
  - [ ] Bổ sung phần hiển thị sức khỏe các service (`/health`), trạng thái background jobs, dung lượng lưu trữ và lỗi tích hợp vào trang Giám sát Hệ thống (`/system-audit` hiện đã xong phần tra cứu nhật ký hệ thống).

---

## PHẦN 4: CHECKLIST DỊCH VỤ BÊN THỨ 3, HẠ TẦNG & TÀI LIỆU ĐỒ ÁN 🌐

### 4.1. Đăng ký & Cấu hình Dịch vụ bên thứ 3
- [ ] **Vector Database**: Cài đặt tiện ích mở rộng `pgvector` ngay trên PostgreSQL hiện có (khuyến nghị vì gọn nhẹ, đồng bộ với EF Core) hoặc đăng ký Qdrant Cloud / Pinecone để lưu trữ embeddings cho RAG.
- [ ] **Dịch vụ Speech-to-Text (STT)**: Đăng ký API nhận diện giọng nói hỗ trợ tiếng Việt & timestamps (ví dụ: OpenAI Whisper / Realtime API, Deepgram, hoặc Google Cloud Speech-to-Text).
- [ ] **Dịch vụ WebRTC / Media Server**: Đăng ký LiveKit Cloud / Agora / Daily.co (hoặc cấu hình STUN/TURN server + SignalR) để truyền và ghi lại luồng màn hình, webcam, âm thanh.
- [ ] **Dịch vụ OCR & Audio Analysis**: Tích hợp Tesseract/PaddleOCR (hoặc Google Cloud Vision / `gpt-4o-mini` vision) để quét văn bản bất thường trên màn hình và thư viện VAD/diarization để phát hiện giọng nói thứ hai.
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
