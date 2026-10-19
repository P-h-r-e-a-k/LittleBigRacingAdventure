using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LBRAInstaller;

// The updater's two small windows, in the setup window's look (App.xaml's brushes and button style): the question asked when a new
// version is ready and the player chose "Ask first", and an update's progress.
internal static class UpdateLook
{
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    public static void Dress(Window window, string title)
    {
        window.Title = title;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x1A));
        window.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xE6, 0xD8));
        window.FontFamily = new FontFamily("Segoe UI");
        window.FontSize = 13;
        window.ShowInTaskbar = true;
    }

    public static StackPanel Header()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Little Big", FontSize = 15, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xB8, 0xE8)) });
        panel.Children.Add(new TextBlock
        {
            Text = "HALLOWEEN RACING ADVENTURE", FontSize = 22, FontWeight = FontWeights.Black, Foreground = Brush("Pumpkin"),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0x4A, 0x00), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.7 },
        });
        return panel;
    }
}

// "A new version of Little Big Halloween Racing Adventure is ready. Install it now?" -- Install, or Later (the download kept)
internal sealed class AskWindow : Window
{
    public bool Install { get; private set; }

    public AskWindow(string? version)
    {
        UpdateLook.Dress(this, GameInstall.Title);
        Topmost = true;
        var root = new StackPanel { Margin = new Thickness(22, 16, 22, 18), Width = 540 };
        root.Children.Add(UpdateLook.Header());
        root.Children.Add(new TextBlock { Text = $"A new version of {GameInstall.Title} is ready. Install it now?", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), FontSize = 14 });
        root.Children.Add(new TextBlock
        {
            Text = (version is not null ? $"Version {version}. " : "") + "Your saves, best laps and settings are kept. The game starts again when it is done.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xAF, 0xCB)),
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var install = new Button { Content = "Install", Background = UpdateLook.Brush("Ember"), IsDefault = true, MinWidth = 90 };
        install.Click += (_, _) => { Install = true; Close(); };
        var later = new Button { Content = "Later", Margin = new Thickness(10, 0, 0, 0), IsCancel = true, MinWidth = 90 };
        later.Click += (_, _) => Close();
        buttons.Children.Add(install);
        buttons.Children.Add(later);
        root.Children.Add(buttons);
        Content = root;
        Loaded += (_, _) => { Activate(); install.Focus(); };
    }
}

// An update's progress: the install's steps, as the setup window shows them
internal sealed class UpdateProgressWindow : Window
{
    private readonly ProgressBar bar;
    private readonly TextBlock status;
    private bool done;

    public UpdateProgressWindow()
    {
        UpdateLook.Dress(this, $"{GameInstall.Title} - Update");
        var root = new StackPanel { Margin = new Thickness(22, 16, 22, 18), Width = 470 };
        root.Children.Add(UpdateLook.Header());
        root.Children.Add(new TextBlock { Text = "Updating the game. Your saves, best laps and settings are kept.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        bar = new ProgressBar { Height = 10, Margin = new Thickness(0, 12, 0, 0), Minimum = 0, Maximum = 1, Foreground = UpdateLook.Brush("Pumpkin"), Background = UpdateLook.Brush("Panel"), BorderBrush = UpdateLook.Brush("Edge") };
        status = new TextBlock { Margin = new Thickness(0, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xAF, 0xCB)), TextTrimming = TextTrimming.CharacterEllipsis };
        root.Children.Add(bar);
        root.Children.Add(status);
        Content = root;
        // (not closed half-way through: the install finishes)
        Closing += (_, e) => e.Cancel = !done;
    }

    public void Report(double fraction, string what)
    {
        bar.Value = fraction;
        status.Text = what;
    }

    public void Done()
    {
        done = true;
        Close();
    }

    public new void Close()
    {
        done = true;
        base.Close();
    }
}
