namespace TaskFlow.Application.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken);
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserDto?> GetOwnAsync(int id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken);

    /// <summary>Updates the caller's own full name and email. Nothing else is changeable.</summary>
    Task<UserDto?> UpdateOwnProfileAsync(int id, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes the caller's own password after verifying the current one, ending other sessions.</summary>
    Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
