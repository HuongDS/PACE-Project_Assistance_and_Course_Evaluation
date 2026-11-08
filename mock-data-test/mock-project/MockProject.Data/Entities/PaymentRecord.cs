namespace MockProject.Data.Entities;

public class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Tiền mặt";
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccessful { get; set; } = true;
    public string TransactionCode { get; set; } = string.Empty;
}

