# Phân tích code theo branch

Nhập link gốc để dùng nhánh mặc định, hoặc link branch để đọc code chưa merge:

- `https://github.com/nhathuyck1/Mindy-BE/tree/Feat/FixUnhappyCase`
- `https://github.com/SU-KY-001/su-ky-project/tree/dev`

Chỉ dán URL thực, không dán nguyên cú pháp Markdown `[tên](link)`. Hỗ trợ GitHub `/tree/`, GitLab `/-/tree/`, Bitbucket `/src/` và thư mục con sau tên nhánh.

URL giữ nguyên branch qua các bước tạo nhanh, nhập sheet, lấy template và phân tích. Mỗi branch/path là nguồn riêng để không dùng nhầm template/nghiệp vụ từ nhánh mặc định. Tên branch và thư mục phân biệt hoa/thường. Tên repo hiển thị có phần `[tree/<branch>]` để phân biệt.

Bộ clone đọc danh sách `refs/heads` từ remote, chọn tên branch đầy đủ khớp link (ưu tiên tên khớp dài nhất) rồi clone đúng `--branch`. Không tự chuyển sang nhánh mặc định khi branch sai hoặc clone thất bại. Thư mục con không tồn tại cũng báo lỗi thay vì quét cả repo. Link tag hoặc commit hash chưa được hỗ trợ ở bước chọn branch.

Không cần migration DB. Các record cũ đã lưu URL gốc vẫn dùng nhánh mặc định; không thể tự khôi phục branch từng bị bỏ mất. Nhập lại link branch qua luồng tạo nhanh/phân tích để tạo nguồn đúng. Không tự sửa/xóa câu hỏi hay đáp án của nguồn cũ.
