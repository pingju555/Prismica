using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Prismica.Core.Components;
using Prismica.Core.Parameters;
using Prismica.Core.Persistence;

// 本项目同时启用 WPF 与 Windows Forms（托盘 NotifyIcon），两者均有 Control/Panel/TextBox/Button/
// CheckBox/ComboBox/MessageBox 等同名类型（Forms 由 UseWindowsForms 全局引入）。用别名消除歧义，
// 指向 WPF 版本。
using WTxt = System.Windows.Controls.TextBox;
using WBt = System.Windows.Controls.Button;
using WChk = System.Windows.Controls.CheckBox;
using WCbo = System.Windows.Controls.ComboBox;
using WPan = System.Windows.Controls.Panel;
using WCtl = System.Windows.Controls.Control;
using WMsg = System.Windows.MessageBox;

namespace Prismica.App;

/// <summary>
/// 布局模式下的实例属性面板：调整选中组件实例的尺寸/位置（X/Y/W/H），
/// 以及其 <c>[Interface.*]</c> 暴露的变量（数据驱动生成控件）。
/// 修改经回调写回 layout 实例 -> 注入变量层 -> 实时重渲染 -> 持久化。
/// </summary>
public sealed class ComponentPropertyWindow : Window
{
    private readonly ComponentDefinition _def;
    private readonly ComponentInstance _inst;
    private readonly Action<ComponentInstance> _onApply;

    private readonly WTxt _xBox = new();
    private readonly WTxt _yBox = new();
    private readonly WTxt _wBox = new();
    private readonly WTxt _hBox = new();

    private readonly Dictionary<string, WCtl> _controls = new();
    private readonly Dictionary<string, Func<object>> _getters = new();

    private static readonly Regex HexColor = new("^#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);

    public ComponentPropertyWindow(ComponentDefinition def, ComponentInstance inst, Action<ComponentInstance> onApply)
    {
        _def = def;
        _inst = inst;
        _onApply = onApply;

        Title = $"Properties - {def.Name}";
        Width = 360;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Margin = new Thickness(12) };
        scroll.Content = panel;
        Content = scroll;

        panel.Children.Add(Header("Size & Position"));
        AddNumberRow(panel, "X", _inst.Bounds.X, _xBox);
        AddNumberRow(panel, "Y", _inst.Bounds.Y, _yBox);
        AddNumberRow(panel, "Width", _inst.Bounds.Width, _wBox);
        AddNumberRow(panel, "Height", _inst.Bounds.Height, _hBox);

        panel.Children.Add(Header("Interface Variables"));
        if (_def.Interface is null || _def.Interface.Parameters.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "(此组件未声明 [Interface.*] 接口)", Foreground = System.Windows.Media.Brushes.Gray });
        }
        else
        {
            foreach (var (key, p) in _def.Interface.Parameters)
            {
                var current = _inst.ParameterOverrides.TryGetValue(key, out var v)
                    ? v?.ToString()
                    : p.DefaultValue?.ToString();
                var (ctl, get) = BuildControl(p, current);
                _controls[key] = ctl;
                _getters[key] = get;
                panel.Children.Add(new TextBlock { Text = p.Description, Margin = new Thickness(0, 6, 0, 0) });
                panel.Children.Add(ctl);
            }
        }

        var apply = new WBt
        {
            Content = "Apply",
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        apply.Click += OnApplyClick;
        panel.Children.Add(apply);
    }

    private static TextBlock Header(string text) =>
        new() { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) };

    private void AddNumberRow(WPan panel, string label, double value, WTxt box)
    {
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 70, VerticalAlignment = VerticalAlignment.Center });
        box.Text = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        box.Width = 200;
        row.Children.Add(box);
        panel.Children.Add(row);
    }

    private static (WCtl Control, Func<object> Get) BuildControl(ParameterInfo p, string? current)
    {
        switch (p.Type)
        {
            case ParameterType.Bool:
                var cb = new WChk { IsChecked = bool.TryParse(current, out var b) && b, VerticalAlignment = VerticalAlignment.Center };
                return (cb, () => cb.IsChecked == true);

            case ParameterType.Select:
                var combo = new WCbo { IsEditable = true, Width = 260 };
                foreach (var o in p.Options ?? Array.Empty<string>()) combo.Items.Add(o);
                combo.Text = current ?? p.DefaultValue?.ToString() ?? "";
                return (combo, () => combo.Text ?? "");

            case ParameterType.Color:
            case ParameterType.Font:
            case ParameterType.Url:
            case ParameterType.Text:
            case ParameterType.String:
                var tb = new WTxt { Text = current ?? p.DefaultValue?.ToString() ?? "", Width = 260 };
                return (tb, () => tb.Text ?? "");

            case ParameterType.Number:
            case ParameterType.Slider:
            default:
                var nt = new WTxt { Text = current ?? p.DefaultValue?.ToString() ?? "0", Width = 260 };
                if (p.Min is not null || p.Max is not null)
                    nt.ToolTip = $"范围: {p.Min ?? double.MinValue} ~ {p.Max ?? double.MaxValue}";
                return (nt, () => nt.Text ?? "0");
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        var overrides = new Dictionary<string, object>();
        foreach (var kv in _getters)
        {
            var raw = kv.Value();
            // 颜色类需做 hex 合法性校验，避免注入非法变量。
            if (_def.Interface.Parameters.TryGetValue(kv.Key, out var p) && p.Type == ParameterType.Color)
            {
                var s = raw?.ToString() ?? "";
                if (!HexColor.IsMatch(s))
                {
                    WMsg.Show($"参数 '{p.Description}' 不是合法颜色（应为 #RRGGBB 或 #RRGGBBAA）。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            overrides[kv.Key] = raw!;
        }

        if (!double.TryParse(_xBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(_yBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var y) ||
            !double.TryParse(_wBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var w) ||
            !double.TryParse(_hBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var h) ||
            w <= 0 || h <= 0)
        {
            WMsg.Show("尺寸/位置必须为数字，且宽高须大于 0。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var edited = _inst with
        {
            Bounds = new Prismica.Core.Primitives.Rect(x, y, w, h),
            ParameterOverrides = overrides
        };
        _onApply(edited);
        Close();
    }
}
