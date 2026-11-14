# Sinh câu hỏi vấn đáp (Raw / Graph / Hybrid)

## API (role Admin)

| Method | Route | Mô tả |
|---|---|---|
| POST | `/api/businesses/{businessId}/questions/generate` | Sinh câu hỏi bằng GPT-6 Luna và lưu vào `generated_questions` |
| GET | `/api/businesses/{businessId}/questions` | Danh sách câu hỏi đã lưu của nghiệp vụ |
| DELETE | `/api/questions/{id}` | Xoá câu hỏi (chặn nếu đã có sinh viên trả lời) |
| POST | `/api/businesses/{businessId}/specification/drawio` | Upload XML Draw.io dạng text JSON để làm điểm neo tri thức |
| POST | `/api/businesses/{businessId}/specification/drawio/file` | Upload file `.xml` hoặc `.drawio` quy trình chuẩn |
| GET | `/api/businesses/{businessId}/specification/drawio` | Xem thông tin sơ đồ quy trình chuẩn đã lưu của nghiệp vụ |
| DELETE | `/api/businesses/{businessId}/specification/drawio` | Xoá sơ đồ quy trình chuẩn tạm của nghiệp vụ |

Body của `generate`:

```json
{
  "mode": "Hybrid",
  "numberOfQuestions": 5,
  "difficulty": "Trung bình",
  "description": null,
  "replaceExisting": false
}
```

- `mode`: `Raw` | `Graph` | `Hybrid` (mặc định `Hybrid`).
- `difficulty`: `Dễ` | `Trung bình` | `Khó` — khớp với hướng dẫn độ khó trong prompt.
- `replaceExisting = true`: xoá các câu hỏi cũ **chưa có câu trả lời** của nghiệp vụ trước khi lưu câu mới.

Phản hồi trả về mode đã chọn thực tế (`appliedContext`), `nodeCount` (N_w), lý do định tuyến, token, latency và
danh sách câu hỏi kèm `targetedEntryPoints` (chỉ trả về, không lưu DB).

## Ba mode

| Mode | Khe `SOURCE CODE` | Khe `DATA FLOW GRAPH` | Tương ứng bài báo 1 |
|---|---|---|---|
| `Raw` | mã nguồn thô các hàm của nghiệp vụ | ẩn (chuỗi "Chế độ Truyền thống…") | Traditional |
| `Graph` | ẩn (chuỗi "Chế độ Graph…") | Mermaid của từng luồng | Pure CFG |
| `Hybrid` | N_w > `GraphNodeThreshold` → như `Graph`, ngược lại → như `Raw` | | router theo kích thước luồng |

N_w = số hàm phân biệt trong các luồng của nghiệp vụ (entry + hai đầu mỗi cạnh), tính trước khi gọi LLM.
Ngưỡng mặc định 10 lấy từ kết quả phân tầng của bài báo 1: dưới 10 node, nén CFG làm mất 0.224 accuracy;
trên ngưỡng, mức mất giảm một bậc độ lớn. `ContextType` lưu trong DB là biểu diễn đã dùng thực tế (`Raw`/`Graph`).

## Điểm neo tri thức từ Sơ đồ chuẩn Draw.io (Ground Truth Specification)

Khi giảng viên upload sơ đồ quy trình chuẩn (`.xml` hoặc `.drawio`), module Drawio:
1. Parse XML (hỗ trợ cả uncompressed lẫn compressed Deflate Base64 của Draw.io).
2. Lọc sạch thẻ HTML rác, trích xuất danh sách thực thể (Actors, Tasks), các cổng rẽ nhánh (Gateways) và các đường đi thực thi hoàn chỉnh (Execution Paths).
3. Lưu tạm dạng JSON vào thư mục cấu hình (`App_Data/specifications/{businessId}.json`).
4. Khi gọi `/generate`: Hệ thống tự động nạp chuỗi quy trình chuẩn làm **Điểm neo tri thức (Ground Truth)** vào System Instruction và Prompt của LLM.
5. Nếu sinh viên viết code bị **nghịch đảo thứ tự** (ví dụ: Thanh toán trước khi Gọi món) hoặc **thiếu bước bắt buộc**: LLM sẽ nhận diện được lỗi và ưu tiên đặt câu hỏi chất vấn sinh viên về sự sai lệch này.

## Prompt

`PACE.Application/QuestionGeneration/QuestionPromptBuilder.cs` chép **nguyên văn** prompt của
`AIService.GenerateUnifiedQuestionsAsync` (Repo_Into_Graph) — prompt dùng chung cho cả ba arm của bài báo.
Model `gpt-4o-mini`, Responses API, reasoning `none`, temperature 0.3 và Structured Outputs (JSON schema), retry lùi luỹ thừa khi 429/5xx.
Lớp gọi API bọc mảng trong `{ "questions": [...] }` để đáp ứng schema; parser hiện có đọc được định dạng này.
Prompt nghiệp vụ giữ nguyên; đổi model nên kết quả không còn cùng baseline Gemini của bài báo.
Không sửa nội dung prompt nếu muốn kết quả còn so sánh được với bài báo.

## Cấu hình

`appsettings.json`:

```json
"QuestionGeneration": { "Provider": "Gemini", "GraphNodeThreshold": 10, "MaxQuestions": 20, "Temperature": 0.3 },
"OpenAI": { "ApiKey": "", "Model": "gpt-4o-mini", "TimeoutSeconds": 600, "MaxRetries": 5, "InitialRetryDelaySeconds": 5 }
```

API key đặt trong `appsettings.Development.json` (đã gitignore): `"OpenAI": { "ApiKey": "..." }` hoặc biến môi trường `OpenAI__ApiKey`.
Cấu hình `Gemini` vẫn dùng riêng cho gom nhóm nghiệp vụ.
