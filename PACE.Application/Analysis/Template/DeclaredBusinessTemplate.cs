using PACE.Domain.Entities;

namespace PACE.Application.Analysis.Template;

public static class DeclaredBusinessTemplate
{
    public static List<BusinessTemplate> Build(IEnumerable<Business> businesses)
        => businesses
            .OrderBy(b => b.OrderIndex)
            .ThenBy(b => b.CreatedAt)
            .Select(b => new BusinessTemplate
            {
                BusinessId = b.Id,
                BusinessName = b.BusinessName,
                Description = b.Description,
                Apis = b.Apis
                    .OrderBy(a => a.OrderIndex)
                    .Select(a => new ApiTemplate
                    {
                        Controller = a.Controller,
                        Function = a.Function,
                        HttpMethod = a.HttpMethod,
                        Path = a.Path
                    })
                    .ToList()
            })
            .ToList();
}
