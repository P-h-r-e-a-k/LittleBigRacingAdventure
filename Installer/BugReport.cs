using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace LBRAInstaller;

// Bug reports (2026-11-01, the user: "Let's add automatic bug reporting ... submit to github and pick them up from there complete with the log file"): an issue on the game's public
// GitHub repo, labelled auto-report, with the game's log in it (GitHub takes no attachments from the API, so the log goes in the issue and, past a body's size, in its comments).
//
//     LBRA-Setup.exe --send-report <install folder>     Update\report_note.txt (the game's): its first line the kind, crash or manual, the rest the player's words
//
// Update\report_note.txt's kind may also be feature: a feature request, the player's words alone, no log. A failed update sends its own (AfterFailedUpdate). What is sent, the player says first: settings' reports=ask (asked once, then auto or off), auto or off. The issue is public, so
// the log is scrubbed first (Scrub): names, folders, addresses. An automatic report of a kind once a week from a PC, for each version (the user, 2026-11-02: "a bug reported in a newer version than the one it was originally reported in is probably a
// failed fix"; no limit a day, and none on what a player sends himself); a report like an open one is a comment on it, its version in it.
// The key, in the setup only (build.ps1, -p:ReportKey, from a file outside the repos, masked so that the file carries no key in the clear): issues on this repo and nothing else.
// For tests LBRA_REPORT_URL points the reports at http://localhost/... (nothing else is taken).
internal static class BugReport
{
    private const string Api = "https://api.github.com/repos/P-h-r-e-a-k/LittleBigRacingAdventure";
    private const string Label = "auto-report", FeatureLabel = "feature-request", UrlVariable = "LBRA_REPORT_URL";
    public const string NoteFile = "report_note.txt";
    private const string SentFile = "reports_sent.txt", Settings = "RACEGAME_SETTINGS.TXT";
    // (GitHub's limit is 65,536 characters a body)
    private const int ChunkMax = 60000, CommentsMax = 4, ResendDays = 7;
    private static readonly byte[] Mask = Encoding.ASCII.GetBytes("LBRA-reports");

