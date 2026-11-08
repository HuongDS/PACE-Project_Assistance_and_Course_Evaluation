namespace MockProject.Business.DTOs;

public class PaymentRequest
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Tiền mặt";
}

public class PaymentResponse
{
    public bool IsSuccess { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

