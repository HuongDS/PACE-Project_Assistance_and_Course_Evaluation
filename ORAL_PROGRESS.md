# Tiến độ và review luồng vấn đáp

## Luồng hiện tại

- Giảng viên tạo hoặc chọn đề theo lớp. Backend hỗ trợ câu thủ công và câu đã duyệt từ ngân hàng AI. Giao diện tạo đề hiện hỗ trợ nhập câu thủ công.
- Khi bắt đầu buổi, hệ thống lưu bản sao câu hỏi. Giảng viên ghi câu đã hỏi, câu hỏi tiếp và tóm tắt câu trả lời theo từng sinh viên. Sửa ghi chú sau khi chấm sẽ yêu cầu chấm lại sinh viên đó.
- Giảng viên chấm theo rubric và kết thúc buổi để công bố điểm. Sinh viên thấy điểm vấn đáp của mình sau khi buổi kết thúc.

## Review BE-3.1.1

- Milestone, deadline và thông báo được quản lý theo lớp. Giảng viên phụ trách hoặc admin chỉnh sửa. Sinh viên đang ghi danh xem được lịch và thông báo.
- Import Excel hoặc CSV qua `POST /api/classes/{classId}/milestones/import`. Header file là `kind,title,dueAt,description`. Giá trị `kind` là `Milestone` hoặc `Deadline`. Cột thời gian nhận timestamp ISO 8601 có múi giờ. Ô ngày giờ kiểu Excel được hiểu là UTC.
- Import được lưu cùng kết quả trong một transaction. Retry cùng `Idempotency-Key` và cùng file trả lại kết quả ban đầu. Dùng lại khóa với file khác trả `409`. Khi bỏ header, backend dùng fingerprint của file. Muốn sửa mốc đã tạo thì gọi API cập nhật theo ID.
- Đợt đánh giá có cấu hình nháp chỉnh sửa được. Khi công bố, backend lưu một phiên bản bất biến gồm rubric, trọng số, deadline, khung mở/đóng, sản phẩm yêu cầu, CLO, chủ đề, hướng dẫn chấm và hướng dẫn hỏi đáp AI. Lịch sử lưu người và thời điểm công bố.
- API công bố là `POST /api/classes/{classId}/milestones/{id}/assessment-rounds/{roundId}/publish` với `previousVersion` bằng phiên bản đang công bố hoặc `0` khi công bố lần đầu. API `GET .../{roundId}/revisions` trả lịch sử cho giảng viên phụ trách và admin.
- Khi tạo buổi oral, giảng viên có thể chọn phiên bản đã công bố của lớp dự án. Backend kiểm tra khung giờ, lưu ID phiên bản và sao rubric vào buổi ngay lúc tạo. Nếu sau đó công bố rubric mới, buổi đã tạo vẫn dùng rubric cũ. Buổi dùng rubric riêng vẫn được hỗ trợ.

## Phần tiếp theo

- BE-3.1.3 sẽ quản lý bản nộp theo đợt. Nhóm được nộp lại đúng hạn. Mỗi lần nộp hợp lệ lưu người nộp, giờ server nhận, commit SHA, danh sách file và checksum. Giảng viên cần truy vấn và tải được các bản cũ. Khi chốt để chấm, buổi oral sẽ giữ tham chiếu tới bản hợp lệ mới nhất nhận đúng hạn.
- Hướng dẫn hỏi đáp AI đã được giữ trong phiên bản công bố. Pipeline sinh câu hỏi chưa đọc cấu hình đó. Giao diện hiện có ô chọn đợt khi tạo buổi. Việc tạo, sửa và công bố đợt thực hiện qua API.
- Cần áp dụng các migration `AddClassPlanning`, `AddAssessmentRoundRevisions`, `AddClassScheduleImportHistory` và `LinkSessionsToAssessmentRoundRevisions` trên database đích trước khi chạy bản API này. Đã áp dụng trên PostgreSQL 18 tạm.

## Kiểm tra

- 32 test hồi quy Teaching và luồng giảng viên tạo lớp đã qua.

- Kiểm thử HTTP trên PostgreSQL 18 mới tạo đã qua cho CRUD, import Excel/CSV, dữ liệu lỗi không lưu một phần, retry đồng thời, khóa trùng với nội dung khác và phân quyền chéo lớp.
- Kiểm thử HTTP xác nhận buổi tiếp tục dùng rubric phiên bản 1 sau khi phiên bản 2 được công bố, sinh viên thấy đúng rubric, và luồng tạo buổi có rubric riêng vẫn hoạt động.
- Frontend đã qua kiểm tra TypeScript. Chưa kiểm thử trên trình duyệt.
