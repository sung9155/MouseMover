using System.Text.Json;

namespace MouseMover;

public sealed class Settings
{
    public int JiggleSeconds { get; set; } = 45;
    public string StatusText { get; set; } = "절전방지 중";
    public bool ShowElapsed { get; set; } = true;
    public bool ShowDismissHint { get; set; } = true;
    public float LabelFontSize { get; set; } = 11f;
    public int LabelColorArgb { get; set; } = unchecked((int)0xFFA0A0A0);
    public bool RunAtStartup { get; set; }
    public int AutoOffMinutes { get; set; }
    public bool ScheduleEnabled { get; set; }
    public int WorkStartMinutes { get; set; } = 540;
    public int WorkEndMinutes { get; set; } = 1080;
    public bool[] WorkDays { get; set; } = { false, true, true, true, true, true, false };
    public bool ShowClock { get; set; }
    public string CenterMessage { get; set; } = "";
    public bool AnalogClock { get; set; }
    public int ClockFontSize { get; set; } = 64;
    public int MessageFontSize { get; set; } = 24;
    public int CenterColorArgb { get; set; } = unchecked((int)0xFFA0A0A0);
    public bool CenterBold { get; set; }
    public bool ClockSeconds { get; set; }
    public bool Clock12Hour { get; set; }
    public bool ShowDate { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MouseMover");
            return Path.Combine(dir, "settings.json");
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static Settings FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        catch (JsonException)
        {
            return new Settings();
        }
    }

    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath) ? FromJson(File.ReadAllText(FilePath)) : new Settings();
        }
        catch (IOException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, ToJson());
    }

    public Settings Clone() => (Settings)MemberwiseClone();
}
