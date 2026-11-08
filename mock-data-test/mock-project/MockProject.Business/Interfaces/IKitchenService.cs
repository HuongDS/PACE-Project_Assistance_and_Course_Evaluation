using MockProject.Business.DTOs;
using MockProject.Data.Entities;

namespace MockProject.Business.Interfaces;

public interface IKitchenService
{
    Task<bool> CheckIngredientsAvailabilityAsync(List<OrderItemDto> items);
    Task<bool> ConfirmOrderAsync(Guid orderId);
    Task<List<IngredientStock>> GetMenuAndInventoryAsync();
}

