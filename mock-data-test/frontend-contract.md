# API Contract: Module Sinh câu hỏi vấn đáp & Sơ đồ chuẩn Draw.io

Tài liệu đặc tả hợp đồng API (API Contract) dành cho Frontend Developer để xây dựng giao diện quản lý sơ đồ nghiệp vụ và tạo câu hỏi vấn đáp cho sinh viên.

---

## 1. TypeScript Types & Interfaces

```typescript
// ==========================================
// 1. ENUMS & CÁC GIÁ TRỊ CỐ ĐỊNH
// ==========================================

export type QuestionGenerationMode = 'Raw' | 'Graph' | 'Hybrid';
export type QuestionDifficulty = 'Dễ' | 'Trung bình' | 'Khó';
export type AppliedContextType = 'Raw' | 'Graph';

// ==========================================
// 2. CONTRACT: QUẢN LÝ SƠ ĐỒ DRAW.IO (GROUND TRUTH)
// ==========================================

/** Thông tin Sơ đồ quy trình chuẩn sau khi upload hoặc get */
export interface DrawioProcessDiagramDto {
  businessId: string;
  diagramName: string;
  nodeCount: number;         // Tổng số bước/hành động
  edgeCount: number;         // Tổng số liên kết chuyển tiếp
  pathCount: number;         // Số kịch bản luồng thực thi bóc tách được
  normalizedFlowText: string;// Nội dung tóm tắt quy trình dạng Markdown
  processedAt: string;       // ISO Date string
}

/** Request gửi nội dung XML thô qua chuỗi JSON */
export interface DrawioUploadRequest {
  rawXml: string;
  diagramName?: string;
}

// ==========================================
// 3. CONTRACT: SINH CÂU HỎI VẤN ĐÁP
// ==========================================

/** Request Body gửi lên để sinh câu hỏi */
export interface GenerateQuestionsRequest {
  mode: QuestionGenerationMode;     // Mặc định: "Hybrid"
  numberOfQuestions: number;        // Từ 1 đến 20 (mặc định: 5)
  difficulty: QuestionDifficulty;   // "Dễ" | "Trung bình" | "Khó"
  description?: string;             // Hướng dẫn thêm từ giảng viên (tuỳ chọn)
  replaceExisting: boolean;         // true: xoá các câu cũ chưa có sinh viên trả lời
}

/** Chi tiết một câu hỏi được tạo ra */
export interface GeneratedQuestionDto {
  id: string;
  businessId: string;
  questionText: string;             // Nội dung câu hỏi tình huống (100% Business language)
  suggestedAnswer: string | null;   // Gợi ý đáp án
  difficulty: string;               // Độ khó
  contextType: AppliedContextType;  // "Raw" hoặc "Graph"
  createdAt: string;                // ISO Date string
  answerCount: number;              // Số sinh viên đã trả lời
  targetedEntryPoints?: string[];   // Call stack: [Controller.Action -> Service -> Repo]
}

/** Response Body trả về khi sinh câu hỏi thành công */
export interface GenerateQuestionsResponse {
  businessId: string;
  businessName: string;
  requestedMode: QuestionGenerationMode;
  appliedContext: AppliedContextType;
  nodeCount: number;                // Số node luồng code của sinh viên
  graphNodeThreshold: number;       // Ngưỡng phân tầng (mặc định 10)
  routingReason: string;            // Lý do chọn Raw hay Graph
  flowCount: number;
  methodCount: number;
  model: string;                    // "gpt-4o-mini" / "gemini-3.1-flash-lite"
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  deletedCount: number;             // Số câu cũ đã xoá nếu replaceExisting = true
  questions: GeneratedQuestionDto[];
  hasGroundTruth: boolean;          // TRUE: Đã áp dụng sơ đồ chuẩn để bắt lỗi ngược luồng
  promptText?: string;              // 🔥 Toàn bộ nội dung prompt gửi tới LLM
  fullPrompt?: string;              // Alias của promptText (tiện dùng tuỳ frontend)
}
```

---

## 2. Danh sách Endpoints API

Tất cả các endpoint dưới đây đều yêu cầu Header xác thực quyền Admin:
```http
Authorization: Bearer <ADMIN_JWT_TOKEN>
```

