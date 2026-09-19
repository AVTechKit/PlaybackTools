using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using PlaybackTools.ViewModels;

namespace PlaybackTools
{
    public static class ShowFileConverter
    {
        public static ShowFile ToShowFile(MainViewModel viewModel, string showName)
        {
            var file = new ShowFile
            {
                ShowName = showName,
                Settings = new ShowSettingsData
                {
                    DefaultEndAction = viewModel.DefaultEndAction.ToString(),
                    Volume = viewModel.Volume,
                    FadeTime = viewModel.FadeTime,
                    BackgroundImagePath = viewModel.BackgroundImagePath,
                    ShowBackgroundOnStop = viewModel.ShowBackgroundOnStop,
                    ShowBackgroundOnAudio = viewModel.ShowBackgroundOnAudio,
                    SelectedDisplayDeviceName = viewModel.SelectedDisplayDeviceName,
                    SelectedAudioDeviceId = viewModel.SelectedAudioDeviceId
                }
            };

            var allSlots = viewModel.Pages.SelectMany(p => p.Slots).ToList();

            foreach (var page in viewModel.Pages)
            {
                var pageData = new PageData
                {
                    PageName = page.PageName,
                    BorderColor = BrushToHex(page.BorderBrush)
                };

                foreach (var slot in page.Slots)
                {
                    pageData.Clips.Add(new ClipData
                    {
                        FilePath = slot.FilePath,
                        DisplayName = slot.DisplayName,
                        EndAction = slot.EndAction.ToString(),
                        InPoint = slot.InPoint,
                        OutPoint = slot.OutPoint,
                        ColorHex = BrushToHex(slot.ColorStripBrush),
                        PlaySelectedTargetIndex = slot.PlaySelectedTarget != null ? allSlots.IndexOf(slot.PlaySelectedTarget) : null,
                        WaveformMin = slot.WaveformPeaks?.Min,
                        WaveformMax = slot.WaveformPeaks?.Max
                    });
                }

                file.Pages.Add(pageData);
            }

            return file;
        }

        public static List<string> ApplyToViewModel(ShowFile file, MainViewModel viewModel)
        {
            viewModel.DefaultEndAction = ParseEndActionOrDefault(file.Settings.DefaultEndAction);
            if (viewModel.DefaultEndAction == EndAction.PlaySelected)
                viewModel.DefaultEndAction = EndAction.PauseOnLastFrame;
            viewModel.Volume = file.Settings.Volume;
            viewModel.FadeTime = file.Settings.FadeTime;
            viewModel.BackgroundImagePath = file.Settings.BackgroundImagePath;
            viewModel.ShowBackgroundOnStop = file.Settings.ShowBackgroundOnStop;
            viewModel.ShowBackgroundOnAudio = file.Settings.ShowBackgroundOnAudio;
            viewModel.SelectedDisplayDeviceName = file.Settings.SelectedDisplayDeviceName;
            viewModel.SelectedAudioDeviceId = file.Settings.SelectedAudioDeviceId;

            var allSlots = viewModel.Pages.SelectMany(p => p.Slots).ToList();
            var pendingTargets = new List<(ClipSlotViewModel Slot, int TargetIndex)>();
            var missingFiles = new List<string>();

            int pageCount = Math.Min(viewModel.Pages.Count, file.Pages.Count);
            for (int pi = 0; pi < pageCount; pi++)
            {
                var page = viewModel.Pages[pi];
                var pageData = file.Pages[pi];

                page.PageName = pageData.PageName;
                page.BorderBrush = HexToBrush(pageData.BorderColor);

                int slotCount = Math.Min(page.Slots.Count, pageData.Clips.Count);
                for (int si = 0; si < slotCount; si++)
                {
                    var slot = page.Slots[si];
                    var clipData = pageData.Clips[si];

                    slot.FilePath = clipData.FilePath;
                    slot.DisplayName = clipData.DisplayName;
                    slot.MediaType = clipData.FilePath != null ? MainWindow.GuessMediaType(clipData.FilePath) : MediaType.None;
                    slot.EndAction = ParseEndActionOrDefault(clipData.EndAction);
                    slot.InPoint = clipData.InPoint;
                    slot.OutPoint = clipData.OutPoint;
                    slot.ColorStripBrush = HexToBrush(clipData.ColorHex);
                    slot.PlaySelectedTarget = null; // resolved in the second pass below
                    slot.WaveformPeaks = (clipData.WaveformMin != null && clipData.WaveformMax != null)
                        ? (clipData.WaveformMin, clipData.WaveformMax)
                        : null;

                    if (clipData.PlaySelectedTargetIndex.HasValue)
                        pendingTargets.Add((slot, clipData.PlaySelectedTargetIndex.Value));

                    if (clipData.FilePath != null && !File.Exists(clipData.FilePath))
                    {
                        missingFiles.Add(clipData.FilePath);
                        slot.RemoveCommand.Execute(null); // file's gone - leave the slot genuinely empty
                    }
                }
            }

            foreach (var (slot, targetIndex) in pendingTargets)
            {
                if (targetIndex >= 0 && targetIndex < allSlots.Count)
                    slot.PlaySelectedTarget = allSlots[targetIndex];
            }

            return missingFiles;
        }

        private static EndAction ParseEndActionOrDefault(string value)
            => Enum.TryParse(value, out EndAction result) ? result : EndAction.PauseOnLastFrame;

        private static string BrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            return "#00FFFFFF";
        }

        private static Brush HexToBrush(string hex)
        {
            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}