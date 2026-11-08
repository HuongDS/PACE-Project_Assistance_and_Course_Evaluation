namespace MockProject.Data.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

