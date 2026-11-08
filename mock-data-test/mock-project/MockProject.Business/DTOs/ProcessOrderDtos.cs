namespace MockProject.Business.DTOs;

public class ProcessOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string TableNumber { get; set; } = "Bàn 01";
    public List<OrderItemDto> Items { get; set; } = [];
    public string PaymentMethod { get; set; } = "Tiền mặt"; // Tiền mặt / Thẻ / Ví điện tử
}

public class ProcessOrderResponse
{
    public Guid OrderId { get; set; }
    public bool IsSuccess { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> FlowStepsExecuted { get; set; } = [];
}

