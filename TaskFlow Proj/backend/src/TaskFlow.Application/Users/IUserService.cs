namespace TaskFlow.Application.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken);
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserDto?> GetOwnAsync(int id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces a user's manager and, for a manager, the projects they oversee. Returns null when the user
    /// does not exist. Throws <see cref="InvalidOperationException"/> for an invalid manager, a non-manager given
    /// projects, or a project that does not exist. Administrator-only; the caller enforces that.</summary>
    Task<UserDto?> UpdateManagerAssignmentsAsync(int id, ManagerAssignmentRequest request, CancellationToken cancellationToken);

    /// <summary>Soft-deletes a user and ends their sessions. Returns false when the user does not exist.
    /// Throws <see cref="InvalidOperationException"/> when it is the caller, the last active Administrator,
    /// or someone who still has unfinished assigned tasks.</summary>
    Task<bool> DeleteAsync(int id, int currentUserId, CancellationToken cancellationToken);

    /// <summary>Updates the caller's own full name and email. Nothing else is changeable.</summary>
    Task<UserDto?> UpdateOwnProfileAsync(int id, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes the caller's own password after verifying the current one, ending other sessions.</summary>
    Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's password with one supplied by an administrator (the admin-assisted reset flow):
    /// the same policy checks, hashing and session-ending the regular user edit uses, but touching only the
    /// credential. <paramref name="actingAdminId"/> is the administrator performing the change and is validated
    /// here as well as at the endpoint, so a reset can never run on behalf of a stale authorization. Returns
    /// false when the user does not exist or is deleted; throws <see cref="InvalidOperationException"/> when the
    /// password fails the policy or the acting administrator is not an active account.</summary>
    Task<bool> SetPasswordAsync(int userId, string newPassword, int actingAdminId, CancellationToken cancellationToken = default);
}
