# Export bài đã nộp và đã đánh giá feedback AI

`GET /api/student-submissions/export-excel`

Yêu cầu Bearer token của tài khoản `Admin`. API trả về file `.xlsx`
với Content-Type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

Xuất toàn bộ câu trả lời đã có điểm khảo sát (`RubricScores`) thuộc bài đã nộp
(`SubmittedAt != null`), bao gồm cả tài khoản test, không phân trang.
“Đã đánh giá feedback AI” được xác định bằng việc đã lưu ít nhất một điểm khảo sát,
giống thuộc tính `SurveyCompleted` của bài làm. Câu trả lời chưa có điểm khảo sát
không xuất hiện trong file.

File gồm sheet tổng quan và sheet chi tiết: MSSV, email, repository, nghiệp vụ,
câu hỏi, câu trả lời, feedback AI, phản hồi sinh viên, điểm từng tiêu chí,
điểm trung bình, thời gian nộp UTC và các ID để đối chiếu.
Sheet chi tiết có thêm cột **Nguồn sinh câu hỏi** ở cuối: `Codebase` khi
`GeneratedQuestion.ContextType = Raw`, `Graph` khi `ContextType = Graph`.
Nguồn được lấy từ từng câu hỏi đã lưu, kể cả khi chạy Hybrid (xuất nhánh
Raw/Graph thực tế đã chọn), không suy đoán từ nội dung feedback hoặc repo.
Nếu nguồn không được cung cấp hoặc không được hỗ trợ, ô ghi `Không xác định`.
Để phân tích RQ3, nhóm các dòng theo cột này rồi so sánh điểm tiêu chí
`ACCEPTANCE`; có thể chia tiếp theo `Loại feedback` để đối chiếu từng kiểu.
Nếu không có dữ liệu phù hợp, API vẫn trả file Excel với tiêu đề và thống kê rỗng.

```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$API_BASE_URL/api/student-submissions/export-excel" \
  --output PACE-Reviewed-Submissions.xlsx
```
