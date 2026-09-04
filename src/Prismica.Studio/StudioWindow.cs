using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Prismica.App;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Measures;
using Prismica.Core.Parsing;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Prismica.Core.Theming;
using Prismica.Core.MultiScreen;
using Prismica.Infra.Wpf;
using CoreSize = Prismica.Core.Primitives.Size;
using CoreRect = Prismica.Core.Primitives.Rect;

namespace Prismica.Studio;

/// <summary>
/// Studio 编辑器：打开/编辑 .pri，实时预览，Interface 参数面板，组件库。
/// </summary>
internal sealed class StudioWindow : Window
{
    private readonly IniSkinTextParser _parser = new();
    private readonly DefaultFormulaEngine _formula = new();
    private ComponentRuntime? _runtime;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly DispatcherTimer _editDebounceTimer = new();
    private readonly ComponentLibrary _componentLibrary;

    private readonly TextBox _editor = new()
    {
        FontFamily = new FontFamily("Cascadia Mono,Consolas"),
        FontSize = 13,
        AcceptsReturn = true,
        AcceptsTab = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private readonly Grid _previewHost = new()
    {
        MinWidth = 300,
        MinHeight = 200,
        Background = Brushes.White
    };

    private readonly TextBox _diagnostics = new()
    {
        IsReadOnly = true,
        Foreground = Brushes.DarkRed,
        FontFamily = new FontFamily("Cascadia Mono,Consolas"),
        Height = 80
    };

    private readonly ScrollViewer _schemaHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<Prismica.Core.Parsing.InterfaceParamEdit> _schemaModel = new();
    private readonly ScrollViewer _formulaHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<FormulaField> _formulaModel = new();
    private readonly ScrollViewer _animationHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<AnimationSpec> _animationModel = new();
    private readonly TextBlock _animationDiagnostics = new() { Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
    private readonly ScrollViewer _themeHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<ThemeSpec> _themeModel = new();
    private readonly ScrollViewer _screenProfileHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<ScreenProfileEdit> _screenProfileModel = new();
    private string _screenProfileDefault = "ClockCpu";
    private readonly TextBlock _screenProfileDiag = new() { Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };

    /// <summary>多屏差异化：单条屏幕分配编辑项（Key=匹配键, Components=逗号分隔组件名）。</summary>
    private sealed record ScreenProfileEdit(string Key, string Components);
    private string? _themeActiveName;
    private readonly ListBox _componentList = new() { SelectionMode = SelectionMode.Single };
    private readonly StackPanel _componentInfo = new() { Margin = new Thickness(8) };

    private string _currentFile = "";

    public StudioWindow()
    {
        Title = "Prismica Studio";
        Width = 1400;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var componentsDir = Path.Combine(AppContext.BaseDirectory, "Components");
        _componentLibrary = new ComponentLibrary(componentsDir);

        _editor.Text = SamplePri;

        // 主布局：左侧工具栏 + 中间编辑/预览 + 右侧参数/组件库
        var mainSplit = new Grid();
        mainSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // 组件库
        mainSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 编辑区
        mainSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // 参数面板

        // 左侧：组件库
        var libraryPanel = BuildLibraryPanel();
        Grid.SetColumn(libraryPanel, 0);
        mainSplit.Children.Add(libraryPanel);

        // 中间：编辑器 + 预览
        var centerSplit = new Grid();
        centerSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        centerSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        centerSplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });

        var editorLabel = new Label { Content = "编辑器 (.pri)" };
        var editorDock = new DockPanel();
        DockPanel.SetDock(editorLabel, Dock.Top);
        editorDock.Children.Add(editorLabel);
        editorDock.Children.Add(new Border { Child = _editor, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) });
        Grid.SetRow(editorDock, 0);
        centerSplit.Children.Add(editorDock);

