using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace CiderToast;

public partial class SettingsWindow : Window
{
    private readonly Config _cfg;
    private static readonly string[] Corners = { "TopLeft", "TopRight", "BottomRight", "BottomLeft" };
    private static readonly Brush On = new SolidColorBrush(Color.FromRgb(0x55, 0xFF, 0x55));
    private static readonly Brush Off = new SolidColorBrush(Color.FromRgb(0x9A, 0x96, 0xA8));

    private App TheApp => (App)Application.Current;

    public SettingsWindow()
    {
        InitializeComponent();
        FontFamily = ToastWindow.McFont;
        _cfg = TheApp.Cfg.Clone();

        TitleBar.MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
        CloseBtn.Click += (_, _) => Close();

        PositionBtn.Click += (_, _) =>
        {
            int i = Array.IndexOf(Corners, _cfg.Corner);
            _cfg.Corner = Corners[(i + 1 + Corners.Length) % Corners.Length];
            Apply();
        };
        DurMinus.Click += (_, _) => { _cfg.ToastSeconds = Clamp(_cfg.ToastSeconds - 1, 2, 20); Apply(); };
        DurPlus.Click += (_, _) => { _cfg.ToastSeconds = Clamp(_cfg.ToastSeconds + 1, 2, 20); Apply(); };
        ScaleMinus.Click += (_, _) => { _cfg.BorderScale = Clamp(_cfg.BorderScale - 0.5, 1.5, 4.0); Apply(); };
        ScalePlus.Click += (_, _) => { _cfg.BorderScale = Clamp(_cfg.BorderScale + 0.5, 1.5, 4.0); Apply(); };
        ArtBtn.Click += (_, _) => { _cfg.ShowArtwork = !_cfg.ShowArtwork; Apply(); };
        AccentBtn.Click += (_, _) => { _cfg.ColorAccent = !_cfg.ColorAccent; Apply(); };
        AutoBtn.Click += (_, _) => { App.SetAutoStart(!App.IsAutoStart()); Refresh(); };

        PreviewBtn.Click += (_, _) => { Apply(); TheApp.PreviewToast(); };
        SaveBtn.Click += (_, _) => { Apply(); TheApp.SaveConfig(); Close(); };

        Refresh();
    }

    /// <summary>Push the edited config live so the next toast (and Preview) reflects it.</summary>
    private void Apply()
    {
        TheApp.SetCfg(_cfg.Clone());
        Refresh();
    }

    private void Refresh()
    {
        PositionBtn.Content = Pretty(_cfg.Corner);
        DurText.Text = _cfg.ToastSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
        ScaleText.Text = _cfg.BorderScale.ToString("0.#", CultureInfo.InvariantCulture);
        SetToggle(ArtBtn, _cfg.ShowArtwork);
        SetToggle(AccentBtn, _cfg.ColorAccent);
        SetToggle(AutoBtn, App.IsAutoStart());
    }

    private static void SetToggle(System.Windows.Controls.Button b, bool on)
    {
        b.Content = on ? "ON" : "OFF";
        b.Foreground = on ? On : Off;
    }

    private static string Pretty(string corner) => corner switch
    {
        "TopRight" => "Top Right",
        "BottomLeft" => "Bottom Left",
        "BottomRight" => "Bottom Right",
        _ => "Top Left",
    };

    private static double Clamp(double v, double lo, double hi) => Math.Round(Math.Min(hi, Math.Max(lo, v)), 1);
}
