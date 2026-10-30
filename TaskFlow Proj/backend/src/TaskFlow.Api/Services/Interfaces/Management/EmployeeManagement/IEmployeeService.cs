using TaskFlow.Api.Dtos.Management.EmployeeManagement;

namespace TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement;

public interface IEmployeeService
{
    Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken);
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken);

    /// <summary>Soft-deletes a user and ends their sessions. Returns false when the user does not exist.
    /// Throws <see cref="InvalidOperationException"/> when it is the caller, the last active Administrator,
    /// or someone who still has unfinished assigned tasks.</summary>
    Task<bool> DeleteAsync(int id, int currentUserId, CancellationToken cancellationToken);
}
