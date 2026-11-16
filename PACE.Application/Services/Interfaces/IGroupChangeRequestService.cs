using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IGroupChangeRequestService
{
    Task<GroupChangeRequestDto> CreateTopicChangeRequestAsync(Guid groupId, CreateTopicChangeRequest request, CancellationToken ct = default);
    Task<GroupChangeRequestDto> CreateMemberChangeRequestAsync(Guid groupId, CreateMemberChangeRequest request, CancellationToken ct = default);
    Task<GroupChangeRequestDto> CreateLeaderChangeRequestAsync(Guid groupId, CreateLeaderChangeRequest request, CancellationToken ct = default);

    Task<List<GroupChangeRequestDto>> GetGroupChangeRequestsAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupChangeRequestDto> GetChangeRequestByIdAsync(Guid requestId, CancellationToken ct = default);
    Task CancelChangeRequestAsync(Guid groupId, Guid requestId, CancellationToken ct = default);

    Task<List<GroupChangeRequestDto>> GetClassChangeRequestsAsync(Guid classId, CancellationToken ct = default);
    Task<GroupChangeRequestDto> ReviewChangeRequestAsync(Guid groupId, Guid requestId, ReviewChangeRequest request, CancellationToken ct = default);
}
