# Mock Project - Restaurant Order & Payment Process (Kiến trúc 3-Layer)

Dự án mẫu (Mock Project) được thiết kế theo **Kiến trúc 3 Tầng (3-Layer Architecture)** bằng **ASP.NET Core (.NET 9)**, phục vụ việc kiểm thử hệ thống PACE phân tích mã nguồn và đối chiếu quy trình nghiệp vụ (Ground Truth) từ Draw.io.

---

## 1. Cấu trúc Dự án 3-Layer (.NET 9)

```
mock-project/
├── MockProject.sln
├── template_business.json                      # Định nghĩa 2 nghiệp vụ 01 & 02 cho PACE
├── README.md                                   # Tài liệu hướng dẫn & kịch bản kiểm thử
│
├── MockProject.API/                            # [Tầng 1: Presentation Layer]
│   ├── Controllers/
│   │   ├── StandardOrderController.cs          # Controller nghiệp vụ 01: Luồng chuẩn
│   │   └── FlawedOrderController.cs            # Controller nghiệp vụ 02: Luồng sai (Nghịch đảo)
│   ├── Program.cs                              # Đăng ký Dependency Injection & Swagger UI
│   └── Properties/launchSettings.json
│
├── MockProject.Business/                       # [Tầng 2: Business Logic Layer (BLL)]
│   ├── DTOs/
│   │   ├── OrderItemDto.cs
│   │   ├── PaymentModels.cs
│   │   └── ProcessOrderDtos.cs
│   ├── Interfaces/
│   │   ├── IOrderService.cs
│   │   ├── IKitchenService.cs
│   │   └── IPaymentService.cs
│   └── Services/
│       ├── OrderService.cs                     # Chứa ProcessOrderAsync (chuẩn) & ProcessFlawedOrderAsync (sai)
│       ├── KitchenService.cs                   # Kiểm tra tồn kho nguyên liệu & xác nhận bếp
│       └── PaymentService.cs                   # Xử lý thanh toán hóa đơn
│
└── MockProject.Data/                           # [Tầng 3: Data Access Layer (DAL)]
    ├── Entities/
    │   ├── Order.cs & OrderItem.cs
    │   ├── OrderStatus.cs                      # Pending, Confirmed, Completed, Cancelled
    │   ├── PaymentRecord.cs
    │   └── IngredientStock.cs                  # Tồn kho nguyên liệu bếp
    ├── Interfaces/
    │   ├── IOrderRepository.cs
    │   ├── IKitchenRepository.cs
    │   └── IPaymentRepository.cs
    └── Repositories/
        ├── OrderRepository.cs                  # Lưu trữ & cập nhật trạng thái đơn
        ├── KitchenRepository.cs                # Quản lý kho nguyên liệu & xác nhận bếp
        └── PaymentRepository.cs                # Ghi nhận lịch sử giao dịch
```

---

## 2. Danh sách 2 Nghiệp vụ trong `template_business.json`

| Nghiệp vụ | Controller | Endpoint | Thứ tự luồng thực hiện |
| :--- | :--- | :--- | :--- |
| **Quy Trinh Goi Mon Va Thanh Toan 01** *(Chuẩn)* | `StandardOrderController` | `POST /api/standard-order/process` | Khách chọn món $\rightarrow$ Kiểm tra bếp $\rightarrow$ Bếp xác nhận $\rightarrow$ Thanh toán $\rightarrow$ Hoàn tất |
| **Quy Trinh Goi Mon Va Thanh Toan 02** *(Sai / Nghịch đảo)* | `FlawedOrderController` | `POST /api/flawed-order/process` | **Thu tiền trước** $\rightarrow$ Mới tạo đơn gọi món $\rightarrow$ Mới kiểm tra bếp $\rightarrow$ Bếp hủy đơn nếu hết món |

---

## 3. Chi tiết từng nghiệp vụ

### 3.1. Nghiệp vụ 01: "Quy Trinh Goi Mon Va Thanh Toan 01" (Luồng chuẩn)

