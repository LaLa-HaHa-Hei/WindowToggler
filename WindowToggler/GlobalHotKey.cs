using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowToggler // 请替换成你的项目命名空间
{
    /// <summary>
    /// 表示系统范围的全局热键注册。
    /// 实现IDisposable以确保取消注册。
    /// </summary>
    public sealed class GlobalHotKey : IDisposable
    {
        private static class NativeMethods
        {
            public const uint MOD_NONE = 0x0000;
            public const uint MOD_ALT = 0x0001;
            public const uint MOD_CONTROL = 0x0002;
            public const uint MOD_SHIFT = 0x0004;
            public const uint MOD_WIN = 0x0008;
            public const uint MOD_NOREPEAT = 0x4000; // 防止在按住键时重复生成热键消息。

            // 消息标识符
            public const uint WM_HOTKEY = 0x0312;

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        }

        public ModifierKeys Modifier { get; private set; }
        public Key Key { get; private set; }
        public bool IsRegistered { get; private set; }
        public int Id { get; private set; }
        public event Action? HotKeyPressed;

        private bool _disposed = false;
        private IntPtr _windowHandle;


        public GlobalHotKey(IntPtr windowHandle, int id, ModifierKeys modifier, Key key)
        {
            if (key == Key.None)
                throw new ArgumentException("Key不能为Key.None。", nameof(key));

            _windowHandle = windowHandle;
            Id = id;
            Modifier = modifier;
            Key = key;
            HwndSource hwndSource = HwndSource.FromHwnd(_windowHandle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
            else
            {
                throw new InvalidOperationException("无法获取窗口的 HwndSource 对象。");
            }
            // 注册通过Register()方法进行。
        }

        // 修改热键组合
        public void ChangeHotKey(ModifierKeys modifier, Key key)
        {
            if (key == Key.None)
                throw new ArgumentException("Key不能为Key.None。", nameof(key));
            Modifier = modifier;
            Key = key;
        }

        public bool Register()
        {
            if (IsRegistered)
                return false;

            uint fsModifiers = ConvertModifiers(Modifier);
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(Key);
            if (!NativeMethods.RegisterHotKey(_windowHandle, Id, fsModifiers, vk))
            {
                //int errorCode = Marshal.GetLastWin32Error();
                //Debug.WriteLine($"注册热键失败({Modifier} + {Key})。Win32错误代码：{errorCode}");
                // 如果需要，可以考虑在此处抛出Win32Exception：
                // throw new Win32Exception(errorCode, $"注册热键失败({_modifier} + {_key})。");
                return false;
            }
            IsRegistered = true;
            return true;
        }

        public bool Unregister()
        {
            if (!IsRegistered)
                return false; 

            if (!NativeMethods.UnregisterHotKey(_windowHandle, Id))
            {
                //记录错误，但通常不在此处抛出异常
                //int errorCode = Marshal.GetLastWin32Error();
                //Debug.WriteLine($"取消注册热键ID {Id}失败。Win32错误代码：{errorCode}");
                return false;
            }
            IsRegistered = false;
            return true;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id)
            {
                HotKeyPressed?.Invoke();
                handled = true;
            }
            else
            {
                handled = false;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 将WPF ModifierKeys转换为Win32虚拟键修饰符标志。
        /// </summary>
        private static uint ConvertModifiers(ModifierKeys modifiers)
        {
            uint fsModifiers = NativeMethods.MOD_NOREPEAT; // 默认添加NOREPEAT
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                fsModifiers |= NativeMethods.MOD_ALT;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                fsModifiers |= NativeMethods.MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                fsModifiers |= NativeMethods.MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                fsModifiers |= NativeMethods.MOD_WIN;
            return fsModifiers;
        }

        /// <summary>
        /// 释放GlobalHotkey实例使用的资源。
        /// 确保热键已取消注册。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                //if (disposing)
                //{
                //    // 释放托管状态（托管对象）。
                //}

            // 释放非托管资源
            if (IsRegistered)
            {
                Unregister();
            }
            _disposed = true;
            }
        }
    }
}