using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WindowToggler
{
    class WindowItem(bool isVisible, string title, IntPtr handle, int pid, string appPath) : INotifyPropertyChanged
    {
        private static class NativeMethods
        {
            [DllImport("user32.DLL")]
            public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

            private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            public const uint SW_HIDE = 0;
            public const uint SW_SHOWNORMAL = 1;
            public const uint SW_SHOWMINIMIZED = 2;
        }

        private string _title = title;
        public string IsVisibleText => IsVisible ? "✔" : "❌";
        public bool IsVisible { get; set; } = isVisible;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        public IntPtr Handle { get; set; } = handle;
        public int PID { get; set; } = pid;
        public string AppPath { get; set; } = appPath;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null!)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        // 切换窗口可见性
        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
                IsVisible = false;
            }
            else
            {
                Show();
                IsVisible = true;
            }
        }
        // 显示窗口
        public void Show()
        {
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOWNORMAL);
            IsVisible = true;
        }
        // 隐藏窗口
        public void Hide()
        {
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
            IsVisible = false;
        }

        public Rect GetWindowRect()
        {
            NativeMethods.GetWindowRect(Handle, out NativeMethods.RECT rect);
            return new Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}
