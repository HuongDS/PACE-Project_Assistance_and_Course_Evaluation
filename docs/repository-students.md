# Gán nhanh sinh viên vào repository

Trong trang admin Repository, nhấn **Thêm sinh viên** ở hàng repo hoặc nhấn số
sinh viên. Hộp thoại có danh sách sinh viên hiện tại và hai cách thêm:

- Dán tối đa 100 MSSV, phân tách bằng dòng mới, khoảng trắng, dấu phẩy hoặc dấu chấm phẩy.
- Tìm sinh viên đã có theo MSSV/email, chọn nhiều kết quả rồi nhấn Thêm sinh viên vào repo.

Mã trùng và chữ thường được chuẩn hóa. Tài khoản mới có email
`mssv@gmail.com`; tài khoản đã có giữ email hiện tại. Hệ thống hiện hỗ trợ mỗi
sinh viên một repo: gán sang repo khác là chuyển liên kết, giữ các bài làm cũ.
Gán lại đúng repo không tạo tài khoản hay phiên làm bài trùng. Thao tác này
không sinh câu hỏi hoặc bài nộp; sinh viên bắt đầu phiên qua luồng đăng nhập hiện có.

**Gỡ khỏi repo** chỉ đặt AnalysisRunId của sinh viên thành null. Giữ tài khoản,
email, bài làm, câu trả lời và điểm khảo sát. API lấy repo của sinh viên đọc DB
thay vì dùng liên kết cũ trong JWT.

Các endpoint đều yêu cầu admin:

- GET `/api/analysis-runs/{id}/students`: danh sách theo repo, có phân trang/tìm kiếm.
- GET `/api/analysis-runs/{id}/students/available`: tìm sinh viên trong DB, trả cả repo đang gán.
- POST `/api/analysis-runs/{id}/students`: body `{"studentCodes":["SE194245","SE194256"]}`.
- DELETE `/api/analysis-runs/{id}/students/{studentId}`: gỡ liên kết.

Thêm hàng loạt chạy trong một transaction. Nếu có mã không hợp lệ, tài khoản
admin/giảng viên, email mặc định bị chiếm hoặc repo không tồn tại, không commit
một phần. Gỡ bằng danh sách cũ không được bỏ liên kết của sinh viên đã chuyển
sang repo khác. Khóa giao dịch dùng cùng namespace MSSV với quick-create.

## Triển khai

Cần chạy migration `20261021010006_AllowStudentsWithoutRepository` trên DB đích
trước khi dùng chức năng gỡ. Migration chỉ bỏ check constraint bắt sinh viên
phải có repo; không xóa hay sửa dòng dữ liệu. Chưa áp dụng migration vào production
trong tác vụ triển khai code này. CI/container hiện không tự chạy migration.

Sau khi cấu hình đúng connection string cho DB đích:

```sh
dotnet ef database update 20261021010006_AllowStudentsWithoutRepository \
  --project PACE.Infrastructure --startup-project PACE.API
```

Sau đó deploy BE và FE. Khi rollback migration, phải gán lại repo cho các sinh
viên đang chưa gán trước khi khôi phục constraint cũ.

## Kiểm tra

```sh
dotnet test PACE.Tests/PACE.Tests.csproj --no-restore -m:1
# Trong thư mục FE:
node --test tests/repository-students.test.cjs
npm run type-check
npm run build
```
