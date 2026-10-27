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
Nếu không có dữ liệu phù hợp, API vẫn trả file Excel với tiêu đề và thống kê rỗng.

```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$API_BASE_URL/api/student-submissions/export-excel" \
  --output PACE-Reviewed-Submissions.xlsx
```
