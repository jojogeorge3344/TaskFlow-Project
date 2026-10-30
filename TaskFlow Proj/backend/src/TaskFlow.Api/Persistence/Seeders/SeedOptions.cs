namespace TaskFlow.Infrastructure.Persistence;

public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Creates the Administrator, Manager and Employee sign-ins when they do not exist yet.</summary>
    public bool Enabled { get; set; }

    public string AdministratorPassword { get; set; } = "123456";

    public string ManagerPassword { get; set; } = "123456";

    public string EmployeePassword { get; set; } = "123456";

}
