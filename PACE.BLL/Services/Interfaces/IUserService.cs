using PACE.BLL.Dtos;
using PACE.DAL.Repositories;

namespace PACE.BLL.Services.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ImportUsersResult> ImportFromExcelAsync(Stream excelStream, CancellationToken ct = default);
}
