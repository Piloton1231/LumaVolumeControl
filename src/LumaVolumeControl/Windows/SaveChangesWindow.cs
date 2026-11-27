using LumaVolumeControl.Infrastructure;

namespace LumaVolumeControl.Windows;

public sealed class SaveChangesWindow : Window
{
    public SaveChangesWindow()
    {
        Width = 380; Height = 190;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = AppLocalization.Text("変更した設定を保存しますか？"), FontSize = 18, Margin = new Thickness(0, 8, 0, 26) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var no = new Button { Content = AppLocalization.Text("いいえ"), MinWidth = 100, Margin = new Thickness(0, 0, 10, 0) };
        var yes = new Button { Content = AppLocalization.Text("はい"), MinWidth = 100, Style = (Style)FindResource("PrimaryButton"), IsDefault = true };
        no.Click += (_, _) => { DialogResult = false; };
        yes.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(no); buttons.Children.Add(yes); panel.Children.Add(buttons);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(17,20,27)), BorderBrush = new SolidColorBrush(Color.FromRgb(89,97,122)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = panel };
    }
}
