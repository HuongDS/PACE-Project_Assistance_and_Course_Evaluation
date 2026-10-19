# K6: 100 sinh viên, dùng dữ liệu repository có sẵn

Từ thư mục gốc:

```bash
python3 scripts/seed_k6.py --count 100 --batch stress100a
cd scripts/k6
k6 run -e VUS=1 -e DURATION=15s students.js
k6 run -e MODE=llm -e VUS=100 -e DURATION=10m students.js
```

Seed chỉ INSERT `users`, không tạo repo/business/question/method/flow. Đọc DB từ environment `ConnectionStrings__DefaultConnection`, hoặc file local `PACE.API/appsettings.Development.json`. Cần Python, psql và k6. Trỏ đúng DB backend đích.

Mỗi sinh viên được gán ngẫu nhiên một repo có câu hỏi, loại repo fixture cũ `[K6:...]`. Từ repo đó chọn ngẫu nhiên tối đa 4 câu hỏi, giữ đúng businessId/questionId. Đáp án test dùng SuggestedAnswer có sẵn, hoặc câu tổng quát nếu thiếu. Đây là tải vận hành, không phải dữ liệu đánh giá chất lượng AI/NCKH. Không tạo bài nộp hay feedback trước khi chạy k6.

Có thể giới hạn repo bằng `--repo-id UUID` (lặp lại để chọn nhiều repo). `--dry-run` đọc DB và in SQL dự kiến, không ghi. Cùng batch cho kết quả ngẫu nhiên tái lập với cùng pool DB; batch trùng bị từ chối. Mặc định tự sinh batch mới và 100 tài khoản.

Marker: StudentCode `K6<BATCH>X0001`, email `pace-k6-<batch>-0001@example.com`, UUID namespace xác định từ batch/index. File `students.json` cho k6 và manifest `batches/<batch>.json` được Git ignore; manifest lưu userId, repoId, businessId, questionId. Bài nộp/answer/feedback/rubric liên kết tới userId test, không cần thay schema DB. Giữ manifest để truy vết; seed lần sau ghi đè students.json nhưng giữ manifest cũ.

## Chạy

Mặc định MODE=read, VUS=100, DURATION=10m; đọc không gọi chấm AI. MODE=llm mỗi VU nộp một bài duy nhất, tổng tối đa 400 câu được chấm mới với 100 sinh viên (chưa tính retry provider). Có chi phí API AI. Sau khi nộp, VU tiếp tục đọc; không tạo tải chấm lặp vô hạn. Dùng batch mới cho lần chấm tiếp theo.

Script kiểm tra câu được seed vẫn có trong lookup trước khi nộp. Main hiện yêu cầu `min(4, tổng số câu trong repo)`; backend deploy cần cùng contract. Không tự retry submit khi timeout vì bài có thể đang được xử lý. Không dùng tài khoản đã hoàn tất cho lượt test LLM mới.

```bash
# Log từng bước, không in token hay request body
k6 run -e MODE=llm -e VUS=100 -e LIVE_LOG=true -e LOG_VU=1 students.js
```

Có thể đổi API bằng `-e API_BASE_URL=https://host/api`; SITE_URL mặc định https://www.pace1.site và FRONTEND_PATH=/test. HTTP test chỉ tải HTML FE; `frontend.browser.js` dùng Chromium để đo render thật, nên bắt đầu với VUS=1.

`SUBMIT_TIMEOUT=8m`, `GRACEFUL_STOP=10m`, `SUBMIT_P95_MS=180000`, `THINK_BEFORE_SUBMIT=5`. Không có barrier: sinh viên nộp sau login/lookup/join, không phải tất cả cùng một mili giây. Máy phát tải cũng cần đủ CPU/RAM.

## Kết quả

`summary.json` ghi tại thư mục chạy; exit code khác 0 khi threshold fail. `api_success`, `http_req_failed`, `checks` đo lỗi; `api_read_ms` p95 mục tiêu 2s; `submission_ms` đo toàn bộ bài; `ai_completed` kiểm tra feedback thực sự có nội dung và Completed. HTTP 201 chưa chắc AI thành công. Đối chiếu log/CPU/RAM BE, DB và lỗi 429/timeout provider để xác định nghẽn.

## Cleanup theo batch

Từ thư mục gốc, sau khi k6 và xử lý server đã dừng:

```bash
python3 scripts/cleanup_k6.py --batch stress100a
python3 scripts/cleanup_k6.py --batch stress100a --apply
```

Mặc định chỉ kiểm kê. Khi apply, xác minh đồng thời marker, UUID, email, role và quyền sở hữu bài làm dưới transaction/lock; xóa rubric scores, answers (kèm feedback), submissions, rồi users test. **Không DELETE repo, business, question hoặc dữ liệu người dùng khác.** Giữ manifest và báo cáo để truy vết. Batch legacy từ script cũ không thuộc marker mới và không bị lệnh này xóa.
