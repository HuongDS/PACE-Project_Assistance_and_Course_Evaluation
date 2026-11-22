namespace PACE.Domain.Entities;

public class BusinessApi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string Controller { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public int OrderIndex { get; set; }

    public Business Business { get; set; } = null!;
}
