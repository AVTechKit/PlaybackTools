using OpenTK.Compute.OpenCL;
using PlaybackTools.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace PlaybackTools
{
    public partial class LicensesWindow : Window
    {
        private static readonly Brush HeaderBrush = MakeFrozenBrush(0x3A, 0x9E, 0xDB);

        public LicensesWindow()
        {
            InitializeComponent();
            BuildContent();
        }

        private void BuildContent()
        {

            AddSection("Copyright & Warranty",
                "Playback Tools\n" +
                "Copyright (C) 2026 [your name]\n\n" +
                "This program is free software: you can redistribute it and/or modify " +
                "it under the terms of the GNU General Public License as published by " +
                "the Free Software Foundation, either version 3 of the License, or " +
                "(at your option) any later version.\n\n" +
                "This program is distributed in the hope that it will be useful, but " +
                "WITHOUT ANY WARRANTY; without even the implied warranty of " +
                "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU " +
                "General Public License for more details.\n\n" +
                "You should have received a copy of the GNU General Public License " +
                "along with this program. If not, see https://www.gnu.org/licenses/.");
            AddSection("License",
                "Playback Tools uses libmpv, compiled under the GNU General Public " +
                "License version 3 via shinchiro's mpv-winbuild-cmake toolchain. " +
                "Because of this, Playback Tools itself is also licensed under GPLv3 - " +
                "you have the same rights to it that you have to any GPL software, " +
                "including the right to the full source code.");

            AddSection("Source Code",
                "mpv build toolchain: https://github.com/shinchiro/mpv-winbuild-cmake\n" +
                "Playback Tools: https://github.com/AVTechKit/PlaybackTools\n\n" +
                "Built using mpv build 20260610-git-304426c and FFmpeg 9.0.1-full_build-gyan.dev.");

            AddSection("Other Components",
                "FFmpeg is invoked as a separate process for waveform generation, not " +
                "linked, and is not modified. It is licensed under GPLv3.\n\n" +
                "NAudio and OpenTK.GLWpfControl are used under the MIT License.\n\n" +
                "Full license text for all components is included as LICENSE.txt " +
                "alongside this application.");
        }

        private void AddSection(string header, string body)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = header,
                Foreground = HeaderBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 14, 0, 4)
            });
            ContentPanel.Children.Add(new TextBlock
            {
                Text = body,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
        }

  


        

        private static Rectangle MakeBar(double left, double top)
        {
            var bar = new Rectangle { Width = 2, Height = 9, Fill = Brushes.White };
            Canvas.SetLeft(bar, left);
            Canvas.SetTop(bar, top);
            return bar;
        }

        private static Brush MakeFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}