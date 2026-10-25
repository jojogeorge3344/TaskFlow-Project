using TaskFlow.Application.Assistant;

namespace TaskFlow.UnitTests;

public class SecretScreenTests
{
    [Theory]
    [InlineData("my password is Hunter2!, change it to NewPass#9")]
    [InlineData("password: abc12345")]
    [InlineData("Password = P@ssw0rd!")]
    [InlineData("please change my password to NewPass#9")]
    [InlineData("here is my key sk-proj-abcdefghijklmnopqrstuv")]
    [InlineData("use AIzaSyA1234567890abcdefghijKLMNOP for gemini")]
    [InlineData("Authorization: Bearer abcdefghijklmnopqrstuvwxyz0123")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc")]
    [InlineData("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8")]
    public void SecretsAreCaught(string text)
    {
        Assert.True(SecretScreen.ContainsSecret(text));
    }

    [Theory]
    [InlineData("How do I change my password?")]
    [InlineData("password reset page")]
    [InlineData("The password is incorrect, what should I do")]
    [InlineData("I forgot my password")]
    [InlineData("What should I work on first?")]
    [InlineData("Send a mail to my manager about TF-102")]
    [InlineData("Task TF-1042 is due on 2026-10-20")]
    [InlineData("my password to the building is on the wall")]
    [InlineData("")]
    [InlineData("   ")]
    public void OrdinaryTextIsNotCaught(string text)
    {
        Assert.False(SecretScreen.ContainsSecret(text));
    }

    [Fact]
    public void NullIsNotASecret()
    {
        Assert.False(SecretScreen.ContainsSecret(null));
    }

    [Fact]
    public void RedactReplacesKeysAndPasswordValuesButKeepsTheRest()
    {
        var redacted = SecretScreen.Redact("Use sk-proj-abcdefghijklmnopqrstuv and the password is Hunter2! today");

        Assert.DoesNotContain("abcdefghijklmnopqrstuv", redacted);
        Assert.DoesNotContain("Hunter2", redacted);
        Assert.Contains(SecretScreen.RedactedText, redacted);
        Assert.Contains("Use", redacted);
        Assert.Contains("today", redacted);
    }

    [Fact]
    public void RedactLeavesPlainTextAlone()
    {
        const string text = "TF-102 Fix login redirect bug, due 2026-10-20";

        Assert.Equal(text, SecretScreen.Redact(text));
    }
}
