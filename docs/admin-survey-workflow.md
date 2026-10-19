# Tạo nhanh và tra cứu khảo sát

## Sinh viên chưa có trong sheet

Admin vào `/admin/submissions` → **Tạo nhanh bài làm**, nhập MSSV, email (nếu biết), chọn repository. Danh sách chỉ có repo đã có câu hỏi; API vẫn kiểm tra lại trước khi ghi. Không tạo repo hay gọi AI khi tạo phiên.

- MSSV được trim và viết hoa, email trim và viết thường.
- Tài khoản chưa có: tạo Student và phiên chưa nộp.
- Tài khoản đã có: giữ nguyên vai trò; chỉ Student mới được gán repo.
- Cùng MSSV/repo: dùng lại phiên đang làm, hoặc trả bài đã nộp để xem lại. Không reset/xóa đáp án hay feedback.
- Nếu để email trống, giữ email cũ; sinh viên mới được gán email theo MSSV và có thể bổ sung email thực lúc đăng nhập nhanh.
- Từ chối email thuộc tài khoản khác và MSSV bị trùng dữ liệu.
- Có transaction, advisory lock theo MSSV và row lock cho tài khoản hiện hữu để tránh tạo trùng khi admin bấm lại.

API (Admin): `GET /api/student-submissions/admin/ready-repositories`, `POST /api/student-submissions/admin/quick-create`.

## Kết quả sau khảo sát

Admin vào `/admin/survey-results`. Chọn bộ lọc rồi **Áp dụng bộ lọc**. Ô tìm kiếm chung tự áp dụng sau 350 ms. Các điều kiện kết hợp bằng AND; nội dung chữ tìm theo chuỗi con, không phân biệt hoa/thường.

Danh sách, số tổng, điểm trung bình, biểu đồ và Excel dùng chung query:

- `analysisRunId`, `businessId`, `questionId`: ID chính xác, không dựa vào tên trùng.
- `studentCode`, `studentEmail`, `repoName`, `businessName`, `questionText`, `answerText`, `aiFeedback`, `studentReply`, `search`.
- `feedbackFormat`: Sandwich, Rubric, Explainable, Researching.
- `criterionId`, `minScore`, `maxScore`: điểm sinh viên đánh giá feedback (1–5). Nếu chọn tiêu chí, chỉ điểm của tiêu chí đó xuất hiện trong bảng/thống kê/Excel. Nếu không chọn, chỉ cần một điểm nằm trọn trong khoảng lọc.
- `minAverageScore`, `maxAverageScore`: trung bình các điểm khảo sát được chọn. Không xem thiếu khảo sát là điểm 0.
- `aiCriterionName`, `minAiScore`, `maxAiScore`: tên và khoảng điểm phải cùng thuộc một tiêu chí AI trong JSON `criteria`; không quy đổi thang điểm. Feedback không có điểm cấu trúc sẽ không khớp bộ lọc này.
- `hasSurvey`, `hasFeedback`: true/false; bỏ trống để xem cả hai.
- `fromDate`, `toDate`: thời gian nộp bài, ISO 8601 có offset; UI đổi giờ địa phương sang UTC. Hai biên được tính bao gồm.
- `excludeTestData`: mặc định true, ẩn MSSV có prefix K6 hoặc email pace-k6-. Không phát hiện được dữ liệu khảo sát mẫu cũ nếu không có marker này.

API (Admin): `GET /api/rubric-criteria/survey-results`, `GET /api/rubric-criteria/survey-results/export-excel`, `GET /api/rubric-criteria/survey-results/options`.

Chỉ lấy câu trả lời thuộc bài đã nộp; không giới hạn ngầm ở 200 bài như trước. Tập đáp án được tải đầy đủ để lọc và tính thống kê, nên với dữ liệu lớn cần chuyển sang tổng hợp/phân trang ở DB. Danh mục dropdown dùng ID từ các bài đã nộp, không thay đổi theo bộ lọc hiện tại. Excel có loại feedback và các ID để truy vết; thời gian Excel là UTC.

Trang kết quả không còn nút tạo dữ liệu mẫu để tránh nhầm với dữ liệu nghiên cứu. Endpoint seed cũ vẫn chỉ dành cho Admin.
