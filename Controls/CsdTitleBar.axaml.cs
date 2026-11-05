using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Tasks.Platform;

namespace Tasks.Controls;

public partial class CsdTitleBar : UserControl
{
    private Window? _hostWindow;
    private string _iconTheme = "Adwaita";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CsdTitleBar, string>(nameof(Title), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set
        {
            SetValue(TitleProperty, value);
            if (TitleText != null)
                TitleText.Text = value;
        }
    }

    public CsdTitleBar()
    {
        InitializeComponent();

        TitleText.Text = Title;
        DragArea.PointerPressed += DragArea_PointerPressed;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (VisualRoot is Window window)
        {
            _hostWindow = window;
            _hostWindow.PropertyChanged += HostWindow_PropertyChanged;
            if (string.IsNullOrEmpty(Title))
                Title = _hostWindow.Title ?? "";

            SetupDecorations();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_hostWindow != null)
        {
            _hostWindow.PropertyChanged -= HostWindow_PropertyChanged;
            _hostWindow = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void HostWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            BuildButtons();
        }
    }

    private void DragArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_hostWindow == null) return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (NativeWindowMenu.TryShowSystemMenu(_hostWindow, e))
            {
                e.Handled = true;
                return;
            }
        }

        if (e.ClickCount == 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _hostWindow.BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void ToggleMaximize()
    {
        if (_hostWindow == null) return;
        _hostWindow.WindowState = _hostWindow.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void SetupDecorations()
    {
        _iconTheme = GnomeDesktopService.GetActiveIconTheme();
        BuildButtons();
    }

    public void RefreshButtons()
    {
        BuildButtons();
    }

    private void BuildButtons()
    {
        LeftButtonsPanel.Children.Clear();
        RightButtonsPanel.Children.Clear();

        var layout = GnomeDesktopService.GetButtonLayout();

        if (!WindowEnvironment.IsMacOS)
        {
            foreach (var btn in layout.LeftButtons)
                LeftButtonsPanel.Children.Add(CreateButton(btn));
        }

        foreach (var btn in layout.RightButtons)
            RightButtonsPanel.Children.Add(CreateButton(btn));
    }

    public Button CreateButton(WindowButtonKind kind)
    {
        var button = new Button();

        if (WindowEnvironment.IsWindows)
        {
            button.Classes.Add("win-caption");
            if (kind == WindowButtonKind.Close)
                button.Classes.Add("close");

            button.Content = CreateWindowsIcon(kind);
        }
        else if (WindowEnvironment.IsMacOS)
        {
            button.Classes.Add("mac-caption");
            if (kind == WindowButtonKind.Close) button.Classes.Add("close");
            else if (kind == WindowButtonKind.Minimize) button.Classes.Add("minimize");
            else if (kind == WindowButtonKind.Maximize) button.Classes.Add("maximize");
        }
        else // GNOME / Linux
        {
            button.Classes.Add("gnome-caption");
            button.Content = CreateGnomeIcon(kind);
        }

        button.Click += (_, _) => HandleButtonClick(kind);
        return button;
    }

    private Control CreateGnomeIcon(WindowButtonKind kind)
    {
        var isMaximized = _hostWindow?.WindowState == WindowState.Maximized;
        Geometry geom = kind switch
        {
            WindowButtonKind.Close => LinuxIconThemeResolver.GetCloseIcon(_iconTheme),
            WindowButtonKind.Minimize => LinuxIconThemeResolver.GetMinimizeIcon(_iconTheme),
            WindowButtonKind.Maximize => isMaximized
                ? LinuxIconThemeResolver.GetRestoreIcon(_iconTheme)
                : LinuxIconThemeResolver.GetMaximizeIcon(_iconTheme),
            _ => LinuxIconThemeResolver.GetCloseIcon(_iconTheme)
        };

        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = geom,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        path[!Shape.FillProperty] = this.GetResourceObservable("SystemControlForegroundBaseHighBrush").ToBinding();
        return path;
    }

    private Control CreateWindowsIcon(WindowButtonKind kind)
    {
        var isMaximized = _hostWindow?.WindowState == WindowState.Maximized;

        if (kind == WindowButtonKind.Minimize)
        {
            // A 10px wide, 1px high rectangle prevents Avalonia Uniform stretch collapse
            var rect = new Rectangle
            {
                Width = 10,
                Height = 1,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            rect[!Shape.FillProperty] = this.GetResourceObservable("SystemControlForegroundBaseHighBrush").ToBinding();
            return rect;
        }

        string data = kind switch
        {
            WindowButtonKind.Close => "M 0,0 L 10,10 M 10,0 L 0,10",
            WindowButtonKind.Maximize => isMaximized
                ? "M 2,0 L 10,0 L 10,8 L 8,8 L 8,10 L 0,10 L 0,2 L 2,2 Z M 2,2 L 2,8 L 8,8 L 8,2 Z"
                : "M 0.5,0.5 L 9.5,0.5 L 9.5,9.5 L 0.5,9.5 Z",
            _ => ""
        };

        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse(data),
            StrokeThickness = 1,
            Width = 10,
            Height = 10,
            Stretch = Stretch.None,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        path[!Shape.StrokeProperty] = this.GetResourceObservable("SystemControlForegroundBaseHighBrush").ToBinding();
        return path;
    }

    private void HandleButtonClick(WindowButtonKind kind)
    {
        if (_hostWindow == null) return;

        switch (kind)
        {
            case WindowButtonKind.Close:
                _hostWindow.Close();
                break;
            case WindowButtonKind.Minimize:
                _hostWindow.WindowState = WindowState.Minimized;
                break;
            case WindowButtonKind.Maximize:
                ToggleMaximize();
                break;
        }
    }
}