    private static string? Key()
    {
        var hex = typeof(BugReport).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "ReportKey")?.Value;
        if (string.IsNullOrEmpty(hex)) return null;
        var bytes = Convert.FromHexString(hex);
        for (var i = 0; i < bytes.Length; i++) bytes[i] ^= Mask[i % Mask.Length];
        return Encoding.ASCII.GetString(bytes);
    }

    private static string? TestUrl()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        return url is not null && url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ? url.TrimEnd('/') : null;
    }

    private static string UserDir(string installDir) => Path.Combine(installDir, "User", "LBA2");

    // The player's say: ask, auto or off
    public static string Consent(string installDir)
    {
        try
        {
            foreach (var line in File.ReadAllLines(Path.Combine(UserDir(installDir), Settings)))
                if (line.StartsWith("reports=", StringComparison.Ordinal)) return line[8..].Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return "ask";
    }

    private static void SetConsent(string installDir, string value)
    {
        var path = Path.Combine(UserDir(installDir), Settings);
        try
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith("reports=", StringComparison.Ordinal)).ToList() : new List<string>();
            lines.Add("reports=" + value);
            File.WriteAllLines(path, lines);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Updater.Log(installDir, "the report setting not saved: " + e.Message); }
    }

    // ---- what is sent ---------------------------------------------------------------------------------------------------------------------------------------

    private static string Read(string path)
    {
        try
        {
            using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var r = new StreamReader(f);
            return r.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    private static readonly string[] NameLines =
    {
        @"\[racenet\] hosting as (.+?) on UDP", @"\[racenet\] joining \S+ as (.+?)\s*$", @"\[racenet\] (.+?) (?:joined|left|gave the password)",
        @"online: (.+?) uses ", @"knocked by (.+?)'s car", @"online: (.+?)'s (?:car|ball)",
    };

    // The log with nothing in it that says who or where the player is: this goes on a public page
    public static string Scrub(string text, string installDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var online = Read(Path.Combine(UserDir(installDir), "ONLINE.TXT"));
        foreach (Match m in Regex.Matches(online, @"^name=(.+?)\s*$", RegexOptions.Multiline)) names.Add(m.Groups[1].Value);
        foreach (var pattern in NameLines)
            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Multiline)) names.Add(m.Groups[1].Value);
        names.RemoveWhere(n => n.Length < 2 || n is "host" or "Player 1" or "Player 2" or "Twinsen");
        names.Add(Environment.UserName);
        names.Add(Environment.MachineName);
        text = text.Replace(installDir, "<game>", StringComparison.OrdinalIgnoreCase).Replace(installDir.Replace('\\', '/'), "<game>", StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, @"(?<![A-Za-z])[A-Za-z]:[\\/](?:[^\\/\r\n:*?""<>|]+[\\/])*", "<folder>/");
        text = Regex.Replace(text, @"\\\\[^\\/\r\n]+[\\/]", "<folder>/");
        foreach (var name in names.Where(n => n.Length >= 2).OrderByDescending(n => n.Length))
            text = Regex.Replace(text, @"(?<![A-Za-z0-9])" + Regex.Escape(name) + @"(?![A-Za-z0-9])", "<name>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", "<address>");
        text = Regex.Replace(text, @"(?<![0-9A-Fa-f:])[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{0,4}){4,7}(?![0-9A-Fa-f:])|(?<![0-9A-Fa-f:])[0-9A-Fa-f]{1,4}::[0-9A-Fa-f:]*", "<address>");
        text = Regex.Replace(text, @"joining \S+ as", "joining <address> as");
        return text;
    }

    // The line that says what went wrong
    private static string Signature(string log, string fallback)
    {
        var lines = log.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var last = lines.LastOrDefault(l => l.Contains("*** ERROR", StringComparison.Ordinal) || l.Contains("[CRASH]", StringComparison.OrdinalIgnoreCase) || l.Contains("Exception", StringComparison.Ordinal));
        var said = last ?? fallback;
        said = Regex.Replace(said, @"^\*+\s*ERROR->\s*", "").Replace("(SEE README.TXT)", "").Trim();
        return said.Length > 110 ? said[..110] : said;
    }

    private static string Fence(string name, string text) =>
        $"<details><summary>{name}</summary>\n\n```text\n{text.Replace("```", "'''")}\n```\n</details>\n";

    // Each log in pieces that fit a body or a comment, as many as are sent; a log with more than that loses its middle
    private static List<string> Pieces(string name, string text)
    {
        var room = ChunkMax * (CommentsMax + 1);
        if (text.Length > room) text = text[..(room / 3)] + "\n\n[... the middle of the log left out ...]\n\n" + text[^(room * 2 / 3 - 60)..];
        var pieces = new List<string>();
        for (var at = 0; at < text.Length; at += ChunkMax) pieces.Add(text.Substring(at, Math.Min(ChunkMax, text.Length - at)));
        return pieces.Select((p, i) => Fence(pieces.Count > 1 ? $"{name}, part {i + 1} of {pieces.Count}" : name, p)).ToList();
    }

    // ---- sending --------------------------------------------------------------------------------------------------------------------------------------------

    private static HttpClient Client(string? key)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"LBRA-Reports/{Updater.OwnVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (key is not null) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return http;
    }

    private static async Task<JsonElement?> Post(HttpClient http, string url, object body, Action<string> L)
    {
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var reply = await http.PostAsync(url, content);
        var text = await reply.Content.ReadAsStringAsync();
        if (!reply.IsSuccessStatusCode) { L($"the report refused: {(int)reply.StatusCode} {reply.ReasonPhrase}"); return null; }
        using var doc = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        return doc.RootElement.Clone();
    }

    // No more than one of a kind a week, three a day. Sent: said so. Manual ones count towards the day only
    private static bool TooSoon(string installDir, string hash, bool manual, Action<string> L)
    {
        var path = Path.Combine(installDir, Updater.Folder, SentFile);
        var today = DateTime.UtcNow.Date;
        var lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        var recent = lines.Select(l => l.Split(' ')).Where(p => p.Length == 2 && DateTime.TryParse(p[0], out _)).Select(p => (Day: DateTime.Parse(p[0]), Hash: p[1])).ToList();
        if (!manual && recent.Any(r => r.Hash == hash && (today - r.Day).TotalDays < ResendDays)) { L("that report was sent this week: not again"); return true; }
        return false;
    }

    private static void NoteSent(string installDir, string hash) =>
        File.AppendAllText(Path.Combine(installDir, Updater.Folder, SentFile), $"{DateTime.UtcNow:yyyy-MM-dd} {hash}\n");

    private static async Task<int> Deliver(string installDir, string kind, string signature, string? note, List<(string Name, string Text)> logs)
    {
        void L(string s) => Updater.Log(installDir, "report: " + s);
        var key = Key();
        var test = TestUrl();
        if (key is null && test is null) { L("this setup has no key: nothing sent"); return 0; }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + signature)))[..12];
        try
        {
            var asked = kind is "manual" or "feature";
            var version = Updater.OwnVersion;
            var sent = $"{hash}-{version}";
            if (TooSoon(installDir, sent, asked, L)) return 0;
            var head = $"[{kind}] {signature}";
            var title = $"{head} ({version})";
            var about = $"**Version** {version}  \n**Kind** {kind}  \n**Windows** {Environment.OSVersion.Version}  \n**Report** {hash}\n\n";
            if (!string.IsNullOrWhiteSpace(note)) about += $"> {Scrub(note, installDir).Replace("\n", " ")}\n\n";
            var pieces = logs.Where(l => l.Text.Length > 0).SelectMany(l => Pieces(l.Name, l.Text)).ToList();
            var api = test ?? Api;
            using var http = Client(key);
            // (one like it open: a comment on it)
            var number = 0L;
            var open = asked ? null : await http.GetStringAsync($"{api}/issues?labels={Label}&state=open&per_page=100");
            if (open is not null)
                foreach (var issue in JsonDocument.Parse(open).RootElement.EnumerateArray())
                    if (issue.GetProperty("title").GetString()?.StartsWith(head + " (", StringComparison.Ordinal) == true) { number = issue.GetProperty("number").GetInt64(); break; }
            if (number > 0)
            {
                L($"like issue {number}: added to it");
                if (await Post(http, $"{api}/issues/{number}/comments", new { body = about + (pieces.Count > 0 ? pieces[^1] : "") }, L) is null) return 1;
            }
            else
            {
                var body = about + (pieces.Count > 0 ? pieces[0] : "");
                if (await Post(http, $"{api}/issues", new { title, body, labels = new[] { kind == "feature" ? FeatureLabel : Label } }, L) is not { } made) return 1;
                number = made.TryGetProperty("number", out var n) ? n.GetInt64() : 0;
                foreach (var piece in pieces.Skip(1).Take(CommentsMax))
                    if (await Post(http, $"{api}/issues/{number}/comments", new { body = piece }, L) is null) return 1;
                L($"sent as issue {number}");
            }
            NoteSent(installDir, sent);
            return 0;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            L("not sent: " + e.Message);
            return 1;
        }
    }

    // From the game: after a crash, the last run's log; asked for, this run's
    public static async Task<int> Send(string installDir)
    {
        installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        var notePath = Path.Combine(installDir, Updater.Folder, NoteFile);
        var lines = File.Exists(notePath) ? File.ReadAllLines(notePath) : Array.Empty<string>();
        try { File.Delete(notePath); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var kind = lines.Length > 0 && lines[0].Trim() is "manual" or "feature" ? lines[0].Trim() : "crash";
        var note = lines.Length > 1 ? string.Join(' ', lines.Skip(1)).Trim() : null;
        if (kind == "feature")
        {
            var wanted = note is { Length: > 0 } ? Scrub(note, installDir) : "a feature request";
            return await Deliver(installDir, kind, wanted.Length > 110 ? wanted[..110] : wanted, note, new());
        }
        var user = UserDir(installDir);
        var log = Scrub(Read(Path.Combine(user, kind == "crash" ? "adeline.prev.log" : "adeline.log")), installDir);
        var signature = kind == "manual" ? (note is { Length: > 0 } ? Scrub(note, installDir) : "reported by a player") : Signature(log, "the game stopped unexpectedly, no error logged");
        if (signature.Length > 110) signature = signature[..110];
        var logs = new List<(string Name, string Text)> { ("adeline.log", log) };
        // (asked for: what went wrong may have been in the run before)
        if (kind == "manual") logs.Add(("adeline.prev.log", Scrub(Read(Path.Combine(user, "adeline.prev.log")), installDir)));
        return await Deliver(installDir, kind, signature, note, logs);
    }

    // From a failed update: the player asked once, then as they said
    public static async Task AfterFailedUpdate(string installDir, Exception failure)
    {
        var consent = Consent(installDir);
        if (consent == "off") return;
        if (consent != "auto")
        {
            var yes = MessageBox.Show("Send a report of this to the game's public GitHub page, now and whenever something goes wrong from now on?\n\n" +
                                      "It includes the game's log, with names, folders and addresses removed. You can turn this off in Options.",
                                      GameInstall.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            SetConsent(installDir, yes ? "auto" : "off");
            if (!yes) return;
        }
        var dir = Path.Combine(installDir, Updater.Folder);
        var message = Scrub(failure.Message, installDir);
        await Deliver(installDir, "update", message, null, new()
        {
            ("the failure", Scrub(failure.ToString(), installDir)),
            ("update.log", Scrub(Read(Path.Combine(dir, Updater.LogFile)), installDir)),
        });
    }
}
