using System.Collections.Generic;

namespace PlaybackTools
{
    /// <summary>The full serializable contents of a .playback show file. Plain data only -
    /// no WPF types, no object references (PlaySelectedTarget becomes a flat index here,
    /// resolved back to a real reference only after every slot exists on load).</summary>
    public class ShowFile
    {
        public int SchemaVersion { get; set; } = 1;
        public string ShowName { get; set; } = "";

        public ShowSettingsData Settings { get; set; } = new();
        public List<PageData> Pages { get; set; } = new();
    }

    public class ShowSettingsData
    {
        public string DefaultEndAction { get; set; } = "PauseOnLastFrame";
        public double Volume { get; set; } = 100;
        public double FadeTime { get; set; } = 1.0;
        public string? BackgroundImagePath { get; set; }
        public bool ShowBackgroundOnStop { get; set; }
        public bool ShowBackgroundOnAudio { get; set; }
        public string? SelectedDisplayDeviceName { get; set; }
        public string? SelectedAudioDeviceId { get; set; }
    }

    public class PageData
    {
        public string PageName { get; set; } = "";
        public string BorderColor { get; set; } = "#FF333333";
        public List<ClipData> Clips { get; set; } = new();
    }

    public class ClipData
    {
        public string? FilePath { get; set; }
        public string DisplayName { get; set; } = "Click to Load";
        public string EndAction { get; set; } = "PauseOnLastFrame";
        public string InPoint { get; set; } = "00:00:00";
        public string OutPoint { get; set; } = "00:00:00";
        public string ColorHex { get; set; } = "#00FFFFFF"; // transparent - matches an empty slot's default
        public int? PlaySelectedTargetIndex { get; set; }
        public float[]? WaveformMin { get; set; }
        public float[]? WaveformMax { get; set; }
    }
}