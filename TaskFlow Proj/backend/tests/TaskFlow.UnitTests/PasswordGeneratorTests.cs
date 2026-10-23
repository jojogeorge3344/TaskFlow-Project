using TaskFlow.Application.Auth;

namespace TaskFlow.UnitTests;

public class PasswordGeneratorTests
{
    private const string LookAlikes = "0O1lI";

    [Fact]
    public void Generate_returns_a_password_of_the_documented_length()
    {
        var password = PasswordGenerator.Generate();

        Assert.Equal(PasswordGenerator.Length, password.Length);
        Assert.Equal(16, PasswordGenerator.Length);
    }

    [Fact]
    public void Generate_uses_only_unambiguous_alphanumeric_characters()
    {
        // The password is read from an email and typed by hand, so look-alike characters are excluded.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var password = PasswordGenerator.Generate();

            Assert.All(password, character =>
            {
                Assert.True(char.IsLetterOrDigit(character), $"'{character}' is not alphanumeric.");
                Assert.DoesNotContain(character, LookAlikes);
            });
        }
    }

    [Fact]
    public void Generate_produces_a_different_password_each_time()
    {
        var passwords = Enumerable.Range(0, 100)
            .Select(_ => PasswordGenerator.Generate())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(100, passwords.Count);
    }
}
