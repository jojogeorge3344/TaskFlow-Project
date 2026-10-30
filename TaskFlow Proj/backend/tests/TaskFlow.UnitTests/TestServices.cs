using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Auth;
using TaskFlow.Api.Repositories.Management.DepartmentManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Repositories.Management.ProjectManagement;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement;
using TaskFlow.Api.Repositories.Settings.RoleConfiguration;
using TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.EmployeeManagement;
using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.ProjectManagement;

namespace TaskFlow.UnitTests;

/// <summary>Builds services the way the API does, with real repositories over the test's database, so tests exercise the
/// same queries the API runs.</summary>
internal static class TestServices
{
    public static UserAllocationService Allocations(AppDbContext db) =>
        new(
            new UserAllocationRepository(db),
            new EmployeeRepository(db),
            new ProjectRepository(db),
            new PositionRepository(db),
            new DepartmentRepository(db),
            new UnitOfWork(db));

    public static EmployeeService Employees(AppDbContext db, IPasswordHasher<User> hasher, IUserAllocationService? allocations = null) =>
        new(
            new EmployeeRepository(db),
            new RoleRepository(db),
            new RefreshTokenRepository(db),
            new TaskRepository(db),
            new UnitOfWork(db),
            hasher,
            allocations ?? Allocations(db));

    public static ProjectService Projects(AppDbContext db, IUserAllocationService? allocations = null) =>
        new(new ProjectRepository(db), allocations ?? Allocations(db), new UnitOfWork(db));
}