* **Controller:** [`StandardOrderController.cs`](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/mock-data-test/mock-project/MockProject.API/Controllers/StandardOrderController.cs)
* **Endpoint:** `POST /api/standard-order/process`
* **Call Stack:**
  1. `StandardOrderController.ProcessOrderAsync`
  2. `IOrderService.ProcessOrderAsync` $\rightarrow$ `OrderService.ProcessOrderAsync`
  3. `IOrderRepository.CreateOrderAsync` $\rightarrow$ `OrderRepository.CreateOrderAsync` *(Tạo đơn)*
  4. `IKitchenService.CheckIngredientsAvailabilityAsync` $\rightarrow$ `KitchenService.CheckIngredientsAvailabilityAsync` *(Kiểm tra bếp)*
  5. `IKitchenService.ConfirmOrderAsync` $\rightarrow$ `KitchenService.ConfirmOrderAsync` *(Bếp xác nhận)*
  6. `IPaymentService.ProcessPaymentAsync` $\rightarrow$ `PaymentService.ProcessPaymentAsync` *(Thanh toán sau cùng)*
  7. `IOrderRepository.UpdateOrderStatusAsync` *(Hoàn tất đơn hàng)*

### 3.2. Nghiệp vụ 02: "Quy Trinh Goi Mon Va Thanh Toan 02" (Luồng sai / Nghịch đảo)

* **Controller:** [`FlawedOrderController.cs`](file:///d:/TaiLieu_FA26/Capstone/Project/PACE-Project_Assistance_and_Course_Evaluation/mock-data-test/mock-project/MockProject.API/Controllers/FlawedOrderController.cs)
* **Endpoint:** `POST /api/flawed-order/process`
* **Call Stack (Bị đảo lộn):**
  1. `FlawedOrderController.ProcessOrderAsync`
  2. `IOrderService.ProcessFlawedOrderAsync` $\rightarrow$ `OrderService.ProcessFlawedOrderAsync`
  3. **`IPaymentService.ProcessPaymentAsync` $\rightarrow$ `PaymentService.ProcessPaymentAsync`** *(Thu tiền khách trước)*
  4. `IOrderRepository.CreateOrderAsync` $\rightarrow$ `OrderRepository.CreateOrderAsync` *(Sau khi trừ tiền mới tạo đơn)*
  5. `IKitchenService.CheckIngredientsAvailabilityAsync` $\rightarrow$ `KitchenService.CheckIngredientsAvailabilityAsync` *(Sau cùng mới kiểm tra bếp)*
  6. `IOrderRepository.UpdateOrderStatusAsync` *(Nếu bếp hết nguyên liệu $\rightarrow$ Đơn bị hủy dù khách đã mất tiền!)*

---

## 4. Hướng dẫn chạy và kiểm thử

### Khởi động dự án:
```bash
dotnet run --project MockProject.API/MockProject.API.csproj
```
Swagger UI: `http://localhost:5042/swagger`.

### Kiểm thử Nghiệp vụ 01 (Luồng chuẩn):
```bash
curl -X POST "http://localhost:5042/api/standard-order/process" \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Nguyen Van A",
    "tableNumber": "Ban 05",
    "paymentMethod": "Vi dien tu",
    "items": [
      { "itemName": "Phở Bò", "quantity": 2, "unitPrice": 50000 },
      { "itemName": "Trà Đào Cam Sả", "quantity": 2, "unitPrice": 30000 }
    ]
  }'
```

### Kiểm thử Nghiệp vụ 02 (Luồng sai khi bếp hết nguyên liệu):
```bash
curl -X POST "http://localhost:5042/api/flawed-order/process" \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Tran Thi B",
    "tableNumber": "Ban 02",
    "paymentMethod": "Vi dien tu",
    "items": [
      { "itemName": "Tôm Hùm Alaska", "quantity": 1, "unitPrice": 500000 }
    ]
  }'
```
* **Kết quả trả về:** Khách hàng đã thanh toán trước thành công, nhưng sau đó nhà bếp báo hết nguyên liệu nên đơn hàng buộc phải hủy.
