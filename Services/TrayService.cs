using System.Windows;
using ElectroScroll.Models;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ElectroScroll.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _enabledItem;
    private readonly AppSettings _settings;
    private readonly ScrollController _controller;
    private readonly Window _window;

    public TrayService(Window window, AppSettings settings, ScrollController controller)
    {
        _window = window;
        _settings = settings;
        _controller = controller;

        _enabledItem = new Forms.ToolStripMenuItem("Enabled")
        {
            Checked = settings.Enabled,
            CheckOnClick = true
        };
        _enabledItem.CheckedChanged += (_, _) =>
        {
            _controller.Enabled = _enabledItem.Checked;
        };
        _controller.EnabledChanged += OnControllerEnabledChanged;

        var openItem = new Forms.ToolStripMenuItem("Open", null, (_, _) => ShowWindow());
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) =>
        {
            if (_window is MainWindow mainWindow)
            {
                mainWindow.AllowClose = true;
            }

            System.Windows.Application.Current.Shutdown();
        });

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(openItem);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "ElectroScroll",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Dispose()
    {
        _controller.EnabledChanged -= OnControllerEnabledChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void ShowWindow()
    {
        _enabledItem.Checked = _settings.Enabled;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    private void OnControllerEnabledChanged(object? sender, EventArgs e)
    {
        _window.Dispatcher.BeginInvoke(() =>
        {
            if (_enabledItem.IsDisposed || _enabledItem.Checked == _controller.Enabled)
            {
                return;
            }

            _enabledItem.Checked = _controller.Enabled;
        });
    }
}
