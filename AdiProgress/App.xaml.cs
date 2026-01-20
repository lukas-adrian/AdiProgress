using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using AdiProgress.Configuration;
using Microsoft.Extensions.Configuration;

namespace AdiProgress;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "Global\\AdiProgressMutex";
    private Mutex _mutex;
    private bool _createdNew;
    public static AppSettings Settings { get; private set; }
    
    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out _createdNew);

        if (!_createdNew)
        {
            // Another instance is running - verify it's alive
            var processes = Process.GetProcessesByName("AdiProgress");
                
            if (processes.Any(p => p.Id != Environment.ProcessId))
            {
                // Another process exists, exit silently
                Shutdown();
                return;
            }
                
            // Mutex exists but no process - it was abandoned, continue
        }

        base.OnStartup(e);
        
        // Load configuration
        Settings = LoadConfiguration();
            
        // Apply theme
        ApplyTheme(Settings.Theme);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
    
    private AppSettings LoadConfiguration()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            var builder = new ConfigurationBuilder()
                .SetBasePath(baseDir)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.user.json", optional: true);

            var configuration = builder.Build();
            var settings = new AppSettings();
            configuration.Bind(settings);
            return settings;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Config error: {ex.Message}");
            return new AppSettings(); // Use defaults
        }
    }
    
    private void ApplyTheme(ThemeConfig theme)
    {
        try
        {
            // Colors
            Current.Resources["BackgroundBrush"] = CreateBrush(theme.BackgroundColor);
            Current.Resources["CardBrush"] = CreateBrush(theme.CardColor);
            Current.Resources["AccentBrush"] = CreateBrush(theme.AccentColor);
            Current.Resources["TextPrimaryBrush"] = CreateBrush(theme.TextPrimaryColor);
            Current.Resources["TextSecondaryBrush"] = CreateBrush(theme.TextSecondaryColor);
            Current.Resources["BorderBrush"] = CreateBrush(theme.BorderColor);
            Current.Resources["ProgressBrush"] = CreateBrush(theme.ProgressColor);
            Current.Resources["CancelBrush"] = CreateBrush(theme.CancelColor);

            // Fonts
            Current.Resources["MainFontFamily"] = new FontFamily(theme.FontFamily);
            Current.Resources["MainFontSize"] = theme.FontSize;
            Current.Resources["HeaderFontSize"] = theme.HeaderFontSize;

            // Dimensions
            Current.Resources["WindowWidth"] = theme.WindowWidth;
            Current.Resources["WindowMinHeight"] = theme.WindowMinHeight;
            Current.Resources["WindowMaxHeight"] = theme.WindowMaxHeight;
            Current.Resources["ProgressBarHeight"] = theme.ProgressBarHeight;
            Current.Resources["BorderRadius"] = new CornerRadius(theme.BorderRadius);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying theme: {ex.Message}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private SolidColorBrush CreateBrush(string colorHex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }
}