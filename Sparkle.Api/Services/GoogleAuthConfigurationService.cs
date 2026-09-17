namespace Sparkle.Api.Services;

public interface IGoogleAuthConfigurationService
{
    bool IsGoogleAuthConfigured { get; }
}

public class GoogleAuthConfigurationService : IGoogleAuthConfigurationService
{
    public bool IsGoogleAuthConfigured { get; }

    public GoogleAuthConfigurationService(bool isConfigured)
    {
        IsGoogleAuthConfigured = isConfigured;
    }
}
