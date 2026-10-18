using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class SessionConfigurationValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyCustomerTag(string? customerTag)
    {
        var result = SessionConfigurationValidator.Validate(Configuration(customerTag, "Mailbox repair"));

        Assert.False(result.IsValid);
        Assert.Contains(SessionValidationError.CustomerTagRequired, result.Errors);
        Assert.DoesNotContain(SessionValidationError.SessionNameRequired, result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Validate_RejectsEmptySessionName(string? sessionName)
    {
        var result = SessionConfigurationValidator.Validate(Configuration("CONTOSO", sessionName));

        Assert.False(result.IsValid);
        Assert.Contains(SessionValidationError.SessionNameRequired, result.Errors);
        Assert.DoesNotContain(SessionValidationError.CustomerTagRequired, result.Errors);
    }

    [Fact]
    public void Validate_RejectsNamesThatContainOnlyInvalidFilenameCharacters()
    {
        var result = SessionConfigurationValidator.Validate(Configuration(":::", "???"));

        Assert.Contains(SessionValidationError.CustomerTagNotFileSafe, result.Errors);
        Assert.Contains(SessionValidationError.SessionNameNotFileSafe, result.Errors);
    }

    [Fact]
    public void Validate_AcceptsACompleteConfiguration()
    {
        var result = SessionConfigurationValidator.Validate(Configuration("Gdańsk", "Naprawa skrzynki"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_AcceptsReservedNamesBecauseTheyAreMadeFileSafe()
    {
        var result = SessionConfigurationValidator.Validate(Configuration("CON", "NUL"));

        Assert.True(result.IsValid);
    }

    private static SessionConfiguration Configuration(string? customerTag, string? sessionName)
        => new()
        {
            CustomerTag = customerTag ?? "",
            SessionName = sessionName ?? ""
        };
}
