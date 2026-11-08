# Hướng dẫn Kiểm thử API Sinh câu hỏi với Điểm neo tri thức Draw.io

Thư mục này chứa dữ liệu mẫu và kịch bản để bạn kiểm thử luồng sinh câu hỏi sau khi đã tích hợp sơ đồ quy trình chuẩn từ Draw.io.

---

## 1. Dữ liệu mẫu có sẵn trong thư mục này

* **`restaurant-order-flow.xml`**: Sơ đồ Draw.io chuẩn thể hiện quy trình:
  `Bắt đầu -> Khách chọn món -> Kiểm tra bếp -> Bếp xác nhận -> Khách thanh toán -> Hoàn tất`.
* **`auction-bidding-flow.xml`**: Sơ đồ Draw.io quy trình đấu giá:
  `Tạo phiên -> Kiểm tra số dư ký quỹ -> Kích hoạt phiên / Báo lỗi`.
* **`upload-spec-request.json`**: Payload mẫu để upload XML qua chuỗi JSON.
* **`generate-questions-request.json`**: Payload mẫu để gọi API sinh câu hỏi.

---

## 2. Các bước kiểm thử từng bước

### Bước 1: Khởi động API
Mở terminal tại thư mục gốc của dự án:
```bash
dotnet run --project PACE.API
```
Truy cập Swagger UI tại: `https://localhost:7084/swagger` (hoặc cổng HTTP hiển thị trên console).

---

### Bước 2: Upload Sơ đồ quy trình chuẩn (Draw.io XML)

Bạn có thể upload bằng **1 trong 2 cách**:

#### Cách A: Upload trực tiếp file `.xml` (Khuyên dùng)
Dùng cURL hoặc tính năng upload file trên Swagger:
```bash
# Thay thế {businessId} bằng ID của Business thực tế trong database của bạn
curl -X POST "https://localhost:7084/api/businesses/{businessId}/specification/drawio/file" \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@mock-data-test/restaurant-order-flow.xml"
```

Hoặc dùng PowerShell:
```powershell
$businessId = "YOUR-BUSINESS-ID-HERE"
$token = "YOUR-ADMIN-TOKEN-HERE"
$filePath = "mock-data-test\restaurant-order-flow.xml"

$form = @{
    file = Get-Item -Path $filePath
}
Invoke-RestMethod -Uri "https://localhost:7084/api/businesses/$businessId/specification/drawio/file" `
    -Method Post `
    -Headers @{ Authorization = "Bearer $token" } `
    -Form $form
```

#### Cách B: Gửi nội dung XML qua JSON Body
```bash
curl -X POST "https://localhost:7084/api/businesses/{businessId}/specification/drawio" \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d @mock-data-test/upload-spec-request.json
```

**Kết quả trả về mẫu:**
```json
{
  "businessId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "diagramName": "Quy Trinh Goi Mon Va Thanh Toan",
  "nodeCount": 6,
  "edgeCount": 5,
  "pathCount": 2,
  "normalizedFlowText": "### QUY TRÌNH NGHIỆP VỤ CHUẨN: Quy Trinh Goi Mon Va Thanh Toan\n...",
  "processedAt": "2026-11-08T01:30:00Z"
}
```

> File đã xử lý sẽ được lưu tạm tại: `PACE.API/bin/Debug/net9.0/App_Data/specifications/{businessId}.json` (không ảnh hưởng tới cấu trúc DB hiện tại).

---

### Bước 3: Xem lại sơ đồ đã lưu của Business
```bash
curl -X GET "https://localhost:7084/api/businesses/{businessId}/specification/drawio" \
  -H "Authorization: Bearer <ADMIN_TOKEN>"
```

---

### Bước 4: Gọi API Sinh câu hỏi vấn đáp (Generate Questions)

Sau khi sơ đồ chuẩn đã được nạp, bạn gọi API tạo câu hỏi:
```bash
curl -X POST "https://localhost:7084/api/businesses/{businessId}/questions/generate" \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d @mock-data-test/generate-questions-request.json
```

#### Dấu hiệu nhận biết cải tiến thành công:
1. **Trong JSON Response:** Trường `"hasGroundTruth": true` sẽ được bật lên.
2. **Trong nội dung các câu hỏi (`questions`):**
   * Nếu mã nguồn của sinh viên triển khai **ngược luồng** (ví dụ: Thanh toán trước khi Gọi món): LLM sẽ đặt các câu hỏi xoáy thẳng vào lỗi nghiệp vụ đảo lộn này (ví dụ: *"Tại sao hệ thống lại tiến hành thanh toán tiền trước khi danh sách món ăn được nhà bếp xác nhận?"*).
   * Mảng `targetedEntryPoints` sẽ trỏ trực tiếp vào các hàm đang thực hiện sai thứ tự trong code.

---

### Bước 5: Xóa sơ đồ chuẩn (khi muốn test lại luồng không có Ground Truth)
```bash
curl -X DELETE "https://localhost:7084/api/businesses/{businessId}/specification/drawio" \
  -H "Authorization: Bearer <ADMIN_TOKEN>"
```
Khi gọi lại `/generate`, `"hasGroundTruth"` sẽ trả về `false` và hệ thống hoạt động như phiên bản cũ.

