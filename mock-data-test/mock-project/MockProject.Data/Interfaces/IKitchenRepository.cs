using MockProject.Data.Entities;

namespace MockProject.Data.Interfaces;

public interface IKitchenRepository
{
    Task<bool> CheckStockAsync(string itemName, int quantity);
    Task<bool> ConfirmOrderInKitchenAsync(Guid orderId);
    Task<List<IngredientStock>> GetInventoryAsync();
    Task<bool> DeductStockAsync(string itemName, int quantity);
}

