using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Media.Control;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Color = System.Windows.Media.Color;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using MediaProps = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties;

namespace CiderToast;

public partial class App : System.Windows.Application
{
    private Config _cfg = new();
    private Forms.NotifyIcon? _tray;
    private ToastWindow? _current;
    private SettingsWindow? _settings;

    private GlobalSystemMediaTransportControlsSessionManager? _smtc;
    private GlobalSystemMediaTransportControlsSession? _hooked;
    private string _lastKey = "";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "CiderToast";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) => { Log("UI exception: " + ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log("Fatal: " + ex.ExceptionObject);

        try
        {
            LoadConfig();

            if (e.Args.Contains("--smtc"))
            {
                _ = SmtcProbe.Run().ContinueWith(_ => Dispatcher.Invoke(Shutdown));
                return;
            }

            SetupTray();

            if (e.Args.Contains("--test"))
                ShowToast(new NowPlaying("Midnight City", "M83", "Hurry Up, We're Dreaming", 240000, null, DefaultAccent, false));

            if (e.Args.Contains("--settings"))
                OpenSettings();

            _ = InitSmtc();
        }
        catch (Exception ex) { Log("Startup failed: " + ex); }
    }

    // ---- Config ----------------------------------------------------------

    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), JsonOpts);
                if (c != null) _cfg = c;
            }
        }
        catch { /* keep defaults */ }
    }

    // ---- System media (SMTC) --------------------------------------------

    private async Task InitSmtc()
    {
        _smtc = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _smtc.CurrentSessionChanged += (_, _) => Dispatcher.Invoke(HookCurrentSession);
        HookCurrentSession();
    }

    private void HookCurrentSession()
    {
        if (_smtc == null) return;
        if (_hooked != null)
        {
            _hooked.MediaPropertiesChanged -= OnMediaChanged;
            _hooked.PlaybackInfoChanged -= OnMediaChanged;
        }
        _hooked = _smtc.GetCurrentSession();
        if (_hooked != null)
        {
            _hooked.MediaPropertiesChanged += OnMediaChanged;
            _hooked.PlaybackInfoChanged += OnMediaChanged;
        }
        _ = Update();
    }

    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession sender, object args) => _ = Update();

    private async Task Update()
    {
        try
        {
            var session = _smtc?.GetCurrentSession();
            if (session == null) return;

            bool playing = session.GetPlaybackInfo().PlaybackStatus == PlaybackStatus.Playing;

            MediaProps props;
            try { props = await session.TryGetMediaPropertiesAsync(); }
            catch { return; }
            if (props == null || string.IsNullOrWhiteSpace(props.Title)) return;

            string title = props.Title;
            string artist = props.Artist ?? "";
            string album = props.AlbumTitle ?? "";

            long dur = 0;
            try
            {
                var tl = session.GetTimelineProperties();
                var d = tl.EndTime - tl.StartTime;
                if (d.TotalMilliseconds > 0) dur = (long)d.TotalMilliseconds;
            }
            catch { }

            string key = $"{session.SourceAppUserModelId}|{title}|{artist}|{album}";
            if (key == _lastKey) return;   // same track, ignore repeat events
            if (!playing) return;          // only announce a track once it is actually playing
            _lastKey = key;

            ImageSource? art = _cfg.ShowArtwork ? await LoadThumb(props) : null;
            Color accent = DefaultAccent;
            bool themed = false;
            if (_cfg.ColorAccent && art is BitmapSource bs)
            {
                accent = AccentFromArt(bs);
                themed = true;
            }
            Dispatcher.Invoke(() => ShowToast(new NowPlaying(title, artist, album, dur, art, accent, themed)));
        }
        catch (Exception ex) { Log("Update error: " + ex.Message); }
    }

    private static readonly Color DefaultAccent = Color.FromRgb(0xFF, 0xFF, 0x55); // Minecraft gold

    /// <summary>Pick a vivid accent colour from the album art (dominant vibrant hue, brightened).</summary>
    private static Color AccentFromArt(BitmapSource src)
    {
        try
        {
            var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            double s = Math.Min(1.0, 24.0 / conv.PixelWidth);
            BitmapSource small = s < 1.0 ? new TransformedBitmap(conv, new ScaleTransform(s, s)) : conv;
            int w = small.PixelWidth, h = small.PixelHeight;
            var px = new byte[w * h * 4];
            small.CopyPixels(px, w * 4, 0);

            // Bucket vibrant pixels by hue, weighted by saturation*value.
            var wr = new double[12]; var wg = new double[12]; var wb = new double[12]; var wt = new double[12];
            for (int i = 0; i < px.Length; i += 4)
            {
                double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                RgbToHsv(r, g, b, out double hue, out double sat, out double val);
                if (sat < 0.22 || val < 0.18 || val > 0.98) continue;
                int bin = (int)(hue / 30.0) % 12;
                double weight = sat * val;
                wr[bin] += r * weight; wg[bin] += g * weight; wb[bin] += b * weight; wt[bin] += weight;
            }
            int best = -1; double bestW = 0;
            for (int k = 0; k < 12; k++) if (wt[k] > bestW) { bestW = wt[k]; best = k; }
            if (best < 0) return DefaultAccent;

            RgbToHsv(wr[best] / wt[best], wg[best] / wt[best], wb[best] / wt[best], out double H, out double _, out double _);
            HsvToRgb(H, 0.85, 1.0, out double or, out double og, out double ob);  // force vivid + bright
            return Color.FromRgb((byte)(or * 255), (byte)(og * 255), (byte)(ob * 255));
        }
        catch { return DefaultAccent; }
    }

    internal static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max; s = max <= 0 ? 0 : d / max;
        h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
        }
        if (h < 0) h += 360;
    }

    internal static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60 % 2) - 1)), m = v - c;
        double r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; }
        else if (h < 120) { r1 = x; g1 = c; }
        else if (h < 180) { g1 = c; b1 = x; }
        else if (h < 240) { g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        r = r1 + m; g = g1 + m; b = b1 + m;
    }

    private static async Task<ImageSource?> LoadThumb(MediaProps props)
    {
        try
        {
            var reference = props.Thumbnail;
            if (reference == null) return null;
            using var ras = await reference.OpenReadAsync();
            using var net = ras.AsStreamForRead();
            var ms = new MemoryStream();
            await net.CopyToAsync(ms);
            if (ms.Length == 0) return null;
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 48;     // low-res -> blocky Minecraft look
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ---- Tray & autostart ------------------------------------------------

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "CiderToast"
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Show test toast", null, (_, _) => PreviewToast());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settings != null) { _settings.Activate(); return; }
        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    // ---- Accessors used by the settings window ---------------------------

    internal Config Cfg => _cfg;
    internal void SetCfg(Config c) => _cfg = c;
    internal void PreviewToast() =>
        ShowToast(new NowPlaying("Preview Song", "Some Artist", "Some Album", 214000, null, DefaultAccent, false));

    internal void SaveConfig()
    {
        try
        {
            var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_cfg, opts));
        }
        catch (Exception ex) { Log("save config error: " + ex.Message); }
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe != null)
            {
                var ico = Drawing.Icon.ExtractAssociatedIcon(exe);
                if (ico != null) return ico;
            }
        }
        catch { }
        return Drawing.SystemIcons.Application;
    }

    internal static bool IsAutoStart()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(RunName) != null; }
        catch { return false; }
    }

    internal static void SetAutoStart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k!.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else k!.DeleteValue(RunName, false);
        }
        catch (Exception ex) { Log("autostart error: " + ex.Message); }
    }

    // ---- Toast -----------------------------------------------------------

    private void ShowToast(NowPlaying np)
    {
        _current?.Kill();
        var w = new ToastWindow(np, _cfg);
        _current = w;
        w.Closed += (_, _) => { if (ReferenceEquals(_current, w)) _current = null; };
        w.ShowToast();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "ciddertoast.log"),
            $"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}"); } catch { }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
}

public record NowPlaying(string Name, string Artist, string Album, long DurationMs,
    ImageSource? Artwork, Color Accent, bool Themed);

public class Config
{
    public double ToastSeconds { get; set; } = 6.0;
    public double MarginLeft { get; set; } = 8;
    public double MarginTop { get; set; } = 8;
    public double BorderScale { get; set; } = 2.5;
    public double MinWidth { get; set; } = 384;
    public bool ShowArtwork { get; set; } = true;
    public bool ColorAccent { get; set; } = true;
    public string Corner { get; set; } = "TopLeft";  // TopLeft | TopRight | BottomLeft | BottomRight

    public Config Clone() => (Config)MemberwiseClone();
}
