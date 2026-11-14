# Checklist Tiến Độ & Danh Sách Task Còn Lại — Hệ Thống AIPOA (PACE)

*Cập nhật lần cuối: 14/11/2026 — Nhánh `feature/part1-bugfixes-hardening`*

> [!TIP]
> **TỔNG KẾT TIẾN ĐỘ HIỆN TẠI:**
> - ✅ **PHẦN 1 (1.1 Backend & 1.2 Frontend — Sửa lỗi, bảo mật, chuẩn hóa DB, PDF Tiếng Việt, Git Contributions & Kết nối API):** **ĐÃ HOÀN THÀNH 100% (12/12 mục)**.
> - ✅ **PHẦN 2.2 (Frontend — Thay thế toàn bộ các trang `<ComingSoon />` bằng UI thực tế):** **ĐÃ HOÀN THÀNH 100% (6/6 trang: `/assessment-room`, `/assessments`, `/reports`, `/users`, `/system-audit`, `/courses`)**.
> - 🚧 **PHẦN 2.1 (Backend — Hoàn thiện các API/Cấu hình đang làm dở):** **Còn 4 mục cần hoàn thiện**.
> - 🚀 **PHẦN 3 (3.1 -> 3.7 — Các tính năng cốt lõi xây dựng mới của đồ án AIPOA):** **Còn 7 nhóm module lớn cần triển khai**.
> - 🌐 **PHẦN 4 (4.1 -> 4.2 — Dịch vụ bên thứ 3, Hạ tầng & Tài liệu đồ án):** **Cần đăng ký dịch vụ và viết tài liệu/test cases**.

---

## I. DANH SÁCH CÁC TASK CÒN LẠI CẦN LÀM (REMAINING TASKS) 🎯

### PHẦN 2.1: BACKEND (BE) — HOÀN THIỆN CÁC TÍNH NĂNG KHUNG ĐANG LÀM DỞ 🚧

- [ ] **1. Hoàn thiện CRUD Services & Controllers cho các bảng Học vụ trong Migration `UpdateDbSchemaTo2026`**:
  - *Đã có*: `SemestersController` (`GET`, `POST`, `PUT`) và `ClassController` (`GET`, `POST`, `Import/Export`).
  - *Cần làm nốt*:
    - Bổ sung `PUT /api/classes/{id}`, `DELETE /api/classes/{id}` trong [ClassController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/ClassController.cs) và `DELETE /api/semesters/{id}` trong [SemestersController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/SemestersController.cs).
    - Viết CRUD Controllers/Services cho các bảng đã có trong DB: `Group`, `GroupMember`, `ClassSchedule`, `ClassSession`, `Attendance`, `Room`, `TimeSlot`, `StudentProfile`, `LecturerProfile`, `OralExam`, `OralQuestion`, `StudentOralSession`, `StudentOralAnswer`.
