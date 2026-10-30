using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;

namespace TaskFlow.Api.Repositories.Management.EmployeeManagement;

public class EmployeeRepository : BaseRepository<User>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<int> CountHoldingRoleAsync(int roleId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters().CountAsync(user => user.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetHoldingRoleForUpdateAsync(int roleId, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().Where(user => user.RoleId == roleId).ToListAsync(cancellationToken);
}
