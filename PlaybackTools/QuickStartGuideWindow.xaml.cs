using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Color = System.Windows.Media.Color;
using PlaybackTools.ViewModels;

namespace PlaybackTools
{
    public partial class QuickStartGuideWindow : Window
    {
        private static readonly Brush HeaderBrush = MakeFrozenBrush(0x3A, 0x9E, 0xDB);

        public QuickStartGuideWindow()
        {
            InitializeComponent();
            BuildContent();
        }

        private void BuildContent()
        {
            AddSection("Loading Clips",
                "Click an empty button to open a file picker - selecting multiple files fills " +
                "the next empty buttons automatically. You can also drag and drop files straight " +
                "onto any button; dropping onto a loaded button replaces it.\n\n" +
                "Right-click a loaded clip for Rename, Replace, Set Button Color, Set End Action, " +
                "Move/Swap Clip, and Remove Clip.");

            AddEndActionsSection();

            AddSection("Pages",
                "The show is split into 10 pages of 30 buttons each. Right-click a page tab to " +
                "rename it, restore its default name, or set/reset a page color.");

            AddSection("Moving & Swapping Clips",
                "Right-click a loaded clip \u2192 Move/Swap Clip. Your cursor becomes a hand - " +
                "click any other button (on any page) to move or swap with it. Press Esc, or " +
                "click the original clip again, to cancel.");

            AddSection("Transport & Global Controls",
                "Play, Pause, and Stop control the active clip. Volume and Fade Time are global " +
                "- Fade Time sets crossfade/fade-to-black duration, and also determines when " +
                "Loop (Fade), Play Next, Play Selected, and Stop to Black actually trigger.\n\n" +
                "Skip to Start, Skip to End (previews just before a crossfade would trigger), " +
                "\u00b120s, and Last 15/30 seconds are also available.");

            AddSection("Mark In / Mark Out",
                "Mark In and Mark Out capture the active clip's current position, trimming " +
                "where it starts and ends. Reset In/Reset Out clear a mark back to the full " +
                "clip. Marks stick with that specific clip, wherever it's moved.\n\n" +
                "Green (In) and red (Out) flags on a button's bottom corners show a clip has " +
                "custom marks, with the trimmed time shown. If a clip's trimmed length is very " +
                "short, its Duration label turns red as a heads-up that crossfading it may " +
                "briefly hiccup.");

            AddSection("Display & Audio Devices",
                "Open Settings (the gear icon, top-right). Playback Display has an Identify " +
                "button that briefly numbers each connected monitor. Audio Output Device has a " +
                "Refresh button to rescan connected devices. Both take effect once you click " +
                "Apply.");

            AddSection("Background Image",
                "Settings \u2192 Background Image lets you load a logo or sponsor slide " +
                "(png/jpg). Two independent checkboxes control when it shows: while stopped, " +
                "and/or during audio-only clips. With no image loaded, both simply show black.");

            AddSection("Saving & Loading Shows",
                "Settings \u2192 New/Save/Load Show. A saved .playback file includes every " +
                "clip, page, mark, and setting - your whole show in one file. Loading a show " +
                "warns you if any clip files can't be found, and leaves those buttons empty " +
                "rather than guessing.");
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

        private void AddEndActionsSection()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "End Actions",
                Foreground = HeaderBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 14, 0, 4)
            });

            AddEndActionRow(EndAction.LoopCut, "Loop (Cut): seamless native loop, no crossfade.");
            AddEndActionRow(EndAction.LoopFade, "Loop (Fade): loops with a crossfade into itself.");
            AddEndActionRow(EndAction.PlayNext, "Play Next: triggers the next loaded clip on the same page.");
            AddEndActionRow(EndAction.PlaySelected, "Play Selected: jumps to one specific clip you choose " +
                "(right-click \u2192 Set End Action \u2192 Play Selected Clip...).");
            AddEndActionRow(EndAction.PauseOnLastFrame, "Pause on Last Frame: freezes on the final frame.");
            AddEndActionRow(EndAction.StopToBlack, "Stop to Black: fades out to nothing.");

            ContentPanel.Children.Add(new TextBlock
            {
                Text = "A small icon in each loaded button's top-right corner shows its current End Action " +
                       "at a glance - the same icons shown above.",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        private void AddEndActionRow(EndAction action, string text)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var iconHolder = new Border { Width = 26, Height = 26, Margin = new Thickness(0, 0, 8, 0), Child = BuildEndActionIcon(action) };
            DockPanel.SetDock(iconHolder, Dock.Left);
            row.Children.Add(iconHolder);

            row.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            ContentPanel.Children.Add(row);
        }

        /// <summary>Same six shapes as MainWindow.xaml's clip button decorations, at a
        /// larger, more legible size for this reference page. See the class-level comment
        /// about these being a second, hand-kept-in-sync copy, not a shared source.</summary>
        private static Viewbox BuildEndActionIcon(EndAction action)
        {
            var canvas = new Canvas { Width = 14, Height = 14 };

            switch (action)
            {
                case EndAction.LoopCut:
                case EndAction.LoopFade:
                    canvas.Children.Add(new Path
                    {
                        Data = Geometry.Parse("M 10,3 A 5,5 0 1 1 7,2"),
                        Stroke = Brushes.White,
                        StrokeThickness = 1.4,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    });
                    canvas.Children.Add(new Polygon { Points = PointCollection.Parse("10,3 12.5,2 11.5,5"), Fill = Brushes.White });
                    break;

                case EndAction.PauseOnLastFrame:
                    canvas.Children.Add(MakeBar(4, 2.5));
                    canvas.Children.Add(MakeBar(8, 2.5));
                    break;

                case EndAction.StopToBlack:
                    var square = new Rectangle { Width = 8, Height = 8, Fill = Brushes.White };
                    Canvas.SetLeft(square, 3);
                    Canvas.SetTop(square, 3);
                    canvas.Children.Add(square);
                    break;

                case EndAction.PlayNext:
                    canvas.Children.Add(new Polygon { Points = PointCollection.Parse("3,2 3,12 12,7"), Fill = Brushes.White });
                    break;

                case EndAction.PlaySelected:
                    canvas.Children.Add(new Polygon { Points = PointCollection.Parse("1,2 1,12 7,7"), Fill = Brushes.White });
                    canvas.Children.Add(new Line { X1 = 9, Y1 = 3, X2 = 13, Y2 = 11, Stroke = Brushes.White, StrokeThickness = 1.5 });
                    canvas.Children.Add(new Line { X1 = 13, Y1 = 3, X2 = 9, Y2 = 11, Stroke = Brushes.White, StrokeThickness = 1.5 });
                    break;
            }

            return new Viewbox { Width = 22, Height = 22, Stretch = Stretch.Uniform, Child = canvas };
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