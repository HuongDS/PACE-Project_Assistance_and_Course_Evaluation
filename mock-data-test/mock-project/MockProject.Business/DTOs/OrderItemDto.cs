namespace MockProject.Business.DTOs;

public class OrderItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

