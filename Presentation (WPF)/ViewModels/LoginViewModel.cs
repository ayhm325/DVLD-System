using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DVLD.Contracts.Auth;
using DVLD_WPF;
using Microsoft.Extensions.DependencyInjection;
using Presentation.Services;
using Presentation.Services.UI;
using System.Windows;

namespace Presentation.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthSessionManager _authSessionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IApiNotificationService _notifications;
    private readonly IUserNotificationService _userNotifications;

    [ObservableProperty] private bool _rememberMe;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private bool _isSigningIn;

    public bool CanLogin => !IsSigningIn &&
                            !string.IsNullOrWhiteSpace(Username) &&
                            !string.IsNullOrWhiteSpace(Password);

    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsSigningInChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void TogglePasswordVisibility() =>
        IsPasswordVisible = !IsPasswordVisible;

    public LoginViewModel(
        IAuthSessionManager authSessionManager,
        IServiceProvider serviceProvider,
        IApiNotificationService notifications,
        IUserNotificationService userNotifications)
    {
        _authSessionManager = authSessionManager ?? throw new ArgumentNullException(nameof(authSessionManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _userNotifications = userNotifications ?? throw new ArgumentNullException(nameof(userNotifications));

        RememberMe = Properties.Settings.Default.RememberMe;

        if (RememberMe)
            Username = Properties.Settings.Default.Username;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (!CanLogin)
            return;

        try
        {
            IsSigningIn = true;
            var result = await _authSessionManager.LoginAsync(
                Username.Trim(),
                Password,
                RememberMe);

            if (result.IsFailure)
            {
                _notifications.ShowFailure(result, "Login Failed");
                return;
            }

            if (result.Value is null)
            {
                _userNotifications.ShowError(
                    "The API did not return user authentication data.",
                    "Login Failed");
                return;
            }

            SaveRememberMeSettings();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            System.Windows.Application.Current.MainWindow = mainWindow;
            mainWindow.Show();

            System.Windows.Application.Current.Windows
                .OfType<LoginWindow>()
                .FirstOrDefault()
                ?.Close();
        }
        catch (Exception ex)
        {
            _userNotifications.ShowError(ex.Message, "Login Failed");
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    private void SaveRememberMeSettings()
    {
        if (RememberMe)
        {
            Properties.Settings.Default.Username = Username.Trim();
            Properties.Settings.Default.RememberMe = true;
        }
        else
        {
            Properties.Settings.Default.Username = string.Empty;
            Properties.Settings.Default.RememberMe = false;
        }

        Properties.Settings.Default.Save();
    }
}
