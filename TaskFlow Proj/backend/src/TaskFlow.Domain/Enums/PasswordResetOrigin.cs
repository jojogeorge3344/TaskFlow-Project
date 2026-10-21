namespace TaskFlow.Domain.Enums;

/// <summary>Where a password recovery request came from: the Login Screen (unauthenticated dialog)
/// or the Profile Screen (signed-in user).</summary>
public enum PasswordResetOrigin
{
    LoginScreen,
    ProfileScreen
}
