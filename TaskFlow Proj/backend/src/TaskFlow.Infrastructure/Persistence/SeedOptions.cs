namespace TaskFlow.Infrastructure.Persistence;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    public string AdministratorPassword { get; set; } = "Admin@123";
}
