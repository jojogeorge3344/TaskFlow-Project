using FluentValidation.TestHelper;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Settings.Validators;
using TaskFlow.Domain.Enums;

namespace TaskFlow.UnitTests;

public class EmailSettingsValidatorTests
{
    private readonly UpdateEmailSettingsRequestValidator _validator = new();
    private readonly SendTestEmailRequestValidator _testValidator = new();

    private static UpdateEmailSettingsRequest Valid() =>
        new(true, "smtp.example.com", 587, SmtpSecurity.StartTls, "taskflow@example.com", "TaskFlow", "taskflow", "09:00");

    [Fact]
    public void Complete_configuration_is_valid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Disabled_email_allows_empty_server_and_sender()
    {
        _validator.TestValidate(Valid() with { EmailEnabled = false, SmtpHost = "", SenderAddress = null })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void Port_outside_range_is_rejected(int port)
    {
        _validator.TestValidate(Valid() with { SmtpPort = port }).ShouldHaveValidationErrorFor(settings => settings.SmtpPort);
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("09:60")]
    [InlineData("nine")]
    [InlineData("")]
    public void Invalid_send_time_is_rejected(string sendTime)
    {
        _validator.TestValidate(Valid() with { SendTime = sendTime }).ShouldHaveValidationErrorFor(settings => settings.SendTime);
    }

    [Theory]
    [InlineData("00:00")]
    [InlineData("23:59")]
    public void Boundary_send_times_are_valid(string sendTime)
    {
        _validator.TestValidate(Valid() with { SendTime = sendTime }).ShouldNotHaveValidationErrorFor(settings => settings.SendTime);
    }

    [Fact]
    public void Enabling_without_a_server_is_rejected()
    {
        _validator.TestValidate(Valid() with { SmtpHost = "" }).ShouldHaveValidationErrorFor(settings => settings.SmtpHost);
    }

    [Fact]
    public void Enabling_without_a_sender_is_rejected()
    {
        _validator.TestValidate(Valid() with { SenderAddress = " " }).ShouldHaveValidationErrorFor(settings => settings.SenderAddress);
    }

    [Fact]
    public void Invalid_sender_address_is_rejected_even_when_disabled()
    {
        _validator.TestValidate(Valid() with { EmailEnabled = false, SenderAddress = "not-an-email" })
            .ShouldHaveValidationErrorFor(settings => settings.SenderAddress);
    }

    [Fact]
    public void Missing_required_values_are_rejected()
    {
        var result = _validator.TestValidate(new UpdateEmailSettingsRequest(null, null, null, null, null, null, null, null));

        result.ShouldHaveValidationErrorFor(settings => settings.EmailEnabled);
        result.ShouldHaveValidationErrorFor(settings => settings.SmtpPort);
        result.ShouldHaveValidationErrorFor(settings => settings.SmtpSecurity);
        result.ShouldHaveValidationErrorFor(settings => settings.SendTime);
    }

    [Fact]
    public void Undefined_security_is_rejected()
    {
        _validator.TestValidate(Valid() with { SmtpSecurity = (SmtpSecurity)42 })
            .ShouldHaveValidationErrorFor(settings => settings.SmtpSecurity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin@example.com")]
    public void Test_address_is_optional_but_valid(string? address)
    {
        _testValidator.TestValidate(new SendTestEmailRequest(address)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_test_address_is_rejected()
    {
        _testValidator.TestValidate(new SendTestEmailRequest("abc")).ShouldHaveValidationErrorFor(request => request.ToAddress);
    }

    [Fact]
    public void Configuration_check_reports_each_problem()
    {
        var problems = EmailConfigurationCheck.Problems("", "", "user", passwordConfigured: false);

        Assert.Equal(
            [EmailConfigurationCheck.MissingHost, EmailConfigurationCheck.MissingSender, EmailConfigurationCheck.MissingPassword],
            problems);
    }

    [Fact]
    public void Configuration_check_is_clean_for_a_complete_configuration()
    {
        Assert.Empty(EmailConfigurationCheck.Problems("smtp.example.com", "a@example.com", "user", passwordConfigured: true));
        // A relay that needs no login needs no password either.
        Assert.Empty(EmailConfigurationCheck.Problems("localhost", "a@example.com", "", passwordConfigured: false));
    }

    [Fact]
    public void Smtp_settings_never_print_the_password()
    {
        var settings = new SmtpSettings("smtp.example.com", 587, SmtpSecurity.StartTls, "a@example.com", "TaskFlow", "user", "S3cret!Pass");

        Assert.DoesNotContain("S3cret!Pass", settings.ToString());
        Assert.Contains("***", settings.ToString());
    }
}
