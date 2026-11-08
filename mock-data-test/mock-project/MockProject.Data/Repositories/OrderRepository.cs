using System.Collections.Concurrent;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Data.Repositories;

public class OrderRepository : IOrderRepository
{
    private static readonly ConcurrentDictionary<Guid, Order> Orders = new();

    public Task<Order> CreateOrderAsync(Order order)
    {
        Orders[order.Id] = order;
        return Task.FromResult(order);
    }

    public Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        Orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task<List<Order>> GetAllOrdersAsync()
    {
        return Task.FromResult(Orders.Values.OrderByDescending(o => o.CreatedAt).ToList());
    }

    public Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        if (Orders.TryGetValue(orderId, out var order))
        {
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

