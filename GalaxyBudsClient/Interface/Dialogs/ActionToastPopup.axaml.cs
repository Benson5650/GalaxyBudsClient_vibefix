using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GalaxyBudsClient.Interface.StyledWindow;
using GalaxyBudsClient.Model.Config;
using GalaxyBudsClient.Model.Constants;
using GalaxyBudsClient.Platform;
using Timer = System.Timers.Timer;

namespace GalaxyBudsClient.Interface.Dialogs;

public partial class ActionToastPopup : Window
{
    private readonly Timer _timer = new(2000) { AutoReset = false };
    private CancellationTokenSource _hideCts = new();

    public ActionToastPopup()
    {
        InitializeComponent();
        Settings.MainSettingsPropertyChanged += OnMainSettingsPropertyChanged;
        _timer.Elapsed += (_, _) => Dispatcher.UIThread.Post(Hide, DispatcherPriority.Render);
    }

    private void OnMainSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.Data.Theme) or nameof(Settings.Data.BlurStrength))
            RequestedThemeVariant = IStyledWindow.GetThemeVariant();
    }

    public void ShowToast(string iconGlyph, string action, string status)
    {
        // Cancel any in-progress hide animation
        _hideCts.Cancel();
        _hideCts = new CancellationTokenSource();

        Dispatcher.UIThread.Post(() =>
        {
            IconText.Text = iconGlyph;
            ActionLabel.Text = action;
            StatusLabel.Text = status;

            UpdatePosition();
            RequestedThemeVariant = IStyledWindow.GetThemeVariant();
            Opacity = 1;
            IsVisible = true;
            OuterBorder.Tag = "showing";

            base.Show();

            _timer.Stop();
            _timer.Start();
        });
    }

    public override void Hide()
    {
        _timer.Stop();
        OuterBorder.Tag = "hiding";

        var cts = _hideCts;
        Task.Delay(350, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
                Dispatcher.UIThread.InvokeAsync(() => IsVisible = false);
        }, TaskContinuationOptions.None);
    }

    private void UpdatePosition()
    {
        var workArea = (Screens.Primary ?? Screens.All[0]).WorkingArea;
        var scaling = RenderScaling;
        var padding = (int)(20 * scaling);

        Position = Settings.Data.PopupPlacement switch
        {
            PopupPlacement.TopLeft => new Avalonia.PixelPoint(workArea.X + padding, workArea.Y + padding),
            PopupPlacement.TopCenter => new Avalonia.PixelPoint(
                (int)(workArea.Width / 2f - Width * scaling / 2 + workArea.X), workArea.Y + padding),
            PopupPlacement.TopRight => new Avalonia.PixelPoint(
                (int)(workArea.Width - Width * scaling + workArea.X - padding), workArea.Y + padding),
            PopupPlacement.BottomLeft => new Avalonia.PixelPoint(workArea.X + padding,
                (int)(workArea.Height - Height * scaling + workArea.Y - padding)),
            PopupPlacement.BottomCenter => new Avalonia.PixelPoint(
                (int)(workArea.Width / 2f - Width * scaling / 2 + workArea.X),
                (int)(workArea.Height - Height * scaling + workArea.Y - padding)),
            _ => new Avalonia.PixelPoint(
                (int)(workArea.Width - Width * scaling + workArea.X - padding),
                (int)(workArea.Height - Height * scaling + workArea.Y - padding))
        };
    }

    private void Window_OnPointerPressed(object? sender, PointerPressedEventArgs e) => Hide();
}