### 2.1. Quản lý Sơ đồ chuẩn Draw.io (Specification Ground Truth)

#### 1. Upload file `.xml` hoặc `.drawio`
* **Method:** `POST`
* **URL:** `/api/businesses/{businessId}/specification/drawio/file`
* **Content-Type:** `multipart/form-data`
* **Request Form:** 
  * `file`: File nhị phân `.xml` hoặc `.drawio`
* **Success Status:** `200 OK`
* **Response Body:** `DrawioProcessDiagramDto`

#### 2. Upload chuỗi XML qua JSON
* **Method:** `POST`
* **URL:** `/api/businesses/{businessId}/specification/drawio`
* **Content-Type:** `application/json`
* **Request Body:** `DrawioUploadRequest`
* **Success Status:** `200 OK`
* **Response Body:** `DrawioProcessDiagramDto`

#### 3. Xem sơ đồ chuẩn đã lưu
* **Method:** `GET`
* **URL:** `/api/businesses/{businessId}/specification/drawio`
* **Success Status:** `200 OK` (hoặc `404 Not Found` nếu chưa có sơ đồ)
* **Response Body:** `DrawioProcessDiagramDto`

#### 4. Xóa sơ đồ chuẩn tạm
* **Method:** `DELETE`
* **URL:** `/api/businesses/{businessId}/specification/drawio`
* **Success Status:** `204 No Content`

---

### 2.2. Sinh câu hỏi vấn đáp

#### 5. Kích hoạt sinh câu hỏi
* **Method:** `POST`
* **URL:** `/api/businesses/{businessId}/questions/generate`
* **Content-Type:** `application/json`
* **Request Body:** `GenerateQuestionsRequest`
* **Success Status:** `200 OK`
* **Response Body:** `GenerateQuestionsResponse`

#### 6. Lấy danh sách câu hỏi đã lưu của Business
* **Method:** `GET`
* **URL:** `/api/businesses/{businessId}/questions`
* **Success Status:** `200 OK`
* **Response Body:** `GeneratedQuestionDto[]`

#### 7. Xóa một câu hỏi
* **Method:** `DELETE`
* **URL:** `/api/questions/{id}`
* **Success Status:** `204 No Content` (hoặc `409 Conflict` nếu đã có sinh viên trả lời)

---

## 3. Mã nguồn Service mẫu cho Frontend (Axios)

```typescript
import axios from 'axios';
import { 
  DrawioProcessDiagramDto, 
  DrawioUploadRequest,
  GenerateQuestionsRequest, 
  GenerateQuestionsResponse,
  GeneratedQuestionDto 
} from './question-api.contract';

const api = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'https://localhost:7084',
});

// Gắn JWT token tự động
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const questionApiService = {
  // 1. Upload file XML/Drawio trực tiếp
  uploadSpecificationFile: async (businessId: string, file: File): Promise<DrawioProcessDiagramDto> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await api.post<DrawioProcessDiagramDto>(
      `/api/businesses/${businessId}/specification/drawio/file`, 
      formData, 
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return res.data;
  },

  // 2. Upload chuỗi XML thô qua JSON
  uploadSpecificationJson: async (businessId: string, payload: DrawioUploadRequest): Promise<DrawioProcessDiagramDto> => {
    const res = await api.post<DrawioProcessDiagramDto>(
      `/api/businesses/${businessId}/specification/drawio`, 
      payload
    );
    return res.data;
  },

  // 3. Lấy sơ đồ chuẩn đang lưu của Business
  getSpecification: async (businessId: string): Promise<DrawioProcessDiagramDto | null> => {
    try {
      const res = await api.get<DrawioProcessDiagramDto>(`/api/businesses/${businessId}/specification/drawio`);
      return res.data;
    } catch (err: any) {
      if (err.response?.status === 404) return null; // Chưa nạp sơ đồ
      throw err;
    }
  },

  // 4. Xóa sơ đồ chuẩn của Business
  deleteSpecification: async (businessId: string): Promise<void> => {
    await api.delete(`/api/businesses/${businessId}/specification/drawio`);
  },

  // 5. Sinh câu hỏi vấn đáp
  generateQuestions: async (businessId: string, payload: GenerateQuestionsRequest): Promise<GenerateQuestionsResponse> => {
    const res = await api.post<GenerateQuestionsResponse>(
      `/api/businesses/${businessId}/questions/generate`, 
      payload
    );
    return res.data;
  },

  // 6. Lấy danh sách câu hỏi đã lưu
  getQuestions: async (businessId: string): Promise<GeneratedQuestionDto[]> => {
    const res = await api.get<GeneratedQuestionDto[]>(`/api/businesses/${businessId}/questions`);
    return res.data;
  },

  // 7. Xóa 1 câu hỏi
  deleteQuestion: async (questionId: string): Promise<void> => {
    await api.delete(`/api/questions/${questionId}`);
  }
};
```

