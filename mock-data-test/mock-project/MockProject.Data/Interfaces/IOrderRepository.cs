using MockProject.Data.Entities;

namespace MockProject.Data.Interfaces;

public interface IOrderRepository
{
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<List<Order>> GetAllOrdersAsync();
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
}

