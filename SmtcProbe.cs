using System.IO;
using System.Text;
using Windows.Media.Control;

namespace CiderToast;

/// <summary>Diagnostic: dump every system media session so we can see what data each app reports.</summary>
internal static class SmtcProbe
{
    public static async Task Run()
    {
        var sb = new StringBuilder();
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var cur = mgr.GetCurrentSession();
            sb.AppendLine($"current session app: {cur?.SourceAppUserModelId ?? "(none)"}");
            foreach (var s in mgr.GetSessions())
            {
                sb.AppendLine("----");
                sb.AppendLine($"app: {s.SourceAppUserModelId}");
                sb.AppendLine($"status: {s.GetPlaybackInfo().PlaybackStatus}");
                try
                {
                    var p = await s.TryGetMediaPropertiesAsync();
                    sb.AppendLine($"title: {p.Title}");
                    sb.AppendLine($"artist: {p.Artist}");
                    sb.AppendLine($"album: {p.AlbumTitle}");
                    sb.AppendLine($"albumArtist: {p.AlbumArtist}");
                    sb.AppendLine($"thumb: {(p.Thumbnail != null ? "yes" : "no")}");
                }
                catch (Exception ex) { sb.AppendLine("props err: " + ex.Message); }
                try
                {
                    var t = s.GetTimelineProperties();
                    sb.AppendLine($"timeline start={t.StartTime} end={t.EndTime} pos={t.Position} dur={t.EndTime - t.StartTime}");
                }
                catch (Exception ex) { sb.AppendLine("timeline err: " + ex.Message); }
            }
        }
        catch (Exception ex) { sb.AppendLine("SMTC err: " + ex); }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smtc.log"), sb.ToString());
    }
}
