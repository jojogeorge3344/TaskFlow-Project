using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile.Validators;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Dtos.Management.EmployeeManagement.Validators;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Exceptions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests.Common;

public class EmailPolicyTests
{
    [Theory]
    [InlineData("jane.doe@taskflow.com")]
    [InlineData("Jane.Doe@TaskFlow.com")]
    [InlineData("  a_b-c1@taskflow.com ")]
    public void Valid_addresses_pass(string email) => Assert.True(EmailPolicy.IsValid(email));

    [Theory]
    [InlineData("")]
    [InlineData("@taskflow.com")]
    [InlineData("jane@gmail.com")]
    [InlineData("jane@taskflow.local")]
    [InlineData("jane@sub.taskflow.com")]
    [InlineData("jane")]
    [InlineData("ja ne@taskflow.com")]
    [InlineData("ja@ne@taskflow.com")]
    [InlineData(".jane@taskflow.com")]
    [InlineData("jane.@taskflow.com")]
    [InlineData("ja..ne@taskflow.com")]
    public void Invalid_addresses_fail(string email) => Assert.False(EmailPolicy.IsValid(email));

    [Fact]
    public void Local_part_is_limited_to_64_characters()
    {
        Assert.True(EmailPolicy.IsValid(new string('a', 64) + EmailPolicy.Suffix));
        Assert.False(EmailPolicy.IsValid(new string('a', 65) + EmailPolicy.Suffix));
    }

    [Fact]
    public void Normalize_trims_and_lowercases()
    {
        Assert.Equal("jane@taskflow.com", EmailPolicy.Normalize("  Jane@TaskFlow.com "));
        Assert.Equal(string.Empty, EmailPolicy.Normalize(null));
    }
}

public class UserEmailValidatorTests
{
    [Fact]
    public void Create_rejects_other_domain_and_accepts_fixed_domain()
    {
        var validator = new CreateUserRequestValidator();

        validator.TestValidate(Create("a@gmail.com")).ShouldHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(Create("a@taskflow.com")).ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Update_rejects_other_domain_and_accepts_fixed_domain()
    {
        var validator = new UpdateUserRequestValidator();

        validator.TestValidate(new UpdateUserRequest(null, "A", "a", "a@gmail.com", 3, null))
            .ShouldHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(new UpdateUserRequest(null, "A", "a", "a@taskflow.com", 3, null))
            .ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Own_profile_rejects_other_domain_and_accepts_fixed_domain()
    {
        var validator = new UpdateOwnProfileRequestValidator();

        validator.TestValidate(new UpdateOwnProfileRequest("A", "a@gmail.com")).ShouldHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(new UpdateOwnProfileRequest("A", "a@taskflow.com")).ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    private static CreateUserRequest Create(string email) =>
        new(null, "A", "a", email, "secret1", SystemRoles.EmployeeId, true, null);
}

public class UserEmailUniquenessTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly EmployeeService _service;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public UserEmailUniquenessTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);
        _service = TestServices.Employees(_db, _hasher);
    }

    private User AddUser(string username, string email, bool deleted = false)
    {
        var user = new User
        {
            FullName = username,
            Username = username,
            Email = email,
            RoleId = SystemRoles.EmployeeId,
            IsActive = true,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = "stamp"
        };
        user.PasswordHash = _hasher.HashPassword(user, "P@ssw0rd!");
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private static CreateUserRequest Create(string username, string email) =>
        new(null, username, username, email, "secret1", SystemRoles.EmployeeId, true, null);

    [Fact]
    public async Task Create_lowercases_the_address()
    {
        var dto = await _service.CreateAsync(Create("jane", "Jane.Doe@TaskFlow.com"), default);

        Assert.Equal("jane.doe@taskflow.com", dto.Email);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_address_ignoring_case()
    {
        AddUser("jane", "jane@taskflow.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Create("other", "JANE@taskflow.com"), default));
        Assert.Contains("Email already exists", ex.Message);
    }

    [Fact]
    public async Task Create_rejects_the_same_local_part_on_a_legacy_domain()
    {
        AddUser("jane", "jane@gmail.com");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Create("other", "jane@taskflow.com"), default));
    }

    [Fact]
    public async Task Create_rejects_an_address_belonging_to_a_deleted_user()
    {
        AddUser("gone", "gone@taskflow.com", deleted: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Create("other", "gone@taskflow.com"), default));
    }

    [Fact]
    public async Task Create_rejects_a_username_that_belongs_to_a_deleted_user()
    {
        AddUser("gone", "gone@taskflow.com", deleted: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Create("GONE", "fresh@taskflow.com"), default));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Update_rejects_taking_a_deleted_users_username()
    {
        AddUser("gone2", "gone2@taskflow.com", deleted: true);
        var other = AddUser("other2", "other2@taskflow.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(other.Id, new UpdateUserRequest(null, "Other", "gone2", "other2@taskflow.com", SystemRoles.EmployeeId, null), default));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Create_rejects_another_domain()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateAsync(Create("other", "other@gmail.com"), default));
    }

    [Fact]
    public async Task Own_profile_save_with_unchanged_email_is_not_a_duplicate()
    {
        var user = AddUser("self", "self@taskflow.com");

        var dto = await TestServices.Profile(_db, _hasher).UpdateAsync(user.Id, new UpdateOwnProfileRequest("New Name", "self@taskflow.com"));

        Assert.Equal("New Name", dto!.FullName);
    }

    [Fact]
    public async Task Update_rejects_taking_another_users_address()
    {
        AddUser("first", "first@taskflow.com");
        var second = AddUser("second", "second@taskflow.com");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(second.Id, new UpdateUserRequest(null, "Second", "second", "first@taskflow.com", SystemRoles.EmployeeId, null), default));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
