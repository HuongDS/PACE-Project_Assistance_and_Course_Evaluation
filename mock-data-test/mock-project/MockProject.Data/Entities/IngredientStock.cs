namespace MockProject.Data.Entities;

public class IngredientStock
{
    public string ItemName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

