using System.Collections.Concurrent;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Data.Repositories;

public class KitchenRepository : IKitchenRepository
{
    private static readonly ConcurrentDictionary<string, IngredientStock> Stock = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Phở Bò"] = new() { ItemName = "Phở Bò", AvailableQuantity = 10, UnitPrice = 50000m },
        ["Cơm Gà Hội An"] = new() { ItemName = "Cơm Gà Hội An", AvailableQuantity = 5, UnitPrice = 45000m },
        ["Trà Đào Cam Sả"] = new() { ItemName = "Trà Đào Cam Sả", AvailableQuantity = 20, UnitPrice = 30000m },
        ["Tôm Hùm Alaska"] = new() { ItemName = "Tôm Hùm Alaska", AvailableQuantity = 0, UnitPrice = 500000m } // Hết nguyên liệu để test ngoại lệ
    };

    private static readonly ConcurrentDictionary<Guid, bool> ConfirmedKitchenOrders = new();

    public Task<bool> CheckStockAsync(string itemName, int quantity)
    {
        if (Stock.TryGetValue(itemName, out var stock))
        {
            return Task.FromResult(stock.AvailableQuantity >= quantity);
        }
        // Nếu món không có trong danh mục bếp -> coi như hết nguyên liệu
        return Task.FromResult(false);
    }

    public Task<bool> ConfirmOrderInKitchenAsync(Guid orderId)
    {
        ConfirmedKitchenOrders[orderId] = true;
        return Task.FromResult(true);
    }

    public Task<List<IngredientStock>> GetInventoryAsync()
    {
        return Task.FromResult(Stock.Values.ToList());
    }

    public Task<bool> DeductStockAsync(string itemName, int quantity)
    {
        if (Stock.TryGetValue(itemName, out var stock) && stock.AvailableQuantity >= quantity)
        {
            stock.AvailableQuantity -= quantity;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

