using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Size = System.Windows.Size;

namespace CiderToast;

public partial class ToastWindow : Window
{
    private readonly Config _cfg;
    private readonly NowPlaying _np;

    private readonly double PX;           // border pixel scale (from config.borderScale)
    private const int Inset = 4;          // nine-slice source inset
    private const double IconSize = 46;

    internal static readonly FontFamily McFont = LoadFont();

    private static FontFamily LoadFont()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var baseUri = new Uri("pack://application:,,,/");
        Func<FontFamily>[] forms =
        {
            () => new FontFamily(baseUri, "./assets/#Monocraft"),
            () => new FontFamily(baseUri, "./#Monocraft"),
            () => new FontFamily($"pack://application:,,,/{asm};component/assets/#Monocraft"),
            () => new FontFamily("pack://application:,,,/assets/#Monocraft"),
        };
        foreach (var make in forms)
        {
            try
            {
                var ff = make();
                if (ff.FamilyNames.Values.Any(v => string.Equals(v, "Monocraft", StringComparison.OrdinalIgnoreCase)))
                    return ff;
            }
            catch { }
        }
        return new FontFamily("Consolas");
    }

    // Default Minecraft note-particle colour spectrum (used when not album-themed).
    private static readonly Color[] DefaultNoteColors =
    {
        Rgb(0xFF,0x55,0x55), Rgb(0xFF,0xAA,0x00), Rgb(0xFF,0xFF,0x55), Rgb(0x55,0xFF,0x55),
        Rgb(0x55,0xFF,0xFF), Rgb(0x55,0x55,0xFF), Rgb(0xFF,0x55,0xFF), Rgb(0xAA,0x00,0xAA),
    };
    private Color[] _noteColors = DefaultNoteColors;

    private static readonly Random Rng = new();
    private Canvas _particles = null!;
    private Point _noteOrigin;
    private BitmapSource? _noteSheet;
    private int _noteFrames = 1;

    private Grid _content = null!;
    private double _offLeft;
    private DispatcherTimer? _dismiss;
    private DispatcherTimer? _spawn;
    private DispatcherTimer? _anim;
    private readonly List<Particle> _live = new();
    private bool _closing;

    public ToastWindow(NowPlaying np, Config cfg)
    {
        InitializeComponent();
        _np = np;
        _cfg = cfg;
        PX = _cfg.BorderScale > 0 ? _cfg.BorderScale : 2.5;
        if (np.Themed) _noteColors = BuildAccentPalette(np.Accent);
        BuildUi();
    }

    // ---- UI construction -------------------------------------------------

    private void BuildUi()
    {
        RootGrid.Children.Add(BuildNineSlice());

        var content = _content = new Grid { Margin = new Thickness(12, 9, 14, 9) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = BuildIcon();
        Grid.SetColumn(icon, 0);
        content.Children.Add(icon);

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(Line("Now Playing", _np.Accent, 12));
        lines.Children.Add(Line(Trim(_np.Name, 34), Rgb(0xFF, 0xFF, 0xFF), 18, top: 2));
        if (!string.IsNullOrWhiteSpace(_np.Artist))
            lines.Children.Add(Line(Trim(_np.Artist, 40), Rgb(0xAA, 0xAA, 0xAA), 13, top: 2));
        if (!string.IsNullOrWhiteSpace(_np.Album))
            lines.Children.Add(Line(Trim(_np.Album, 42), Rgb(0xAA, 0xAA, 0xAA), 12, top: 1));
        if (_np.DurationMs > 0)
            lines.Children.Add(Line(FormatDuration(_np.DurationMs), Rgb(0x55, 0xFF, 0xFF), 12, top: 3));
        Grid.SetColumn(lines, 2);
        content.Children.Add(lines);

        RootGrid.Children.Add(content);

        _particles = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
        RootGrid.Children.Add(_particles);
    }

    private Grid BuildNineSlice()
    {
        var g = new Grid();
        BitmapSource? src = TryLoad("now_playing.png");
        if (src == null)
        {
            // Fallback: draw the panel with brushes if the asset is missing.
            g.Children.Add(new Border
            {
                Background = new SolidColorBrush(Rgb(0x21, 0x21, 0x21)),
                BorderBrush = new SolidColorBrush(Rgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2)
            });
            return g;
        }

        int w = src.PixelWidth, h = src.PixelHeight, i = Inset;
        double edge = i * PX;
        foreach (var col in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) })
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = col });
        foreach (var row in new[] { new GridLength(edge), new GridLength(1, GridUnitType.Star), new GridLength(edge) })
            g.RowDefinitions.Add(new RowDefinition { Height = row });

        (int x, int y, int cw, int ch)[] rects =
        {
            (0,0,i,i),        (i,0,w-2*i,i),        (w-i,0,i,i),
            (0,i,i,h-2*i),    (i,i,w-2*i,h-2*i),    (w-i,i,i,h-2*i),
            (0,h-i,i,i),      (i,h-i,w-2*i,i),      (w-i,h-i,i,i),
        };
        for (int idx = 0; idx < 9; idx++)
        {
            var r = rects[idx];
            var piece = new Image
            {
                Source = new CroppedBitmap(src, new Int32Rect(r.x, r.y, r.cw, r.ch)),
                Stretch = Stretch.Fill
            };
            RenderOptions.SetBitmapScalingMode(piece, BitmapScalingMode.NearestNeighbor);
            Grid.SetColumn(piece, idx % 3);
            Grid.SetRow(piece, idx / 3);
            g.Children.Add(piece);
        }
        return g;
    }

    private FrameworkElement BuildIcon()
    {
        var box = new Grid
        {
            Width = IconSize,
            Height = IconSize,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Album art (pixelated) if we have it, else a tinted note glyph.
        if (_cfg.ShowArtwork && _np.Artwork != null)
        {
            var art = new Image { Stretch = Stretch.UniformToFill, Source = _np.Artwork };
            RenderOptions.SetBitmapScalingMode(art, BitmapScalingMode.NearestNeighbor);
            box.Children.Add(new Border
            {
                Width = IconSize,
                Height = IconSize,
                Background = new SolidColorBrush(Rgb(0x10, 0x10, 0x10)),
                ClipToBounds = true,
                Child = art
            });
        }
        else
        {
            box.Children.Add(NoteGlyph(IconSize, Rgb(0xFF, 0xFF, 0xFF)));
        }
        return box;
    }

    /// <summary>A single note sprite frame, tinted to a flat colour via opacity mask.</summary>
    private UIElement NoteGlyph(double size, Color color)
    {
        _noteSheet ??= TryLoad("music_notes.png");
        if (_noteSheet != null)
        {
            int fw = _noteSheet.PixelWidth;
            _noteFrames = Math.Max(1, _noteSheet.PixelHeight / fw);
            int frame = Rng.Next(_noteFrames);
            var cropped = new CroppedBitmap(_noteSheet, new Int32Rect(0, frame * fw, fw, fw));
            var rect = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                OpacityMask = new ImageBrush(cropped) { Stretch = Stretch.Uniform }
            };
            RenderOptions.SetBitmapScalingMode(rect, BitmapScalingMode.NearestNeighbor);
            return rect;
        }
        // Fallback glyph.
        return new TextBlock
        {
            Text = "♪",
            FontSize = size * 0.8,
            Foreground = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>A Minecraft text line: main glyphs over a colour-&gt;&gt;2 hard shadow.</summary>
    private FrameworkElement Line(string text, Color color, double size, double top = 0)
    {
        var host = new Grid { Margin = new Thickness(0, top, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        TextBlock Make(Color c, double dx, double dy) => new()
        {
            Text = text,
            FontFamily = McFont,
            FontSize = size,
            Foreground = new SolidColorBrush(c),
            RenderTransform = new TranslateTransform(dx, dy)
        };
        host.Children.Add(Make(Shadow(color), 2, 2));  // drop shadow
        host.Children.Add(Make(color, 0, 0));          // main text
        return host;
    }

    // ---- Show / animate --------------------------------------------------

    public void ShowToast()
    {
        // Size the window from the content's desired size. (A star-based nine-slice
        // Grid inside a SizeToContent window would otherwise expand to fill the screen.)
        _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // Clamp to a minimum width so short song names don't shrink the toast;
        // every toast then shares the api-key toast's footprint.
        Width = Math.Max(_cfg.MinWidth, Math.Ceiling(_content.DesiredSize.Width));
        Height = Math.Ceiling(_content.DesiredSize.Height);

        // Resting spot + off-screen start depend on the chosen corner.
        var wa = SystemParameters.WorkArea;
        bool right = _cfg.Corner.Contains("Right", StringComparison.OrdinalIgnoreCase);
        bool bottom = _cfg.Corner.Contains("Bottom", StringComparison.OrdinalIgnoreCase);
        double restLeft = right ? wa.Right - Width - _cfg.MarginLeft : wa.Left + _cfg.MarginLeft;
        Top = bottom ? wa.Bottom - Height - _cfg.MarginTop : wa.Top + _cfg.MarginTop;
        _offLeft = right ? wa.Right + 8 : wa.Left - Width - 8;

        Show();
        Left = _offLeft;

        _noteOrigin = new Point(12 + IconSize / 2, 9 + IconSize / 2);

        PlaySound("In.wav");
        StartNotes();

        Tween(_offLeft, restLeft, 0.34, () =>
        {
            _dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1.0, _cfg.ToastSeconds)) };
            _dismiss.Tick += (_, _) => Dismiss();
            _dismiss.Start();
        });
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _dismiss?.Stop();
        _spawn?.Stop();
        PlaySound("Out.wav");
        Tween(Left, _offLeft, 0.30, () => { try { Close(); } catch { } });
    }

    /// <summary>Close immediately, used when a newer song replaces this toast.</summary>
    public void Kill()
    {
        if (_closing) return;
        _closing = true;
        _dismiss?.Stop();
        _spawn?.Stop();
        _anim?.Stop();
        try { Close(); } catch { }
    }

    /// <summary>Cubic-ease tween of Window.Left (WPF can't animate Left cleanly).</summary>
    private void Tween(double from, double to, double seconds, Action? done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        t.Tick += (_, _) =>
        {
            double p = Math.Min(1.0, sw.Elapsed.TotalSeconds / seconds);
            double e = 1 - Math.Pow(1 - p, 3);      // ease-out cubic
            Left = from + (to - from) * e;
            if (p >= 1.0) { t.Stop(); done?.Invoke(); }
        };
        t.Start();
    }

    // ---- Note particles --------------------------------------------------

    private void StartNotes()
    {
        _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _anim.Tick += (_, _) => StepNotes();
        _anim.Start();

        SpawnNote();
        _spawn = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _spawn.Tick += (_, _) => SpawnNote();
        _spawn.Start();
    }

    private void SpawnNote()
    {
        if (_closing) return;
        var color = _noteColors[Rng.Next(_noteColors.Length)];
        double sz = 14 + Rng.NextDouble() * 8;
        var el = NoteGlyph(sz, color);
        var p = new Particle
        {
            El = el,
            BaseX = _noteOrigin.X - sz / 2 + (Rng.NextDouble() * 10 - 5),
            Y = _noteOrigin.Y - sz / 2,
            Life = 1.1 + Rng.NextDouble() * 0.6,
            RiseSpeed = 26 + Rng.NextDouble() * 16,
            Amp = 6 + Rng.NextDouble() * 8,
            Freq = 3 + Rng.NextDouble() * 2,
            Phase = Rng.NextDouble() * Math.PI * 2,
            Size = sz
        };
        Canvas.SetLeft(el, p.BaseX);
        Canvas.SetTop(el, p.Y);
        _particles.Children.Add(el);
        _live.Add(p);
    }

    private void StepNotes()
    {
        double dt = 0.016;
        for (int k = _live.Count - 1; k >= 0; k--)
        {
            var p = _live[k];
            p.Age += dt;
            double t = p.Age / p.Life;
            if (t >= 1.0)
            {
                _particles.Children.Remove(p.El);
                _live.RemoveAt(k);
                continue;
            }
            p.Y -= p.RiseSpeed * dt;
            double x = p.BaseX + Math.Sin(p.Age * p.Freq + p.Phase) * p.Amp;
            Canvas.SetLeft(p.El, x);
            Canvas.SetTop(p.El, p.Y);
            p.El.Opacity = 1.0 - t * t;   // ease-out fade
        }
        if (_closing && _live.Count == 0) _anim?.Stop();
    }

    private sealed class Particle
    {
        public UIElement El = null!;
        public double BaseX, Y, Life, Age, RiseSpeed, Amp, Freq, Phase, Size;
    }

    // ---- Assets / helpers ------------------------------------------------

    private static BitmapSource? TryLoad(string file)
    {
        try
        {
            // Load from the embedded resource stream. (BitmapImage.UriSource with a
            // pack:// URI is unreliable in single-file self-contained publishes.)
            var info = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/{file}"));
            if (info == null) return null;
            using var s = info.Stream;
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static readonly List<System.Media.SoundPlayer> Players = new();
    private static void PlaySound(string file)
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/{file}"));
            if (info == null) return;
            var sp = new System.Media.SoundPlayer(info.Stream);
            Players.Add(sp);
            if (Players.Count > 8) Players.RemoveAt(0);
            sp.Play();
        }
        catch { }
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max - 3).TrimEnd() + "...";

    /// <summary>A small family of note colours around the album's accent hue.</summary>
    private static Color[] BuildAccentPalette(Color accent)
    {
        App.RgbToHsv(accent.R / 255.0, accent.G / 255.0, accent.B / 255.0, out double h, out double _, out double _);
        var list = new List<Color>();
        foreach (double dh in new[] { -34.0, -17, 0, 17, 34 })
        {
            App.HsvToRgb((h + dh + 360) % 360, 0.8, 1.0, out double r, out double g, out double b);
            list.Add(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
        }
        return list.ToArray();
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static Color Shadow(Color c) => Color.FromRgb((byte)(c.R / 4), (byte)(c.G / 4), (byte)(c.B / 4));

    private static string FormatDuration(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }
}