        var previewLabel = new Label { Content = "实时预览" };
        var previewDock = new DockPanel();
        DockPanel.SetDock(previewLabel, Dock.Top);
        previewDock.Children.Add(previewLabel);
        previewDock.Children.Add(new Border { Child = _previewHost, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) });
        Grid.SetRow(previewDock, 1);
        centerSplit.Children.Add(previewDock);

        var diagLabel = new Label { Content = "诊断" };
        var diagDock = new DockPanel();
        DockPanel.SetDock(diagLabel, Dock.Top);
        diagDock.Children.Add(diagLabel);
        diagDock.Children.Add(_diagnostics);
        Grid.SetRow(diagDock, 2);
        centerSplit.Children.Add(diagDock);

        Grid.SetColumn(centerSplit, 1);
        mainSplit.Children.Add(centerSplit);

        // 右侧：参数 Schema / 公式编辑器（Tab）
        var rightTabs = new TabControl();
        var paramTab = new TabItem { Header = "参数 Schema" };
        paramTab.Content = BuildParameterPanel();
        var formulaTab = new TabItem { Header = "公式编辑器" };
        formulaTab.Content = BuildFormulaPanel();
        var animTab = new TabItem { Header = "动画" };
        animTab.Content = BuildAnimationPanel();
        var themeTab = new TabItem { Header = "主题" };
        themeTab.Content = BuildThemePanel();
        var multiTab = new TabItem { Header = "多屏" };
        multiTab.Content = BuildMultiScreenPanel();
        rightTabs.Items.Add(paramTab);
        rightTabs.Items.Add(formulaTab);
        rightTabs.Items.Add(animTab);
        rightTabs.Items.Add(themeTab);
        rightTabs.Items.Add(multiTab);
        Grid.SetColumn(rightTabs, 2);
        mainSplit.Children.Add(rightTabs);

        // 工具栏
        var open = new Button { Content = "打开", Width = 80 };
        var save = new Button { Content = "保存", Width = 80 };
        var refresh = new Button { Content = "刷新预览", Width = 100 };
        open.Click += (_, _) => OpenFile();
        save.Click += (_, _) => SaveFile();
        refresh.Click += (_, _) => RefreshPreview();
        refresh.IsDefault = true;

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        toolbar.Children.Add(open);
        toolbar.Children.Add(save);
        toolbar.Children.Add(refresh);

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(mainSplit);
        Content = root;

        Closing += OnClosing;

        _refreshTimer.Tick += (_, _) =>
        {
            if (_runtime is null) { _refreshTimer.Stop(); return; }
            try
            {
                var interval = _refreshTimer.Interval;
                _runtime.UpdateAsync(interval).GetAwaiter().GetResult();
                _previewHost.InvalidateVisual();
            }
            catch
            {
                _refreshTimer.Stop();
            }
        };

        // 编辑即预览（#18）：输入停止 350ms 后自动重建预览，无需点「刷新预览」
        _editDebounceTimer.Interval = TimeSpan.FromMilliseconds(350);
        _editDebounceTimer.Tick += (_, _) =>
        {
            _editDebounceTimer.Stop();
            RefreshPreview();
        };
        _editor.TextChanged += (_, _) =>
        {
            _editDebounceTimer.Stop();
            _editDebounceTimer.Start();
        };

        // 组件库双击加载
        _componentList.MouseDoubleClick += (_, _) => LoadSelectedComponent();
        RefreshComponentLibrary();
        RefreshPreview();
        RebuildMultiScreenPanel();
    }

    private DockPanel BuildLibraryPanel()
    {
        var label = new Label { Content = "组件库", FontWeight = FontWeights.Bold };
        var loadBtn = new Button { Content = "加载选中", Width = 80, Margin = new Thickness(4) };
        loadBtn.Click += (_, _) => LoadSelectedComponent();

        var header = new DockPanel();
        DockPanel.SetDock(loadBtn, Dock.Right);
        header.Children.Add(loadBtn);
        header.Children.Add(label);

        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(_componentList);

        var border = new Border
        {
            Child = panel,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4)
        };

        var result = new DockPanel();
        result.Children.Add(border);
        return result;
    }

    private DockPanel BuildParameterPanel()
    {
        var label = new Label { Content = "Interface 参数 Schema", FontWeight = FontWeights.Bold };
        var apply = new Button { Content = "应用参数", Width = 90, Margin = new Thickness(4) };
        apply.Click += (_, _) => ApplyInterfaceParams();
        var add = new Button { Content = "＋ 新增", Width = 70, Margin = new Thickness(4) };
        add.Click += (_, _) => AddSchemaParam();

        var header = new DockPanel();
        DockPanel.SetDock(apply, Dock.Right);
        header.Children.Add(apply);
        DockPanel.SetDock(add, Dock.Right);
        header.Children.Add(add);
        header.Children.Add(label);

        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(_schemaHost);

        var border = new Border
        {
            Child = panel,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4)
        };

        var result = new DockPanel();
        result.Children.Add(border);
        return result;
    }

    private void RefreshComponentLibrary()
    {
        _componentList.Items.Clear();
        foreach (var comp in _componentLibrary.GetAvailableComponents())
        {
            _componentList.Items.Add(new ListBoxItem
            {
                Content = comp.Name,
                Tag = comp,
                ToolTip = comp.Description
            });
        }
    }

    private void LoadSelectedComponent()
    {
        if (_componentList.SelectedItem is ListBoxItem item && item.Tag is ComponentInfo comp)
        {
            _currentFile = comp.FilePath;
            _editor.Text = File.ReadAllText(_currentFile);
            RefreshPreview();
        }
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog { Filter = "Prismica 组件 (*.pri;*.txt)|*.pri;*.txt|所有文件 (*.*)|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            _currentFile = dlg.FileName;
            _editor.Text = File.ReadAllText(_currentFile);
            RefreshPreview();
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_currentFile))
        {
            var dlg = new SaveFileDialog { Filter = "Prismica 组件 (*.pri)|*.pri|文本 (*.txt)|*.txt", FileName = "component.pri" };
            if (dlg.ShowDialog(this) != true) return;
            _currentFile = dlg.FileName;
        }
        File.WriteAllText(_currentFile, _editor.Text);
    }

    private void RefreshPreview()
    {
        _refreshTimer.Stop();
        _runtime?.Dispose();

        var resolved = ThemeResolver.Resolve(_editor.Text);
        var result = _parser.Parse(resolved, _currentFile);
        _diagnostics.Text = string.Join("\n", result.Diagnostics);
        RebuildInterfacePanel(result);
        RebuildFormulaPanel();
        RebuildAnimationPanel(result);
        RebuildThemePanel();

        _previewHost.Children.Clear();
        _runtime = null;

        if (result.Definition is not null)
        {
            var ps = result.Definition.Prismica;
            _runtime = ComponentRuntime.Create(result.Definition, _formula);
            var ctx = new RenderContext(_formula, result.Definition.Variables, 1.0, new CoreSize(ps.Width, ps.Height));
            var root = new WpfVisualRoot(result.Definition, ctx, _runtime.Meters, _runtime.Embeds);

            root.Measure(new System.Windows.Size(ps.Width, ps.Height));
            root.Arrange(new System.Windows.Rect(0, 0, ps.Width, ps.Height));

            var container = new Border
            {
                Child = root,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8)
            };
            _previewHost.Children.Add(container);

            StartRuntimeLoop(ps.Update > 0 ? ps.Update : 1000);
        }
    }

    private void StartRuntimeLoop(int intervalMs)
    {
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _refreshTimer.Start();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _runtime?.Dispose();
    }

    /// <summary>#20 参数 Schema 设计器：从 .pri 提取 [Interface.*] 定义，渲染为可编辑卡片。</summary>
    private void RebuildInterfacePanel(ParseResult result)
    {
        _schemaModel.Clear();
        _schemaModel.AddRange(InterfaceSchemaSerializer.Extract(_editor.Text));
        BuildSchemaPanel();
    }

    /// <summary>根据 _schemaModel 重建设计器面板。</summary>
    private void BuildSchemaPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(6) };

        if (_schemaModel.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "（无 Interface 参数，点击「＋ 新增」添加）",
                Foreground = Brushes.Gray,
                Opacity = 0.6,
                Margin = new Thickness(0, 4, 0, 0)
            });
            _schemaHost.Content = panel;
            return;
        }

        for (var i = 0; i < _schemaModel.Count; i++)
            panel.Children.Add(BuildSchemaCard(i));

        _schemaHost.Content = panel;
    }

    /// <summary>构建单个参数的可编辑卡片。index 用于在模型中定位该参数。</summary>
    private Border BuildSchemaCard(int index)
    {
        var p = _schemaModel[index];
        var card = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        // 标题行：名称 + 上移/下移/删除
        var titleRow = new DockPanel();
        var nameBox = new TextBox
        {
            Text = p.Name,
            FontWeight = FontWeights.SemiBold,
            Width = 130,
            Margin = new Thickness(0, 0, 4, 0)
        };
        nameBox.TextChanged += (_, _) =>
            _schemaModel[index] = _schemaModel[index] with { Name = nameBox.Text };
        DockPanel.SetDock(nameBox, Dock.Left);
        titleRow.Children.Add(nameBox);

        var del = new Button { Content = "✕", Width = 26, Margin = new Thickness(2, 0, 0, 0) };
        del.Click += (_, _) => RemoveSchemaParam(index);
        DockPanel.SetDock(del, Dock.Right);
        titleRow.Children.Add(del);

        var down = new Button { Content = "▼", Width = 26, Margin = new Thickness(2, 0, 0, 0) };
        down.Click += (_, _) => MoveSchemaParam(index, +1);
        DockPanel.SetDock(down, Dock.Right);
        titleRow.Children.Add(down);

        var up = new Button { Content = "▲", Width = 26, Margin = new Thickness(2, 0, 0, 0) };
        up.Click += (_, _) => MoveSchemaParam(index, -1);
        DockPanel.SetDock(up, Dock.Right);
        titleRow.Children.Add(up);

        card.Children.Add(titleRow);

        // 类型
        var typeBox = new ComboBox { Width = 130, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var t in InterfaceParamEdit.KnownTypes) typeBox.Items.Add(t);
        typeBox.SelectedItem = p.Type;
        typeBox.SelectionChanged += (_, _) =>
        {
            if (typeBox.SelectedItem is string t)
            {
                _schemaModel[index] = _schemaModel[index] with { Type = t };
                BuildSchemaPanel(); // 类型变化影响 Min/Max/Options 可见性，重建卡片
            }
        };
        card.Children.Add(WithLabel("类型", typeBox));

        // 默认值
        var defaultBox = new TextBox { Text = p.Default, Margin = new Thickness(0, 2, 0, 2) };
        defaultBox.TextChanged += (_, _) =>
            _schemaModel[index] = _schemaModel[index] with { Default = defaultBox.Text };
        card.Children.Add(WithLabel("默认值", defaultBox));

        // 标签（Label）
        var labelBox = new TextBox { Text = p.Label ?? "", Margin = new Thickness(0, 2, 0, 2) };
        labelBox.TextChanged += (_, _) =>
            _schemaModel[index] = _schemaModel[index] with { Label = labelBox.Text };
        card.Children.Add(WithLabel("标签", labelBox));

        // 数值范围（Number/Slider）
        if (string.Equals(p.Type, "Number", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Type, "Slider", StringComparison.OrdinalIgnoreCase))
        {
            var minBox = new TextBox { Text = p.Min ?? "", Width = 60 };
            minBox.TextChanged += (_, _) =>
                _schemaModel[index] = _schemaModel[index] with { Min = minBox.Text };
            var maxBox = new TextBox { Text = p.Max ?? "", Width = 60 };
            maxBox.TextChanged += (_, _) =>
                _schemaModel[index] = _schemaModel[index] with { Max = maxBox.Text };
            var range = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            range.Children.Add(new TextBlock { Text = "范围 ", VerticalAlignment = VerticalAlignment.Center });
            range.Children.Add(minBox);
            range.Children.Add(new TextBlock { Text = " ~ ", VerticalAlignment = VerticalAlignment.Center });
            range.Children.Add(maxBox);
            card.Children.Add(range);
        }

        // 选项（Select）
        if (string.Equals(p.Type, "Select", StringComparison.OrdinalIgnoreCase))
        {
            var optsBox = new TextBox { Text = p.Options ?? "", Margin = new Thickness(0, 2, 0, 2) };
            optsBox.TextChanged += (_, _) =>
                _schemaModel[index] = _schemaModel[index] with { Options = optsBox.Text };
            card.Children.Add(WithLabel("选项(逗号分隔)", optsBox));
        }

        return new Border
        {
            Child = card,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(3)
        };
    }

    private static DockPanel WithLabel(string text, FrameworkElement control)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        var lbl = new TextBlock
        {
            Text = text,
            Width = 96,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.SlateGray
        };
        DockPanel.SetDock(lbl, Dock.Left);
        dock.Children.Add(lbl);
        dock.Children.Add(control);
        return dock;
    }

    private void AddSchemaParam()
    {
        _schemaModel.Add(new InterfaceParamEdit($"Param{_schemaModel.Count + 1}", "Text", "", "", null, null, null));
        BuildSchemaPanel();
    }

    private void RemoveSchemaParam(int index)
    {
        if (index >= 0 && index < _schemaModel.Count)
        {
            _schemaModel.RemoveAt(index);
            BuildSchemaPanel();
        }
    }

    private void MoveSchemaParam(int index, int delta)
    {
        var target = index + delta;
        if (target >= 0 && target < _schemaModel.Count)
        {
            (_schemaModel[index], _schemaModel[target]) = (_schemaModel[target], _schemaModel[index]);
            BuildSchemaPanel();
        }
    }

    /// <summary>#20：把 Schema 模型经 InterfaceSchemaSerializer 回写进 .pri 文本（修复旧版按行 key 匹配导致参数值被丢弃的 bug），再重新预览。</summary>
    private void ApplyInterfaceParams()
    {
        if (_schemaModel.Count == 0) return;
        _editor.Text = InterfaceSchemaSerializer.Apply(_editor.Text, _schemaModel);
        RefreshPreview();
    }

    // ===== #21 公式编辑器 =====

    /// <summary>右侧「公式编辑器」Tab 的容器。</summary>
    private DockPanel BuildFormulaPanel()
    {
        var label = new Label { Content = "Calc 公式编辑器", FontWeight = FontWeights.Bold };
        var inner = new DockPanel();
        DockPanel.SetDock(label, Dock.Top);
        inner.Children.Add(label);
        inner.Children.Add(_formulaHost);

        var border = new Border
        {
            Child = inner,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4)
        };

        var result = new DockPanel();
        result.Children.Add(border);
        return result;
    }

    /// <summary>从 .pri 提取所有 [Measure*] Formula= 字段并重建卡片。</summary>
    private void RebuildFormulaPanel()
    {
        _formulaModel.Clear();
        _formulaModel.AddRange(FormulaFieldSerializer.Extract(_editor.Text));

        var panel = new StackPanel { Margin = new Thickness(6) };
        if (_formulaModel.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "（无 Calc 公式；在 .pri 中添加 [MeasureX] Measure=Calc Formula=...）",
                Foreground = Brushes.Gray,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
            _formulaHost.Content = panel;
            return;
        }

        for (var i = 0; i < _formulaModel.Count; i++)
            panel.Children.Add(BuildFormulaCard(i));

        _formulaHost.Content = panel;
    }

    /// <summary>构建单个公式的可编辑卡片：实时校验 + 函数插入 + 试算 + 应用。</summary>
    private Border BuildFormulaCard(int index)
    {
        var f = _formulaModel[index];
        var card = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        card.Children.Add(new TextBlock
        {
            Text = f.Section,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 2)
        });

        var formulaBox = new TextBox
        {
            Text = f.Formula,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        };

        var diag = new TextBlock { Margin = new Thickness(0, 0, 0, 2), TextWrapping = TextWrapping.Wrap };
        void ValidateNow()
        {
            var ds = FormulaValidator.Validate(formulaBox.Text);
            if (ds.Count == 0)
            {
                diag.Text = "✓ 语法正确";
                diag.Foreground = Brushes.Green;
            }
            else
            {
                diag.Text = "✗ " + ds[0].Message;
                diag.Foreground = Brushes.Red;
            }
        }
        formulaBox.TextChanged += (_, _) =>
        {
            _formulaModel[index] = _formulaModel[index] with { Formula = formulaBox.Text };
            ValidateNow();
        };
        card.Children.Add(formulaBox);
        card.Children.Add(diag);
        ValidateNow();

        // 函数目录：选择后插入模板到光标处
        var funcCombo = new ComboBox { Width = 220, Margin = new Thickness(0, 2, 0, 2), ToolTip = "选择函数插入到光标处" };
        funcCombo.Items.Add("插入函数…");
        foreach (var fi in FormulaCatalog.All)
            funcCombo.Items.Add($"{fi.Name}({string.Join(", ", fi.ParamNames)})");
        funcCombo.SelectedIndex = 0;
        funcCombo.SelectionChanged += (_, _) =>
        {
            if (funcCombo.SelectedIndex <= 0) return;
            var fi = FormulaCatalog.All[funcCombo.SelectedIndex - 1];
            var template = fi.ParamNames.Count == 0
                ? $"{fi.Name}()"
                : $"{fi.Name}({string.Join(", ", fi.ParamNames)})";
            var caret = formulaBox.CaretIndex;
            formulaBox.Text = formulaBox.Text.Insert(caret, template);
            formulaBox.CaretIndex = caret + template.Length;
            formulaBox.Focus();
            funcCombo.SelectedIndex = 0;
        };
        card.Children.Add(funcCombo);

        // 试算 + 应用
        var evalOut = new TextBlock { Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap };
        var evalBtn = new Button { Content = "试算", Width = 70, Margin = new Thickness(0, 2, 4, 2) };
        evalBtn.Click += (_, _) =>
        {
            try
            {
                var ast = _formula.Parse(formulaBox.Text);
                var ctx = new EvalContext(
                    new Dictionary<string, FormulaValue>(),
                    new Dictionary<string, IMeasure>(),
                    new Dictionary<string, object>(),
                    CancellationToken.None);
                var val = _formula.Evaluate(ast, ctx);
                evalOut.Text = $"= {val.AsString()}";
                evalOut.Foreground = Brushes.Black;
            }
            catch (Exception ex)
            {
                evalOut.Text = "⚠ " + ex.Message;
                evalOut.Foreground = Brushes.Red;
            }
        };

        var applyBtn = new Button { Content = "应用", Width = 70, Margin = new Thickness(0, 2, 0, 2) };
        applyBtn.Click += (_, _) =>
        {
            _editor.Text = FormulaFieldSerializer.Apply(_editor.Text, _formulaModel);
            RefreshPreview();
        };

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        btnRow.Children.Add(evalBtn);
        btnRow.Children.Add(applyBtn);
        card.Children.Add(btnRow);
        card.Children.Add(evalOut);

        return new Border
        {
            Child = card,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(3)
        };
    }

    // ===== #22 动画系统 =====

    /// <summary>右侧「动画」Tab 的容器。</summary>
    private DockPanel BuildAnimationPanel()
    {
        var label = new Label { Content = "动画设计器", FontWeight = FontWeights.Bold };
        var apply = new Button { Content = "应用动画", Width = 90, Margin = new Thickness(4) };
        apply.Click += (_, _) => ApplyAnimations();
        var add = new Button { Content = "＋ 新增", Width = 70, Margin = new Thickness(4) };
        add.Click += (_, _) => AddAnimationParam();

        var header = new DockPanel();
        DockPanel.SetDock(apply, Dock.Right);
        header.Children.Add(apply);
        DockPanel.SetDock(add, Dock.Right);
        header.Children.Add(add);
        header.Children.Add(label);

        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        DockPanel.SetDock(_animationDiagnostics, Dock.Bottom);
        panel.Children.Add(_animationDiagnostics);
        panel.Children.Add(_animationHost);

        var border = new Border
        {
            Child = panel,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4)
        };
        var result = new DockPanel();
        result.Children.Add(border);
        return result;
    }

    /// <summary>从解析结果同步模型并重建卡片；同时给出校验诊断。</summary>
    private void RebuildAnimationPanel(ParseResult result)
    {
        _animationModel.Clear();
        if (result.Definition is not null)
            _animationModel.AddRange(result.Definition.Animations);

        var panel = new StackPanel { Margin = new Thickness(6) };
        if (_animationModel.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "（无动画，点「＋ 新增」添加 [Animation*]）", Foreground = Brushes.Gray, Opacity = 0.6 });
        }
        else
        {
            for (int i = 0; i < _animationModel.Count; i++)
                panel.Children.Add(BuildAnimationCard(_animationModel[i], i));
        }
        _animationHost.Content = panel;

        var knownTargets = result.Definition is null
            ? new List<string>()
            : result.Definition.Meters.Select(m => m.Name)
                .Concat(result.Definition.Measures.Select(m => m.Name))
                .Concat(result.Definition.Embeds.Select(e => e.Name))
                .ToList();
        var diags = AnimationSpecSerializer.Validate(_animationModel, knownTargets);
        _animationDiagnostics.Text = diags.Count == 0
            ? "✓ 动画配置有效"
            : string.Join("\n", diags.Select(d => $"[{(d.Severity == DiagnosticSeverity.Error ? "错误" : "警告")}] {d.Message}"));
        _animationDiagnostics.Foreground = diags.Any(d => d.Severity == DiagnosticSeverity.Error) ? Brushes.DarkRed : Brushes.Gray;
    }

    private void AddAnimationParam()
    {
        var n = _animationModel.Count + 1;
        _animationModel.Add(new AnimationSpec($"Anim{n}", AnimationTrigger.OnShow, "", AnimationProperty.Opacity,
            0, 1, 300, "Linear", false, 0, 0));
        RebuildAnimationPanel(_parser.Parse(_editor.Text, _currentFile));
    }

    /// <summary>把模型回写 .pri 并刷新预览。</summary>
    private void ApplyAnimations()
    {
        _editor.Text = AnimationSpecSerializer.Apply(_editor.Text, _animationModel);
        RefreshPreview();
    }

    /// <summary>更新模型中第 index 条动画的某个字段。</summary>
    private void UpdateAnimation(int index, Func<AnimationSpec, AnimationSpec> mutate)
    {
        if (index < 0 || index >= _animationModel.Count) return;
        _animationModel[index] = mutate(_animationModel[index]);
    }

    private Border BuildAnimationCard(AnimationSpec spec, int index)
    {
        var card = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        var title = new DockPanel();
        var nameBox = new TextBox { Text = spec.Name, Width = 120, Margin = new Thickness(0, 0, 4, 0) };
        nameBox.TextChanged += (_, _) => UpdateAnimation(index, s => s with { Name = nameBox.Text });
        var del = new Button { Content = "✕", Width = 26 };
        del.Click += (_, _) => { _animationModel.RemoveAt(index); RebuildAnimationPanel(_parser.Parse(_editor.Text, _currentFile)); };
        var up = new Button { Content = "▲", Width = 26 };
        up.Click += (_, _) => { MoveAnimation(index, -1); };
        var down = new Button { Content = "▼", Width = 26 };
        down.Click += (_, _) => { MoveAnimation(index, 1); };
        DockPanel.SetDock(del, Dock.Right); title.Children.Add(del);
        DockPanel.SetDock(down, Dock.Right); title.Children.Add(down);
        DockPanel.SetDock(up, Dock.Right); title.Children.Add(up);
        title.Children.Add(nameBox);
        card.Children.Add(title);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        // 行 0：Trigger / Target
        var triggerCombo = BuildCombo(AnimationSpec.KnownTriggers, spec.Trigger.ToString(),
            v => UpdateAnimation(index, s => s with { Trigger = AnimationSpec.ParseTrigger(v) }), 0, 0);
        var targetBox = TextBox(spec.Target, t => UpdateAnimation(index, s => s with { Target = t }), 120);
        grid.Children.Add(Field("触发", triggerCombo, 0, 0));
        grid.Children.Add(Field("目标", targetBox, 1, 0));

        // 行 1：Property / Easing
        var propCombo = BuildCombo(AnimationSpec.KnownProperties, spec.Property.ToString(),
            v => UpdateAnimation(index, s => s with { Property = AnimationSpec.ParseProperty(v) }), 0, 1);
        var easingCombo = BuildCombo(NamedEasing.Names, spec.EasingName,
            v => UpdateAnimation(index, s => s with { EasingName = v }), 1, 1);
        grid.Children.Add(Field("属性", propCombo, 0, 1));
        grid.Children.Add(Field("缓动", easingCombo, 1, 1));

        // 行 2：From/To | Duration/Repeat/Delay
        var fromTo = new StackPanel { Orientation = Orientation.Horizontal };
        fromTo.Children.Add(TextBox(spec.From.ToString(System.Globalization.CultureInfo.InvariantCulture),
            t => UpdateAnimation(index, s => s with { From = ParseDoubleSafe(t, s.From) }), 56));
        fromTo.Children.Add(new TextBlock { Text = "→", Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center });
        fromTo.Children.Add(TextBox(spec.To.ToString(System.Globalization.CultureInfo.InvariantCulture),
            t => UpdateAnimation(index, s => s with { To = ParseDoubleSafe(t, s.To) }), 56));
        grid.Children.Add(Field("From→To", fromTo, 0, 2));

        var timing = new StackPanel { Orientation = Orientation.Horizontal };
        timing.Children.Add(TextBox(spec.DurationMs.ToString(), t => UpdateAnimation(index, s => s with { DurationMs = ParseIntSafe(t, s.DurationMs) }), 44));
        timing.Children.Add(new TextBlock { Text = "ms 重复", Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center });
        timing.Children.Add(TextBox(spec.Repeat.ToString(), t => UpdateAnimation(index, s => s with { Repeat = ParseIntSafe(t, s.Repeat) }), 32));
        timing.Children.Add(new TextBlock { Text = "延", Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center });
        timing.Children.Add(TextBox(spec.DelayMs.ToString(), t => UpdateAnimation(index, s => s with { DelayMs = ParseIntSafe(t, s.DelayMs) }), 32));
        grid.Children.Add(Field("时长/重复/延迟", timing, 1, 2));

        card.Children.Add(grid);

        var auto = new CheckBox { Content = "自动反向 (AutoReverse)", IsChecked = spec.AutoReverse, Margin = new Thickness(0, 4, 0, 0) };
        auto.Checked += (_, _) => UpdateAnimation(index, s => s with { AutoReverse = true });
        auto.Unchecked += (_, _) => UpdateAnimation(index, s => s with { AutoReverse = false });
        card.Children.Add(auto);

        return new Border
        {
            Child = card,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(3)
        };
    }

    private void MoveAnimation(int index, int dir)
    {
        var j = index + dir;
        if (j < 0 || j >= _animationModel.Count) return;
        (_animationModel[index], _animationModel[j]) = (_animationModel[j], _animationModel[index]);
        RebuildAnimationPanel(_parser.Parse(_editor.Text, _currentFile));
    }

    private static ComboBox BuildCombo(IEnumerable<string> items, string selected, Action<string> onPick, int col, int row)
    {
        var cmb = new ComboBox { Margin = new Thickness(0, 2, 4, 2) };
        foreach (var it in items) cmb.Items.Add(it);
        cmb.SelectedItem = selected;
        cmb.SelectionChanged += (_, _) => { if (cmb.SelectedItem is string v) onPick(v); };
        Grid.SetColumn(cmb, col); Grid.SetRow(cmb, row);
        return cmb;
    }

    private static StackPanel Field(string label, UIElement control, int col, int row)
    {
        var sp = new StackPanel { Margin = new Thickness(2) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.Gray });
        sp.Children.Add(control);
        Grid.SetColumn(sp, col); Grid.SetRow(sp, row);
        return sp;
    }

    private static TextBox TextBox(string text, Action<string> onText, int width = 120)
    {
        var tb = new TextBox { Text = text, Width = width, Margin = new Thickness(2) };
        tb.TextChanged += (_, _) => onText(tb.Text);
        return tb;
    }

    private static double ParseDoubleSafe(string s, double fallback) =>
        double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    private static int ParseIntSafe(string s, int fallback) =>
        int.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    #region 主题 Tab

    private DockPanel BuildThemePanel()
    {
        var label = new Label { Content = "主题", FontWeight = FontWeights.Bold };
        var apply = new Button { Content = "应用主题", Width = 80, Margin = new Thickness(4) };
        apply.Click += (_, _) => ApplyThemes();
        var add = new Button { Content = "＋ 新增主题", Width = 90, Margin = new Thickness(4) };
        add.Click += (_, _) => AddTheme();

        var header = new DockPanel();
        DockPanel.SetDock(apply, Dock.Right);
        DockPanel.SetDock(add, Dock.Right);
        header.Children.Add(apply);
        header.Children.Add(add);
        header.Children.Add(label);

        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(_themeHost);
        return panel;
    }

    private void RebuildThemePanel()
    {
        _themeModel.Clear();
        _themeModel.AddRange(ThemeCatalog.ExtractThemes(_editor.Text));
        _themeActiveName = ThemeCatalog.ExtractActiveName(_editor.Text);

        var panel = new StackPanel { Margin = new Thickness(6) };

        // 活动主题选择器
        var activeRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        activeRow.Children.Add(new TextBlock { Text = "活动主题", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        var combo = new ComboBox { MinWidth = 120 };
        foreach (var t in _themeModel) combo.Items.Add(t.Name);
        combo.SelectedItem = _themeActiveName;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string v)
            {
                _themeActiveName = v;
                RebuildThemePanel();
            }
        };
        activeRow.Children.Add(combo);
        panel.Children.Add(activeRow);

        var activeIdx = _themeModel.FindIndex(t => string.Equals(t.Name, _themeActiveName, StringComparison.OrdinalIgnoreCase));
        if (activeIdx < 0)
        {
            panel.Children.Add(new TextBlock { Text = "（无主题或活动主题未定义）", Foreground = Brushes.Gray, Opacity = 0.7 });
            _themeHost.Content = panel;
            return;
        }

        // 令牌卡片
        var theme = _themeModel[activeIdx];
        var tokenPanel = new StackPanel();
        foreach (var (key, value) in theme.Tokens)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var keyBox = new TextBox { Text = key, Width = 100, Margin = new Thickness(2), IsReadOnly = true };
            var valBox = TextBox(value, v => UpdateThemeToken(activeIdx, key, v));
            var del = new Button { Content = "✕", Width = 28, Margin = new Thickness(2) };
            del.Click += (_, _) => RemoveThemeToken(activeIdx, key);
            DockPanel.SetDock(del, Dock.Right);
            DockPanel.SetDock(keyBox, Dock.Left);
            row.Children.Add(del);
            row.Children.Add(keyBox);
            row.Children.Add(valBox);
            tokenPanel.Children.Add(row);
        }
        var addToken = new Button { Content = "＋ 添加令牌", Margin = new Thickness(2) };
        addToken.Click += (_, _) => AddThemeToken(activeIdx);
        tokenPanel.Children.Add(addToken);

        var deleteTheme = new Button { Content = "删除当前主题", Margin = new Thickness(2, 8, 2, 2) };
        deleteTheme.Click += (_, _) => RemoveTheme(activeIdx);
        tokenPanel.Children.Add(deleteTheme);

        panel.Children.Add(tokenPanel);
        _themeHost.Content = panel;
    }

    private void ApplyThemes()
    {
        _editor.Text = ThemeCatalog.Apply(_editor.Text, _themeModel, _themeActiveName);
        RefreshPreview();
    }

    private void AddTheme()
    {
        var n = _themeModel.Count + 1;
        var name = $"Theme{n}";
        while (_themeModel.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"Theme{++n}";
        _themeModel.Add(new ThemeSpec(name, new Dictionary<string, string> { ["Token"] = "#FFFFFFFF" }));
        _themeActiveName = name;
        RebuildThemePanel();
    }

    private void RemoveTheme(int index)
    {
        if (index < 0 || index >= _themeModel.Count) return;
        var removed = _themeModel[index];
        _themeModel.RemoveAt(index);
        if (string.Equals(_themeActiveName, removed.Name, StringComparison.OrdinalIgnoreCase))
            _themeActiveName = _themeModel.Count > 0 ? _themeModel[0].Name : null;
        RebuildThemePanel();
    }

    private void UpdateThemeToken(int themeIdx, string key, string value)
    {
        if (themeIdx < 0 || themeIdx >= _themeModel.Count) return;
        var t = _themeModel[themeIdx];
        var d = new Dictionary<string, string>(t.Tokens, StringComparer.OrdinalIgnoreCase) { [key] = value };
        _themeModel[themeIdx] = t with { Tokens = d };
    }

    private void AddThemeToken(int themeIdx)
    {
        if (themeIdx < 0 || themeIdx >= _themeModel.Count) return;
        var t = _themeModel[themeIdx];
        var d = new Dictionary<string, string>(t.Tokens, StringComparer.OrdinalIgnoreCase);
        var key = "NewToken";
        int n = 1;
        while (d.ContainsKey(key)) key = $"NewToken{n++}";
        d[key] = "#FFFFFFFF";
        _themeModel[themeIdx] = t with { Tokens = d };
        RebuildThemePanel();
    }

    private void RemoveThemeToken(int themeIdx, string key)
    {
        if (themeIdx < 0 || themeIdx >= _themeModel.Count) return;
        var t = _themeModel[themeIdx];
        var d = new Dictionary<string, string>(t.Tokens, StringComparer.OrdinalIgnoreCase);
        d.Remove(key);
        _themeModel[themeIdx] = t with { Tokens = d };
        RebuildThemePanel();
    }

    #endregion

    #region 多屏 Tab

    /// <summary>右侧「多屏」Tab 的容器。</summary>
    private DockPanel BuildMultiScreenPanel()
    {
        var label = new Label { Content = "多屏差异化配置", FontWeight = FontWeights.Bold };
        var apply = new Button { Content = "应用配置", Width = 90, Margin = new Thickness(4) };
        apply.Click += (_, _) => ApplyMultiScreen();
        var add = new Button { Content = "＋ 新增屏幕", Width = 90, Margin = new Thickness(4) };
        add.Click += (_, _) => AddScreenProfile();

        var header = new DockPanel();
        DockPanel.SetDock(apply, Dock.Right);
        header.Children.Add(apply);
        DockPanel.SetDock(add, Dock.Right);
        header.Children.Add(add);
        header.Children.Add(label);

        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        DockPanel.SetDock(_screenProfileDiag, Dock.Bottom);
        panel.Children.Add(_screenProfileDiag);
        panel.Children.Add(_screenProfileHost);

        var border = new Border
        {
            Child = panel,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4)
        };
        var result = new DockPanel();
        result.Children.Add(border);
        return result;
    }

    /// <summary>从 desktop.profile 读取并重建编辑面板。</summary>
    private void RebuildMultiScreenPanel()
    {
        var (profile, diags) = ScreenProfileCatalog.Parse(ScreenProfileCatalog.LoadProfileText());
        _screenProfileDefault = string.Join(",", profile.DefaultComponents);
        _screenProfileModel.Clear();
        _screenProfileModel.AddRange(profile.Screens
            .Select(a => new ScreenProfileEdit(a.ScreenKey, string.Join(",", a.Components))));

        var panel = new StackPanel { Margin = new Thickness(6) };

        var defaultRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        defaultRow.Children.Add(new TextBlock { Text = "默认组件(逗号分隔)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        var defaultBox = new TextBox { Text = _screenProfileDefault, MinWidth = 160 };
        defaultBox.TextChanged += (_, _) => _screenProfileDefault = defaultBox.Text;
        defaultRow.Children.Add(defaultBox);
        panel.Children.Add(defaultRow);

        panel.Children.Add(new TextBlock
        {
            Text = "匹配键：Primary=主屏 / Secondary=非主屏 / 数字=屏幕序号 / 其余=设备名子串",
            Foreground = Brushes.Gray, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
        });

        if (_screenProfileModel.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "（无屏幕分配，点「＋ 新增屏幕」添加）", Foreground = Brushes.Gray, Opacity = 0.7 });
        }
        else
        {
            for (int i = 0; i < _screenProfileModel.Count; i++)
                panel.Children.Add(BuildScreenProfileCard(i));
        }

        _screenProfileHost.Content = panel;

        _screenProfileDiag.Text = diags.Count == 0
            ? "✓ 配置有效"
            : string.Join("\n", diags.Select(d => $"[{(d.Severity == DiagnosticSeverity.Error ? "错误" : "警告")}] {d.Message}"));
        _screenProfileDiag.Foreground = diags.Any(d => d.Severity == DiagnosticSeverity.Error) ? Brushes.DarkRed : Brushes.Gray;
    }

    private Border BuildScreenProfileCard(int index)
    {
        var s = _screenProfileModel[index];
        var card = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        var title = new DockPanel();
        var keyBox = new TextBox { Text = s.Key, Width = 120, Margin = new Thickness(0, 0, 4, 0) };
        keyBox.TextChanged += (_, _) =>
            _screenProfileModel[index] = _screenProfileModel[index] with { Key = keyBox.Text };
        var del = new Button { Content = "✕", Width = 26, Margin = new Thickness(2, 0, 0, 0) };
        del.Click += (_, _) => RemoveScreenProfile(index);
        DockPanel.SetDock(del, Dock.Right);
        title.Children.Add(del);
        DockPanel.SetDock(keyBox, Dock.Left);
        title.Children.Add(keyBox);
        card.Children.Add(title);

        var compsBox = new TextBox { Text = s.Components, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
        compsBox.TextChanged += (_, _) =>
            _screenProfileModel[index] = _screenProfileModel[index] with { Components = compsBox.Text };
        card.Children.Add(WithLabel("组件(逗号分隔)", compsBox));

        return new Border
        {
            Child = card,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(3)
        };
    }

    private void AddScreenProfile()
    {
        _screenProfileModel.Add(new ScreenProfileEdit($"Screen{_screenProfileModel.Count}", "ClockCpu"));
        RebuildMultiScreenPanel();
    }

    private void RemoveScreenProfile(int index)
    {
        if (index < 0 || index >= _screenProfileModel.Count) return;
        _screenProfileModel.RemoveAt(index);
        RebuildMultiScreenPanel();
    }

    /// <summary>把面板内容经 ScreenProfileCatalog 序列化后写入用户配置文件。</summary>
    private void ApplyMultiScreen()
    {
        var sep = new[] { ',', ';' };
        var defaults = _screenProfileDefault
            .Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).ToList();
        var screens = _screenProfileModel
            .Select(s => new ScreenAssignment(s.Key, s.Components
                .Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0).ToList()))
            .ToList();

        var profile = new DesktopProfile("0.1", defaults, screens);
        var text = ScreenProfileCatalog.ToText(profile);

        var dir = Path.GetDirectoryName(ScreenProfileCatalog.ProfileSavePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(ScreenProfileCatalog.ProfileSavePath, text);

        _screenProfileDiag.Text = $"✓ 已保存到 {ScreenProfileCatalog.ProfileSavePath}（重启 Desktop 生效）";
        _screenProfileDiag.Foreground = Brushes.Green;
    }

    #endregion

    private const string SamplePri = @"
[Prismica]
Version=0.1
Name=StudioPreview
Update=1000
Width=240
Height=80
Theme=Dark

[Interface.Text]
Type=Text
Default=Prismica Studio
Label=Title Text

[Interface.FontSize]
Type=Number
Default=28
Min=8
Max=72
Label=Font Size

[Variables]
FontColor=@Theme.Accent

[MeterTitle]
Meter=String
Text=Prismica Studio
X=0 Y=0 W=240 H=40
FontSize=28
FontColor=@Theme.Text

[MeterSub]
Meter=String
Text=实时预览
X=0 Y=40 W=240 H=30
FontSize=16
FontColor=@Theme.Sub

[MeasureDemo]
Measure=Calc
Formula=[MeterTitle] * 2 + clamp(50, 0, 100)

[AnimationFadeIn]
Trigger=OnShow
Target=Sub
Property=Opacity
From=0
To=1
Duration=400
Easing=EaseOutQuad
AutoReverse=False
Repeat=0
Delay=0

[Theme.Dark]
Text=#FFF3F3F3
Sub=#FF9A9A9A
Accent=#FF4C8BF5

[Theme.Light]
Text=#FF1A1A1A
Sub=#FF666666
Accent=#FF1976D2
";
}
