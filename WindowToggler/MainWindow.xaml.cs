using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using static WindowToggler.GlobalMouseHook;

namespace WindowToggler;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static class NativeMethods
    {
        public delegate bool WndEnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern int EnumWindows(WndEnumProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder title, int size);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    }

    private IntPtr _windowHandle = IntPtr.Zero;
    private StringCollection _targetAppList = [];
    private ObservableCollection<WindowItem> _targetWidowList = [];
    private ObservableCollection<WindowItem> _allWindowList = [];
    private GlobalHotKey? _hideThisAppHotKey;
    private GlobalHotKey? _hideWindowHotKey;
    private WindowItem? _thisWindowItem;
    private GlobalMouseHook _mouseHook = GlobalMouseHook.Instance;

    public MainWindow()
    {
        InitializeComponent();

        // 绑定数据源
        TargetWindowsListView.ItemsSource = _targetWidowList;
        AllWindowsListView.ItemsSource = _allWindowList;

        // 填充下拉框内容
        FillComboBoxes();

        // 读取配置
        LoadSettings();
    }

    private void ToogleWindowListVisibility()
    {
        foreach (var window in _targetWidowList)
            window.ToggleVisibility();
    }

    private void ToogleByMousePosition(object? sender, MouseMoveEventArgs e)
    {
        int mouseX = e.X;
        int mouseY = e.Y;
        foreach (var window in _targetWidowList)
        {
            Rect rect = window.GetWindowRect();
            bool isInside = rect.Contains(mouseX, mouseY);
            if (isInside && !window.IsVisible)
            {
                window.Show();
            }
            else if (!isInside && window.IsVisible)
            {
                window.Hide();
            }
        }
    }

    private void FillComboBoxes()
    {
        var modifierKeys = new[] { "Alt", "Shift", "Control", "None" };
        var alphabet = Enumerable.Range('A', 26).Select(x => ((char)x).ToString()).ToArray();
        foreach (var comboBox in new[] { ModifierKey1ComboBox, ModifierKey2ComboBox, ModifierKey3ComboBox, ThisModifierKey1ComboBox, ThisModifierKey2ComboBox, ThisModifierKey3ComboBox })
        {
            comboBox.ItemsSource = modifierKeys;
        }
        VirtualKeyComboBox.ItemsSource = alphabet;
        ThisVirtualKeyComboBox.ItemsSource = alphabet;
    }

    private bool EnumWindowsProc(IntPtr handle, IntPtr lParam)
    {
        if (!NativeMethods.IsWindow(handle)) 
            return true;
        if (!NativeMethods.IsWindowEnabled(handle) && ListUnableWindowCheckBox.IsChecked == false)
            return true;
        int cTextLen = NativeMethods.GetWindowTextLength(handle);
        string title = string.Empty;
        if (cTextLen != 0)
        {
            StringBuilder text = new(cTextLen + 1);
            _ = NativeMethods.GetWindowText(handle, text, cTextLen + 1);
            title = text.ToString();
        }
        else if (ListUntitledWindowCheckBox.IsChecked == false)
            return true;
        bool isVisible = false;
        if (NativeMethods.IsWindowVisible(handle))
            isVisible = true;
        else if (ListInvisibleWindowCheckBox.IsChecked == false)
            return true;
        _ = NativeMethods.GetWindowThreadProcessId(handle, out int pid);
        string appPath;
        try
        {
            appPath = Process.GetProcessById(pid).MainModule?.FileName ?? "无法获取";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            appPath = "没有权限";
        }
        // 获取程序名,如何在_targetAppList中匹配,则添加到_targetWidowList
        if (_targetAppList.Contains(appPath) && isVisible)
        {
            _targetWidowList.Add(new WindowItem(isVisible, title, handle, pid, appPath));
        }
        _allWindowList.Add(new WindowItem(isVisible, title, handle, pid, appPath));
        return true;
    }
    // 刷新所有窗口列表
    private void RefreshAllWindowsListView()
    {
        _allWindowList.Clear();
        _ = NativeMethods.EnumWindows(EnumWindowsProc, IntPtr.Zero);
        _targetAppList.Clear();
    }
    // 刷新目标窗口列表，删除失效窗口，更新标题
    private void RefreshTargetWindowsListView()
    {
        for (int i = _targetWidowList.Count - 1; i >= 0; i--)
        {
            if (!NativeMethods.IsWindow(_targetWidowList[i].Handle))
            {
                _targetWidowList.Remove(_targetWidowList[i]);
                continue;
            }
            int cTextLen = NativeMethods.GetWindowTextLength(_targetWidowList[i].Handle);
            string title = string.Empty;
            if (cTextLen != 0)
            {
                StringBuilder text = new(cTextLen + 1);
                _ = NativeMethods.GetWindowText(_targetWidowList[i].Handle, text, cTextLen + 1);
                title = text.ToString();
            }
            if (_targetWidowList[i].Title != title)
            {
                _targetWidowList[i].Title = title;
            }
        }
    }

    private void LoadSettings()
    {
        // 读取配置并设置控件的值
        ListInvisibleWindowCheckBox.IsChecked = Settings.Default.ListInvisibleWindow;
        ListUntitledWindowCheckBox.IsChecked = Settings.Default.ListUntitledWindow;
        ListUnableWindowCheckBox.IsChecked = Settings.Default.ListUnableWindow;
        UseHotKeyCheckBox.IsChecked = Settings.Default.UseHotKey;
        UseMiddleButtonDownCheckBox.IsChecked = Settings.Default.UseMiddleButtonDown;
        UseSimultaneousLeftRightDownCheckBox.IsChecked = Settings.Default.UseSimultaneousLeftRightDown;
        UseMouseMoveOutCheckBox.IsChecked = Settings.Default.UseMouseMoveOut;
        UseHideThisAppHotKeyCheckBox.IsChecked = Settings.Default.UseHideThisAppHotKey;

        ModifierKey1ComboBox.SelectedItem = Settings.Default.ModifierKey1ComboBox;
        ModifierKey2ComboBox.SelectedItem = Settings.Default.ModifierKey2ComboBox;
        ModifierKey3ComboBox.SelectedItem = Settings.Default.ModifierKey3ComboBox;
        VirtualKeyComboBox.SelectedItem = Settings.Default.VirtualKeyComboBox;

        ThisModifierKey1ComboBox.SelectedItem = Settings.Default.ThisModifierKey1ComboBox;
        ThisModifierKey2ComboBox.SelectedItem = Settings.Default.ThisModifierKey2ComboBox;
        ThisModifierKey3ComboBox.SelectedItem = Settings.Default.ThisModifierKey3ComboBox;
        ThisVirtualKeyComboBox.SelectedItem = Settings.Default.ThisVirtualKeyComboBox;

        _targetAppList = Settings.Default.TargetAppList ?? [];
    }

    private void SaveSettings()
    {
        // 保存配置
        Settings.Default.ListInvisibleWindow = ListInvisibleWindowCheckBox.IsChecked ?? false;
        Settings.Default.ListUntitledWindow = ListUntitledWindowCheckBox.IsChecked ?? false;
        Settings.Default.ListUnableWindow = ListUnableWindowCheckBox.IsChecked ?? false;
        Settings.Default.UseHotKey = UseHotKeyCheckBox.IsChecked ?? false;
        Settings.Default.UseMiddleButtonDown = UseMiddleButtonDownCheckBox.IsChecked ?? false;
        Settings.Default.UseSimultaneousLeftRightDown = UseSimultaneousLeftRightDownCheckBox.IsChecked ?? false;
        Settings.Default.UseMouseMoveOut = UseMouseMoveOutCheckBox.IsChecked ?? false;
        Settings.Default.UseHideThisAppHotKey = UseHideThisAppHotKeyCheckBox.IsChecked ?? false;

        Settings.Default.ModifierKey1ComboBox = ModifierKey1ComboBox.SelectedItem?.ToString();
        Settings.Default.ModifierKey2ComboBox = ModifierKey2ComboBox.SelectedItem?.ToString();
        Settings.Default.ModifierKey3ComboBox = ModifierKey3ComboBox.SelectedItem?.ToString();
        Settings.Default.VirtualKeyComboBox = VirtualKeyComboBox.SelectedItem?.ToString();

        Settings.Default.ThisModifierKey1ComboBox = ThisModifierKey1ComboBox.SelectedItem?.ToString();
        Settings.Default.ThisModifierKey2ComboBox = ThisModifierKey2ComboBox.SelectedItem?.ToString();
        Settings.Default.ThisModifierKey3ComboBox = ThisModifierKey3ComboBox.SelectedItem?.ToString();
        Settings.Default.ThisVirtualKeyComboBox = ThisVirtualKeyComboBox.SelectedItem?.ToString();

        Settings.Default.TargetAppList = [];
        Settings.Default.TargetAppList.AddRange([.. _targetWidowList.Select(item => item.AppPath)]);

        // 保存设置
        Settings.Default.Save();
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.NavigateUri.AbsoluteUri,
                UseShellExecute = true
            });
        }
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        RefreshAllWindowsListView();
        // --- 重要: 注册热键的最佳时机 ---
        // 在 SourceInitialized 事件中注册，确保窗口句柄 (HWND) 已创建。
        // 不要 在构造函数中直接注册，那时句柄可能还不存在。
        _thisWindowItem = new(true, string.Empty, _windowHandle, 0, string.Empty);
        ModifierKeys modifierKey = ConvertStringToModifierKeys(ThisModifierKey1ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ThisModifierKey2ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ThisModifierKey3ComboBox.SelectedItem?.ToString()?? "None");
        Key key = ConvertStringToKey(ThisVirtualKeyComboBox.SelectedItem?.ToString() ?? "None");
        _hideThisAppHotKey = new GlobalHotKey(_windowHandle, 1, modifierKey, key);
        _hideThisAppHotKey.HotKeyPressed += _thisWindowItem.ToggleVisibility;
        if (UseHideThisAppHotKeyCheckBox.IsChecked ?? false)
            _hideThisAppHotKey.Register();

        modifierKey = ConvertStringToModifierKeys(ModifierKey1ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ModifierKey2ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ModifierKey3ComboBox.SelectedItem?.ToString() ?? "None");
        key = ConvertStringToKey(VirtualKeyComboBox.SelectedItem?.ToString() ?? "None");
        _hideWindowHotKey = new GlobalHotKey(_windowHandle, 2, modifierKey, key);
        _hideWindowHotKey.HotKeyPressed += ToogleWindowListVisibility;
        if (UseHotKeyCheckBox.IsChecked ?? false)
            _hideWindowHotKey.Register();
    }

    private static ModifierKeys ConvertStringToModifierKeys(string modifierKeyString)
    {
        if (Enum.TryParse(modifierKeyString, true, out ModifierKeys modifierKey))
        {
            return modifierKey;
        }
        else
        {
            throw new ArgumentException($"无法将字符串 '{modifierKeyString}' 转换为 ModifierKeys 枚举。");
        }
    }

    private static Key ConvertStringToKey(string virtualKeyString)
    {
        if (Enum.TryParse(virtualKeyString, true, out Key virtualKey))
        {
            return virtualKey;
        }
        else
        {
            throw new ArgumentException($"无法将字符串 '{virtualKeyString}' 转换为 Key 枚举。");
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow aboutWindow = new()
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        // 保存配置
        SaveSettings();
    }

    private void RefreshWindowListButton_Click(object sender, RoutedEventArgs e)
    { 
        RefreshAllWindowsListView();
        RefreshTargetWindowsListView();
    }

    private void AddWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = AllWindowsListView.SelectedItems.Cast<WindowItem>().ToList();
        foreach (var item in selectedItems)
        {
            if (!_targetWidowList.Any(w => w.Handle == item.Handle))
            {
                _targetWidowList.Add(item);
            }
        }
    }

    private void RemoveWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = TargetWindowsListView.SelectedItems.Cast<WindowItem>().ToList();
        foreach (var item in selectedItems)
        {
            _targetWidowList.Remove(item);
        }
    }

    private void RemoveAllWindowButton_Click(object sender, RoutedEventArgs e)
    {
        _targetWidowList.Clear();
    }

    private void RemoveTargetWindowMenuItem_Click(object sender, RoutedEventArgs e) =>
        RemoveWindowButton_Click(sender, e);

    private void ShowTargetWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = TargetWindowsListView.SelectedItems.Cast<WindowItem>().ToList();
        foreach (var item in selectedItems)
            item.Show();
    }

    private void HideTargetWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = TargetWindowsListView.SelectedItems.Cast<WindowItem>().ToList();
        foreach (var item in selectedItems)
            item.Hide();
    }

    private void AddWindowMenuItem_Click(object sender, RoutedEventArgs e) =>
        AddWindowButton_Click(sender, e);

    private void UseHotKeyCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_hideWindowHotKey?.Register() ?? false)
        {
            MessageBox.Show("热键已被占用！");
        }
    }

    private void UseHotKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        _hideWindowHotKey?.Unregister();
    }

    private void UseMiddleButtonDownCheckBox_Checked(object sender, RoutedEventArgs e) =>
        _mouseHook.MiddleButtonDown += ToogleWindowListVisibility;
    private void UseMiddleButtonDownCheckBox_Unchecked(object sender, RoutedEventArgs e) =>
        _mouseHook.MiddleButtonDown -= ToogleWindowListVisibility;


    private void UseSimultaneousLeftRightDownCheckBox_Checked(object sender, RoutedEventArgs e) =>
        _mouseHook.SimultaneousLeftRightDown += ToogleWindowListVisibility;

    private void UseSimultaneousLeftRightDownCheckBox_Unchecked(object sender, RoutedEventArgs e) =>
        _mouseHook.SimultaneousLeftRightDown -= ToogleWindowListVisibility;

    private void UseMouseMoveOutCheckBox_Checked(object sender, RoutedEventArgs e) =>
        _mouseHook.MouseMove += ToogleByMousePosition;
    private void UseMouseMoveOutCheckBox_Unchecked(object sender, RoutedEventArgs e) =>
        _mouseHook.MouseMove -= ToogleByMousePosition;


    private void UseHideThisAppHotKeyCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_hideThisAppHotKey?.Register() ?? false)
        {
            MessageBox.Show("热键已被占用！");
        }
    }

    private void UseHideThisAppHotKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        _hideThisAppHotKey?.Unregister();
    }

    private void HideWindowHotKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ModifierKeys modifierKey = ConvertStringToModifierKeys(ModifierKey1ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ModifierKey2ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ModifierKey3ComboBox.SelectedItem?.ToString() ?? "None");
        Key key = ConvertStringToKey(VirtualKeyComboBox.SelectedItem?.ToString() ?? "None");
        _hideWindowHotKey?.ChangeHotKey(modifierKey, key);
        if (UseHotKeyCheckBox.IsChecked ?? false)
        {
            _hideWindowHotKey?.Unregister();
            if (!_hideWindowHotKey?.Register() ?? false)
            {
                MessageBox.Show("热键已被占用！");
            }
        }
    }

    private void HideThisAppHotKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ModifierKeys modifierKey = ConvertStringToModifierKeys(ThisModifierKey1ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ThisModifierKey2ComboBox.SelectedItem?.ToString() ?? "None") | ConvertStringToModifierKeys(ThisModifierKey3ComboBox.SelectedItem?.ToString() ?? "None");
         Key key = ConvertStringToKey(ThisVirtualKeyComboBox.SelectedItem?.ToString() ?? "None");
        _hideThisAppHotKey?.ChangeHotKey(modifierKey, key);
        if (UseHideThisAppHotKeyCheckBox.IsChecked ?? false)
        {
            _hideThisAppHotKey?.Unregister();
            if(!_hideThisAppHotKey?.Register() ?? false)
            {
                MessageBox.Show("热键已被占用！");
            }
        }
    }
}