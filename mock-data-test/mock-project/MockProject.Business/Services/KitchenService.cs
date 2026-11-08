using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Business.Services;

public class KitchenService : IKitchenService
{
    private readonly IKitchenRepository _kitchenRepository;

    public KitchenService(IKitchenRepository kitchenRepository)
    {
        _kitchenRepository = kitchenRepository;
    }

    public async Task<bool> CheckIngredientsAvailabilityAsync(List<OrderItemDto> items)
    {
        foreach (var item in items)
        {
            var isAvailable = await _kitchenRepository.CheckStockAsync(item.ItemName, item.Quantity);
            if (!isAvailable)
            {
                return false;
            }
        }
        return true;
    }

    public async Task<bool> ConfirmOrderAsync(Guid orderId)
    {
        return await _kitchenRepository.ConfirmOrderInKitchenAsync(orderId);
    }

    public async Task<List<IngredientStock>> GetMenuAndInventoryAsync()
    {
        return await _kitchenRepository.GetInventoryAsync();
    }
}

