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

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetOrderByIdAsync(orderId);
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _orderRepository.GetAllOrdersAsync();
    }
}

