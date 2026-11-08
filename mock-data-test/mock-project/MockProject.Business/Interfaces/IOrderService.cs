using MockProject.Business.DTOs;
using MockProject.Data.Entities;

namespace MockProject.Business.Interfaces;

public interface IOrderService
{
    Task<ProcessOrderResponse> ProcessOrderAsync(ProcessOrderRequest request);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<List<Order>> GetAllOrdersAsync();
}

