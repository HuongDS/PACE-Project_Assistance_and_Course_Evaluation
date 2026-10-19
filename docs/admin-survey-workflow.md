# Tạo nhanh và tra cứu khảo sát

## Sinh viên chưa có trong sheet

Admin vào `/admin/submissions` → **Tạo nhanh bài làm**, nhập đủ **MSSV, Gmail/email và link repository** rồi bấm **Lưu & tiếp tục phân tích repo**. Không cần nhập sheet và không yêu cầu repo đã có câu hỏi.

1. BE chuẩn hóa link repo, dùng lại repository có cùng URL hoặc tạo repository chưa phân tích; tạo/dùng lại Student và phiên bài làm.
2. FE chuyển ngay sang wizard phân tích dùng chung với trang Repository: tải/đề xuất template nghiệp vụ, admin xem lại rồi **Chốt & phân tích**.
3. Phân tích xong, bấm **Chọn nghiệp vụ & sinh câu hỏi** để mở `/admin/questions?run=<id>` đúng repository. Admin tick nghiệp vụ, chọn số câu/độ khó và sinh câu hỏi như luồng bình thường.
4. Nếu repo đã có nghiệp vụ, có thể chọn **Chọn nghiệp vụ hiện có & sinh câu hỏi** để dùng lại; không bắt phân tích lại repo đã có câu hỏi/đáp án.

- MSSV trim/viết hoa, email trim/viết thường; email là bắt buộc.
- Từ chối link thiếu tên repo, email thuộc tài khoản khác, MSSV bị trùng hoặc tài khoản có vai trò Admin/Lecturer.
- Cùng MSSV/repo: dùng lại phiên đang làm hoặc bài đã nộp. Không reset/xóa đáp án hay feedback.
- Có transaction, advisory lock theo URL repo và MSSV, row lock cho tài khoản hiện hữu. Clone/AI chạy sau transaction đăng ký qua luồng phân tích chuẩn.
- Nếu phân tích hoặc sinh câu hỏi lỗi, tài khoản/repo/phiên vừa đăng ký vẫn còn để thử lại; không báo sinh viên đã sẵn sàng khi chưa có câu hỏi.

API (Admin): `POST /api/student-submissions/admin/quick-create` nhận `{ studentCode, email, repoUrl }`. Không còn yêu cầu `analysisRunId` hay danh mục repo có câu hỏi.

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
