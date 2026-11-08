using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Business.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IKitchenService _kitchenService;
    private readonly IPaymentService _paymentService;

    public OrderService(
        IOrderRepository orderRepository,
        IKitchenService kitchenService,
        IPaymentService paymentService)
    {
        _orderRepository = orderRepository;
        _kitchenService = kitchenService;
        _paymentService = paymentService;
    }

    public async Task<ProcessOrderResponse> ProcessOrderAsync(ProcessOrderRequest request)
    {
        var steps = new List<string>();

        // Bước 1: Khách hàng chọn món ăn (Tạo danh sách món cần gọi)
        steps.Add("Bước 1: Khách hàng chọn món ăn & Tạo danh sách món cần gọi");
        var order = new Order
        {
            CustomerName = request.CustomerName,
            TableNumber = request.TableNumber,
            Status = OrderStatus.Pending,
            Items = request.Items.Select(i => new OrderItem
            {
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        var createdOrder = await _orderRepository.CreateOrderAsync(order);

        // Bước 2: Kiểm tra nguyên liệu trong bếp còn đủ không?
        steps.Add("Bước 2: Kiểm tra nguyên liệu trong bếp còn đủ không?");
        var isAvailable = await _kitchenService.CheckIngredientsAvailabilityAsync(request.Items);

        // Nhánh rẽ: Hết nguyên liệu -> Báo hết món và hủy đơn
        if (!isAvailable)
        {
            steps.Add("Nhánh rẽ: Hết nguyên liệu -> Báo hết món và hủy đơn");
            await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Cancelled);

            return new ProcessOrderResponse
            {
                OrderId = createdOrder.Id,
                IsSuccess = false,
                Status = OrderStatus.Cancelled.ToString(),
                TotalAmount = createdOrder.TotalAmount,
                PaymentMethod = request.PaymentMethod,
                Message = "Bếp báo không còn đủ nguyên liệu cho món ăn yêu cầu. Đơn hàng đã bị hủy theo quy trình.",
                FlowStepsExecuted = steps
            };
        }

        // Nhánh rẽ: Còn đủ món -> Nhà bếp xác nhận nhận đơn
        steps.Add("Nhánh rẽ: Còn đủ món -> Nhà bếp xác nhận nhận đơn");
        await _kitchenService.ConfirmOrderAsync(createdOrder.Id);
        await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Confirmed);

        // Bước 3: Sau khi dùng bữa xong -> Khách hàng thanh toán hóa đơn (Tiền mặt / Thẻ / Ví điện tử)
        steps.Add($"Bước 3: Khách hàng thanh toán hóa đơn bằng phương thức '{request.PaymentMethod}'");
        var paymentRequest = new PaymentRequest
        {
            OrderId = createdOrder.Id,
            Amount = createdOrder.TotalAmount,
            PaymentMethod = request.PaymentMethod
        };
        var paymentResult = await _paymentService.ProcessPaymentAsync(paymentRequest);

        if (!paymentResult.IsSuccess)
        {
            return new ProcessOrderResponse
            {
                OrderId = createdOrder.Id,
                IsSuccess = false,
                Status = OrderStatus.Confirmed.ToString(),
                TotalAmount = createdOrder.TotalAmount,
                PaymentMethod = request.PaymentMethod,
                Message = $"Thanh toán thất bại: {paymentResult.Message}",
                FlowStepsExecuted = steps
            };
        }

        // Bước 4: Hoàn tất đơn hàng & Rời quán
        steps.Add("Bước 4: Hoàn tất đơn hàng & Khách hàng rời quán");
        await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Completed);

        return new ProcessOrderResponse
        {
            OrderId = createdOrder.Id,
            IsSuccess = true,
            Status = OrderStatus.Completed.ToString(),
            TotalAmount = createdOrder.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            Message = $"Đơn hàng hoàn tất thành công. Mã giao dịch: {paymentResult.TransactionId}.",
            FlowStepsExecuted = steps
        };
    }

    /// <summary>
    /// LUỒNG SAI / NGHỊCH ĐẢO QUY TRÌNH (Anti-pattern Flow):
    /// Hệ thống thực hiện thu tiền khách hàng TRƯỚC, sau đó mới tạo đơn gọi món và kiểm tra bếp.
    /// Dẫn đến rủi ro: Khách đã bị trừ tiền nhưng bếp hết nguyên liệu dẫn đến hủy đơn mà không hoàn tiền kịp thời.
    /// </summary>
    public async Task<ProcessOrderResponse> ProcessFlawedOrderAsync(ProcessOrderRequest request)
    {
        var steps = new List<string>();
        var estimatedTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var tempOrderId = Guid.NewGuid();

        // BƯỚC 1 (SAI QUY TRÌNH): Khách hàng thanh toán tiền trước khi tạo đơn và kiểm tra bếp
        steps.Add($"Bước 1 (NGHỊCH ĐẢO): Khách hàng thanh toán trước {estimatedTotal:N0} VNĐ qua phương thức '{request.PaymentMethod}'");
        var paymentResult = await _paymentService.ProcessPaymentAsync(new PaymentRequest
        {
            OrderId = tempOrderId,
            Amount = estimatedTotal,
            PaymentMethod = request.PaymentMethod
        });

        if (!paymentResult.IsSuccess)
        {
            return new ProcessOrderResponse
            {
                OrderId = tempOrderId,
                IsSuccess = false,
                Status = OrderStatus.Cancelled.ToString(),
                TotalAmount = estimatedTotal,
                PaymentMethod = request.PaymentMethod,
                Message = $"Thanh toán thất bại: {paymentResult.Message}",
                FlowStepsExecuted = steps
            };
        }

        // BƯỚC 2 (SAI QUY TRÌNH): Đã thu tiền xong mới tạo đơn đặt món ăn
        steps.Add("Bước 2: Sau khi trừ tiền mới tạo đơn gọi món & lưu vào hệ thống");
        var order = new Order
        {
            Id = tempOrderId,
            CustomerName = request.CustomerName,
            TableNumber = request.TableNumber,
            Status = OrderStatus.Pending,
            Items = request.Items.Select(i => new OrderItem
            {
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        var createdOrder = await _orderRepository.CreateOrderAsync(order);

        // BƯỚC 3 (SAI QUY TRÌNH): Đã thu tiền và chốt đơn rồi mới kiểm tra nguyên liệu trong bếp
        steps.Add("Bước 3: Chuyển xuống bếp kiểm tra nguyên liệu xem còn đủ không?");
        var isAvailable = await _kitchenService.CheckIngredientsAvailabilityAsync(request.Items);

        // Rẽ nhánh ngoại lệ: Hết nguyên liệu trong bếp
        if (!isAvailable)
        {
            steps.Add("Nhánh rẽ NGUY HIỂM: Bếp hết nguyên liệu -> Hủy đơn hàng (nhưng tiền đã bị thu từ Bước 1!)");
            await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Cancelled);

            return new ProcessOrderResponse
            {
                OrderId = createdOrder.Id,
                IsSuccess = false,
                Status = OrderStatus.Cancelled.ToString(),
                TotalAmount = createdOrder.TotalAmount,
                PaymentMethod = request.PaymentMethod,
                Message = $"CẢNH BÁO LỖI NGHIỆP VỤ: Đã thu tiền thành công (Mã GD: {paymentResult.TransactionId}), nhưng bếp báo hết nguyên liệu nên đơn hàng bị hủy!",
                FlowStepsExecuted = steps
            };
        }

        // Rẽ nhánh còn đủ món: Nhà bếp xác nhận nhận đơn
        steps.Add("Nhánh rẽ: Còn đủ món -> Nhà bếp xác nhận nhận đơn");
        await _kitchenService.ConfirmOrderAsync(createdOrder.Id);
        await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Confirmed);

        // Bước 4: Hoàn tất đơn hàng
        steps.Add("Bước 4: Hoàn tất đơn hàng (Vì đã thu tiền từ trước)");
        await _orderRepository.UpdateOrderStatusAsync(createdOrder.Id, OrderStatus.Completed);

        return new ProcessOrderResponse
        {
            OrderId = createdOrder.Id,
            IsSuccess = true,
            Status = OrderStatus.Completed.ToString(),
            TotalAmount = createdOrder.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            Message = $"Đơn hàng hoàn tất theo luồng tính tiền trước. Mã giao dịch: {paymentResult.TransactionId}.",
            FlowStepsExecuted = steps
        };
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetOrderByIdAsync(orderId);
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _orderRepository.GetAllOrdersAsync();
    }
}

