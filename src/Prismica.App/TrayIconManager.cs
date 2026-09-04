using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Prismica.App;

/// <summary>
/// 系统托盘图标管理器：右键菜单（Studio/自启/退出）、左键单击显隐。
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private bool _disposed;

    public event Action? OnOpenStudio;
    public event Action? OnExit;
    public event Action? OnCheckUpdates;
    public event Action? OnToggleLayoutMode;

    public TrayIconManager()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Prismica",
            Visible = true
        };

        _autoStartItem = new ToolStripMenuItem("Auto Start")
        {
            Checked = AutoStartManager.IsAutoStartEnabled,
            CheckOnClick = true
        };
        _autoStartItem.Click += (_, _) =>
        {
            AutoStartManager.Toggle();
            _autoStartItem.Checked = AutoStartManager.IsAutoStartEnabled;
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Studio", null, (_, _) => OnOpenStudio?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add("Check for Updates", null, (_, _) => OnCheckUpdates?.Invoke());
        menu.Items.Add("Toggle Layout Mode", null, (_, _) => OnToggleLayoutMode?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => OnExit?.Invoke());
        _notifyIcon.ContextMenuStrip = menu;

        _notifyIcon.DoubleClick += (_, _) => OnOpenStudio?.Invoke();
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    private static Icon CreateDefaultIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 画一个简单的 "P" 字母
        using var brush = new SolidBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x88)); // 绿色
        using var font = new Font("Arial", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString("P", font, brush, 2, 2);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