- [ ] **2. Hoàn thiện API Chi tiết Khóa học & Cấu hình Rubric Phiên đánh giá (`TeachingController.cs`)**:
  - Thay thế các giá trị đang hardcode rỗng trong [TeachingController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/TeachingController.cs#L131-L235):
    - `milestones = new object[0]` -> trả về danh sách Milestones thực tế của khóa học.
    - `levels = new object[0]` trong tiêu chí Rubric -> hỗ trợ đầy đủ các mức điểm (Rubric levels/bands) và mô tả hướng dẫn chấm (grading guide).
    - Trạng thái kết nối sinh viên (`isAudioActive = false, isVideoActive = false, connectionQuality = "disconnected"`) -> kết nối với trạng thái phiên trực tuyến thực tế.
- [ ] **3. Mở rộng Phân tích AST/CFG (`/api/evidence/projects/{projectId}/ast-cfg`)**:
  - Trong [EvidenceController.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Controllers/EvidenceController.cs):
    - Tính toán và trả về độ phức tạp `cyclomaticComplexity` thực tế (hiện đang để `null`).
    - Xử lý trường hợp hàm C# có tham chiếu đến kiểu dữ liệu ngoài hàm (tránh trả lỗi `422`) và mở rộng hỗ trợ AST/CFG cho Java, JS/TS (bằng Tree-sitter đã có sẵn trong project).
- [ ] **4. Hoàn thiện Health Check thực tế cho `/health` (`Program.cs`)**:
  - Trong [Program.cs](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/PACE.API/Program.cs#L99), đăng ký kiểm tra kết nối thực tế cho `builder.Services.AddHealthChecks()`: PostgreSQL (`AppDbContext`), Upstash Redis, Git CLI, và trạng thái cấu hình các AI Providers.

---

### PHẦN 3: CÁC TÍNH NĂNG CỐT LÕI CHƯA LÀM (CẦN XÂY DỰNG MỚI) 🚀

#### 3.1. Quản lý Học vụ, Nhóm, Đề tài, Milestone & Đóng băng bài nộp (Frozen Snapshot)
- **Backend (BE):**
  - [ ] Thiết kế bảng và API quản lý **Milestones & Deadlines** cho từng khóa học/lớp học (trọng số, hạn nộp, danh sách sản phẩm yêu cầu, độ phủ CLO/Topic, hướng dẫn chấm và chỉ dẫn hỏi đáp riêng cho AI).
  - [ ] Xây dựng luồng **Sinh viên tự tạo nhóm & đăng ký đề tài**: mời/chọn thành viên, bầu nhóm trưởng, chọn đề tài có sẵn hoặc đề xuất đề tài mới, gửi yêu cầu phê duyệt và yêu cầu đổi thành viên/đề tài.
  - [ ] Xây dựng cơ chế **Nộp bài theo Milestone & Đóng băng bằng chứng (Frozen Evidence Snapshot)**:
    - Cho phép upload gói tài liệu dự án (SRS, thiết kế, DB schema/migrations, API docs) kèm mã `commit SHA`.
    - Khóa (freeze) trạng thái mã nguồn, tài liệu và trạng thái Jira/GitHub tại thời điểm trước buổi đánh giá để thay đổi sau đó không làm sai lệch căn cứ chấm.
- **Frontend (FE):**
  - [ ] Giao diện Sinh viên xem danh sách đề tài, tự tạo nhóm, mời thành viên, đăng ký/đề xuất đề tài và gửi yêu cầu thay đổi.
  - [ ] Giao diện Giảng viên cấu hình Milestones, Deadlines, CLO/Topic coverage và hướng dẫn hỏi đáp tùy chỉnh cho AI.
  - [ ] Giao diện Sinh viên nộp gói sản phẩm theo từng Milestone (chọn commit SHA, upload tài liệu dự án) và xem lịch sử các bản nộp đã đóng băng (Frozen snapshots).

#### 3.2. Tích hợp GitHub/Jira & Phân tích Đóng góp / Tiến hóa Code (Contribution Intelligence)
- **Backend (BE):**
  - [ ] **Tích hợp Jira API (Read-only)**: Lấy danh sách issues, trạng thái, lịch sử chuyển trạng thái, người được giao (assignee) và sprint.
  - [ ] **Ánh xạ định danh (Identity Mapping)**: Liên kết tài khoản Sinh viên trong hệ thống với username/email GitHub và tài khoản Jira.
  - [ ] **Phân tích Tiến hóa Code (Code Evolution) ở mức File/Class/Method**:
    - Theo dõi lịch sử thay đổi ở mức module/file/class/method theo thời gian (ai tạo mới, ai sửa đổi, ai viết lại phần lớn code).
    - Phân biệt rõ **đóng góp lịch sử** (người viết code ban đầu) và **hiện trạng mã nguồn** (người sửa đổi/viết lại sau đó), kèm cơ chế chụp snapshot định kỳ.
  - [ ] **Đối chiếu đa nguồn & Phát hiện bất thường (Anomaly Detection)**:
    - Liên kết ngữ nghĩa giữa Jira task <-> Pull Request <-> Commit <-> Code Diff.
    - Phát hiện và gắn cờ các trường hợp bất thường: đẩy lượng lớn code vào phút chót (large late code introduction), lịch sử bị xóa/squash/rebase/force-push, dùng chung tài khoản commit, hoặc code không có task Jira tương ứng.
  - [ ] **Tổng hợp Hồ sơ Bằng chứng Đóng góp (`Contribution Evidence Profile`)** chi tiết cho từng sinh viên.
- **Frontend (FE):**
  - [ ] Giao diện Sinh viên kết nối/ánh xạ tài khoản GitHub và Jira cá nhân.
  - [ ] Dashboard cho Giảng viên theo dõi tiến độ nhóm: lịch sử Jira task, biểu đồ tiến hóa code (Code Evolution), Hồ sơ đóng góp (`Contribution Evidence Profile`) của từng sinh viên, và danh sách cảnh báo lịch sử phát triển bất thường.

#### 3.3. Xử lý Bằng chứng, Cơ sở Tri thức Dự án & Hệ thống RAG
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
    - Xây dựng bộ truy xuất RAG lấy đúng ngữ cảnh liên quan đến sinh viên, tiêu chí Rubric, lịch sử câu hỏi và chỉ dẫn của giảng viên (thay vì nhồi toàn bộ code vào prompt).

#### 3.4. Hệ thống AI Hỗ trợ Vấn đáp (AI-Assisted Oral Assessment)
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
    - Nâng cấp trang `/assessments` với khung gợi ý câu hỏi từ AI (câu hỏi chung & câu hỏi cá nhân, lý do, bằng chứng); các nút chọn hỏi, sửa câu hỏi, bỏ qua, yêu cầu sinh thêm câu hỏi/câu hỏi phụ (follow-up), hoặc nhập câu hỏi thủ công.
    - Khung hiển thị bản gỡ băng thời gian thực (Real-time STT Transcript) có chỉnh sửa người nói.
    - Khung hiển thị phân tích câu trả lời tức thì của AI, thẻ gợi ý hành động tiếp theo (`CLARIFY`, `PROBE`...) và thanh theo dõi độ phủ Rubric/sinh viên.
  - [ ] **Giao diện Duyệt kết quả, Đối chiếu Bằng chứng & Chốt điểm**:
    - Cho phép Giảng viên bấm vào từng nhận định chấm điểm của AI để xem đối chiếu song song đoạn transcript/video trả lời của sinh viên và đoạn code/tài liệu/commit diff được trích dẫn.
    - Cho phép chỉnh sửa/ghi đè (override) điểm của AI, thêm ghi chú, chốt điểm và công bố cho sinh viên.
  - [ ] **Báo cáo Tiến độ Học kỳ (Semester Progress Report)**:
    - Bổ sung giao diện và API tổng hợp Báo cáo Tiến độ xuyên suốt cả học kỳ qua các Milestones cho Sinh viên và Giảng viên (bên cạnh báo cáo từng phiên đã có ở `/reports`).

#### 3.5. Phòng Vấn đáp Trực tuyến, Truyền luồng WebRTC & Giám sát Môi trường
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

#### 3.6. Mô-đun Trợ giảng AI (AI Teaching Assistant)
- **Backend (BE):**
  - [ ] Xây dựng Background Scheduler / Cronjob chạy định kỳ trước mỗi ca học **15 phút** (dựa trên lịch `ClassSchedule` / `TimeSlot`):
    - Tổng hợp danh sách các task trễ hạn hoặc chưa hoàn thành của từng nhóm.
    - Đánh giá giai đoạn hiện tại và mức độ tiến độ chung của dự án.
    - Phát hiện và cảnh báo các nhóm không hoạt động trên hệ thống (System inactivity alerts).
    - Gửi thông báo/bản tin (Briefing) cho giảng viên qua Web UI và Email.
- **Frontend (FE):**
  - [ ] Giao diện hiển thị Bản tin Trợ giảng AI (Pre-class AI Briefing) trên Dashboard của Giảng viên trước giờ lên lớp.

#### 3.7. Phân hệ Quản trị Hệ thống (System Administration)
- **Backend (BE):**
  - [ ] Xây dựng bảng cấu hình và API cho phép Admin thiết lập động: định dạng file cho phép, giới hạn dung lượng upload, thời gian lưu trữ dữ liệu (retention policy), cấu hình AI/LLM/STT providers và chính sách giám sát.
  - [ ] Xây dựng hệ thống quản lý Background Jobs (theo dõi tiến trình clone repo, phân tích AST, tạo embeddings) và API thống kê dung lượng lưu trữ (Storage usage), lỗi tích hợp (Integration failures).
- **Frontend (FE):**
  - [ ] Xây dựng trang Cấu hình Hệ thống & Tích hợp (File limits, Retention policy, AI/GitHub/Jira credentials).
  - [ ] Bổ sung phần hiển thị sức khỏe các service (`/health`), trạng thái background jobs, dung lượng lưu trữ và lỗi tích hợp vào trang Giám sát Hệ thống (`/system-audit`).

---

### PHẦN 4: DỊCH VỤ BÊN THỨ 3, HẠ TẦNG & TÀI LIỆU ĐỒ ÁN 🌐

#### 4.1. Đăng ký & Cấu hình Dịch vụ bên thứ 3
- [ ] **Vector Database**: Cài đặt tiện ích mở rộng `pgvector` ngay trên PostgreSQL hiện có (khuyến nghị vì gọn nhẹ, đồng bộ với EF Core) hoặc đăng ký Qdrant Cloud / Pinecone để lưu trữ embeddings cho RAG.
- [ ] **Dịch vụ Speech-to-Text (STT)**: Đăng ký API nhận diện giọng nói hỗ trợ tiếng Việt & timestamps (ví dụ: OpenAI Whisper / Realtime API, Deepgram, hoặc Google Cloud Speech-to-Text).
- [ ] **Dịch vụ WebRTC / Media Server**: Đăng ký LiveKit Cloud / Agora / Daily.co (hoặc cấu hình STUN/TURN server + SignalR) để truyền và ghi lại luồng màn hình, webcam, âm thanh.
- [ ] **Dịch vụ OCR & Audio Analysis**: Tích hợp Tesseract/PaddleOCR (hoặc Google Cloud Vision / `gpt-4o-mini` vision) để quét văn bản bất thường trên màn hình và thư viện VAD/diarization để phát hiện giọng nói thứ hai.
- [ ] **Jira Atlassian Developer App & GitHub OAuth/App**: Đăng ký ứng dụng trên Atlassian Developer Console và GitHub Developer Settings để lấy thông tin kết nối Read-only cho các nhóm sinh viên.
- [ ] **Object Storage (Lưu trữ Video/File nộp)**: Cấu hình Cloudflare R2, AWS S3, hoặc MinIO để lưu trữ các bản ghi màn hình/audio và gói tài liệu nộp theo Milestone.

#### 4.2. Bộ Tài liệu Đồ án & Kiểm thử (Deliverables theo mục 3.2.c & 3.2.e)
- [ ] **Tài liệu Đặc tả & Thiết kế**:
  - [ ] User Requirements & Software Requirements Specification (URS & SRS).
  - [ ] Use Case Model & Detailed Workflows.
  - [ ] Domain/Data Model & Relational Database Design.
  - [ ] System Architecture Design & Detailed Design.
  - [ ] Project Evidence & Contribution Intelligence Design.
  - [ ] AI System Design (RAG, Question Planning, Adaptive Follow-up, Rubric Assessment, Citations & Guardrails).
  - [ ] Web-Based Assessment Monitoring Design.
  - [ ] API / Integration Documentation.
- [ ] **Kiểm thử & Đánh giá (QA & Pilot Evaluation)**:
  - [ ] Xây dựng bộ Test Cases kiểm thử chất lượng AI (độ liên quan của câu hỏi, độ bám sát bằng chứng, độ chính xác của trích dẫn, tỷ lệ ảo giác và mức độ đồng thuận chấm điểm với giảng viên).
  - [ ] Kiểm thử hiệu năng (độ trễ sinh câu hỏi và phân tích câu trả lời trực tiếp), bảo mật (RBAC, che giấu secret) và khả năng khôi phục phiên khi mất kết nối.
  - [ ] Hoàn thiện Test Plan, Test Report, Deployment Guide và User Manual.

---

## II. LỊCH SỬ CÁC HẠNG MỤC ĐÃ HOÀN THÀNH (COMPLETED) ✅

### 1. Phần 1.1 — Backend (BE): Sửa lỗi, Bảo mật & Chuẩn hóa (9/9 mục)
- [x] Xóa tài khoản Backdoor hardcode trong `AuthService.cs`.
- [x] Gỡ bỏ thông tin nhạy cảm (Upstash Redis secret) khỏi `appsettings.json` & `UpstashRedisOtpCacheService.cs`.
- [x] Hiện thực `GeminiQuestionLlmClient.cs`, resolve DI động Gemini/OpenAI và cập nhật model `gpt-4o-mini`.
- [x] Bật `[Authorize(Roles = Roles.Staff)]` cho `QuestionsController.cs`.
- [x] Thay thế xuất PDF thô bằng `PdfSharpCore` nhúng font Unicode tiếng Việt (Arial), tính `gradeLetter` và tổng hợp `aiConclusions` trong `ReportsController.cs`.
- [x] Hợp nhất liên kết `CourseProject` <-> `Group`, `AssessmentSession` <-> `OralExam` và tạo EF Core Migration `20261114121657_AddOralExamToAssessmentSessions.cs`.
- [x] Bỏ `--depth 2` trong `GitCloneService.cs` và dùng `git log -n 50 --numstat` trong `EvidenceController.cs` để tính đầy đủ `additions`, `deletions`, `changedFilesCount` và mảng `contributions` theo từng tác giả.
- [x] Dọn dẹp logic khảo sát cũ 4 câu ngẫu nhiên trong `StudentSubmissionService.cs`, chuyển sang cố định `FeedbackFormat.Rubric`.
- [x] Xóa class rỗng `EvaluationLlmService.cs` và `IEvaluationLlmService.cs`.

### 2. Phần 1.2 & Phần 2.2 — Frontend (FE): Middleware, API Services & Giao diện (9/9 mục)
- [x] Bổ sung bảo vệ route Admin (`/classes`, `/repositories`, `/users`, `/system-audit`) và quyền dùng chung `/classes` cho Lecturer/Admin trong `src/middleware.ts`.
- [x] Thay thế toàn bộ mảng mock `sampleTasks` & `sampleDeadlines` trong `src/app/(student)/projects/page.tsx` bằng dữ liệu thật từ `/api/assessments/sessions`, `/api/classes/{id}` và bằng chứng commit từ `evidenceService.getEvidence(runId)`, kèm tab mới "Bằng chứng Git & AST".
- [x] Tạo đầy đủ 6 API client services trong `src/lib/` (`courses.ts`, `assessments.ts`, `evidence.ts`, `reports.ts`, `audit.ts`, `users.ts`) và gắn toàn bộ vào giao diện.
- [x] Thay thế toàn bộ 6 trang `<ComingSoon />` bằng giao diện thực tế kết nối API:
  - [x] Phòng vấn đáp Sinh viên (`/assessment-room`)
  - [x] Phiên vấn đáp Giảng viên (`/assessments`)
  - [x] Báo cáo & Xuất PDF Giảng viên (`/reports`)
  - [x] Quản lý Môn học & Duyệt đề tài Giảng viên (`/courses`)
  - [x] Quản lý Người dùng & Import Excel Admin (`/users`)
  - [x] Nhật ký Hệ thống Admin (`/system-audit`)