---

## 4. Dữ liệu Mock mẫu (Dùng khi chưa bật Backend)

### 4.1. Mock Response Sơ đồ Draw.io (`DrawioProcessDiagramDto`)
```json
{
  "businessId": "d3b07384-d113-49d6-b0cf-5f80b2a59a72",
  "diagramName": "Quy Trinh Goi Mon Va Thanh Toan",
  "nodeCount": 6,
  "edgeCount": 5,
  "pathCount": 2,
  "normalizedFlowText": "### QUY TRÌNH NGHIỆP VỤ CHUẨN: Quy Trinh Goi Mon Va Thanh Toan\n1. DANH SÁCH BƯỚC:\n  - Bước 1: [Hành động] Khách hàng chọn món ăn\n  - Bước 2: [Cổng rẽ nhánh] Kiểm tra nguyên liệu còn đủ không?\n  - Bước 3: [Hành động] Nhà bếp xác nhận đơn\n  - Bước 4: [Hành động] Khách hàng thanh toán hóa đơn\n...",
  "processedAt": "2026-11-08T09:30:00.000Z"
}
```

### 4.2. Mock Response Tạo câu hỏi (`GenerateQuestionsResponse`)
```json
{
  "businessId": "d3b07384-d113-49d6-b0cf-5f80b2a59a72",
  "businessName": "Quản lý Đặt món Nhà hàng",
  "requestedMode": "Hybrid",
  "appliedContext": "Graph",
  "nodeCount": 12,
  "graphNodeThreshold": 10,
  "routingReason": "Mode Hybrid: luồng có 12 node > 10 nên dùng Graph",
  "flowCount": 2,
  "methodCount": 8,
  "model": "gpt-4o-mini",
  "inputTokens": 1450,
  "outputTokens": 320,
  "latencyMs": 2100,
  "deletedCount": 0,
  "hasGroundTruth": true,
  "promptText": "=== SYSTEM INSTRUCTION ===\nBạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm...\n\n=== USER PROMPT ===\n--- QUY TRÌNH NGHIỆP VỤ CHUẨN ĐÃ CHỐT VỚI GIẢNG VIÊN ---\n...",
  "fullPrompt": "=== SYSTEM INSTRUCTION ===\nBạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm...\n\n=== USER PROMPT ===\n--- QUY TRÌNH NGHIỆP VỤ CHUẨN ĐÃ CHỐT VỚI GIẢNG VIÊN ---\n...",
  "questions": [
    {
      "id": "78326a51-d309-4ce4-897e-1288fbc8d001",
      "businessId": "d3b07384-d113-49d6-b0cf-5f80b2a59a72",
      "questionText": "Nếu khách hàng thanh toán tiền thành công nhưng khi chuyển thông tin sang nhà bếp thì nguyên liệu của món ăn đã hết, hệ thống sẽ xử lý hoàn tiền hoặc hủy đơn như thế nào?",
      "suggestedAnswer": "Hệ thống cần kích hoạt giao dịch hoàn tiền hoặc thông báo cho nhân viên phục vụ để hỗ trợ khách đổi món tương đương.",
      "difficulty": "Trung bình",
      "contextType": "Graph",
      "createdAt": "2026-11-08T09:35:00.000Z",
      "answerCount": 0,
      "targetedEntryPoints": [
        "OrderController.Payment",
        "IOrderService.ProcessPaymentAsync",
        "OrderServiceImpl.ProcessPaymentAsync"
      ]
    }
  ]
}
```

