namespace AdiProgress.Configuration;

public class AppSettings
{
    public string WindowTitle { get; set; } = "Progress";
    public ThemeConfig Theme { get; set; } = new ThemeConfig();
}

public class ThemeConfig
{
    public string BackgroundColor { get; set; } = "#F5F5F5";
    public string CardColor { get; set; } = "#FFFFFF";
    public string AccentColor { get; set; } = "#2D3436";
    public string TextPrimaryColor { get; set; } = "#212121";
    public string TextSecondaryColor { get; set; } = "#757575";
    public string BorderColor { get; set; } = "#E0E0E0";
    public string ProgressColor { get; set; } = "#2ECC71";
    public string CancelColor { get; set; } = "#F44336";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 12;
    public double HeaderFontSize { get; set; } = 14;
    public double WindowWidth { get; set; } = 450;
    public double WindowMinHeight { get; set; } = 120;
    public double WindowMaxHeight { get; set; } = 600;
    public double ProgressBarHeight { get; set; } = 6;
    public double BorderRadius { get; set; } = 12;
}