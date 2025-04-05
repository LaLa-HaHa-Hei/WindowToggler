using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowToggler
{
    /// <summary>
    /// 每个程序最好只有一个全局鼠标钩子，所以为单例模式（不可继承）
    /// </summary>
    public sealed class GlobalMouseHook : IDisposable
    {
        // 线程安全的懒汉式初始化
        private static readonly Lazy<GlobalMouseHook> _lazyInstance =
            new(() => new GlobalMouseHook());

        // 公共静态属性提供全局访问点
        public static GlobalMouseHook Instance => _lazyInstance.Value;

        private static class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public POINT pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            // 钩子相关委托和常量
            public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
            public const int WH_MOUSE_LL = 14;
            public const int WM_LBUTTONDOWN = 0x0201;
            public const int WM_LBUTTONUP = 0x0202;
            public const int WM_RBUTTONDOWN = 0x0204;
            public const int WM_RBUTTONUP = 0x0205;
            public const int WM_MBUTTONDOWN = 0x0207;
            public const int WM_MBUTTONUP = 0x0208;
            public const int WM_MOUSEMOVE = 0x0200;
        }
        public class MouseMoveEventArgs(int x, int y) : EventArgs
        {
            public int X { get; } = x;
            public int Y { get; } = y;
        }

        private readonly NativeMethods.LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _disposed = false;

        // 用于检测左右键同时按下的状态
        private bool _isLeftButtonDown = false;
        private bool _isRightButtonDown = false;
        private readonly object _lockObject = new(); // 用于同步状态访问

        // 定义事件
        public event EventHandler<MouseMoveEventArgs>? MouseMove; // 可以传递 Point 参数: EventHandler<Point>
        public event Action? MiddleButtonDown; // 鼠标中间按下
        public event Action? SimultaneousLeftRightDown; // 左右键同时按下

        private GlobalMouseHook()
        {
            _proc = HookCallback;
            InstallHook();
        }

        // 安装钩子
        private void InstallHook()
        {
            // 防止重复安装（虽然单例模式下理论上不会，但增加健壮性）
            if (_hookID != IntPtr.Zero) 
                return;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule ?? throw new InvalidOperationException("Main module not found."))
            _hookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);

            if (_hookID == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(errorCode, "Failed to set mouse hook.");
            }
        }

        // 卸载钩子
        private void UninstallHook()
        {
            if (_hookID == IntPtr.Zero)
                return;
            NativeMethods.UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }

        // 钩子回调方法
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) // HC_ACTION
            {
                // 从 lParam 获取鼠标坐标等信息（如果需要）
                var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int cursorX = ms.pt.x;
                int cursorY = ms.pt.y;

                // 使用 lock 保护状态变量的读写
                lock (_lockObject)
                {
                    switch ((int)wParam)
                    {
                        case NativeMethods.WM_MOUSEMOVE:
                            MouseMove?.Invoke(this, new MouseMoveEventArgs(cursorX, cursorY));
                            break;

                        case NativeMethods.WM_MBUTTONDOWN:
                            MiddleButtonDown?.Invoke();
                            break;

                        case NativeMethods.WM_LBUTTONDOWN:
                            _isLeftButtonDown = true;
                            break;

                        case NativeMethods.WM_RBUTTONDOWN:
                            _isRightButtonDown = true;
                            break;

                        case NativeMethods.WM_LBUTTONUP:
                            _isLeftButtonDown = false;
                            break;

                        case NativeMethods.WM_RBUTTONUP:
                            _isRightButtonDown = false;
                            break;
                    }

                    // 在 lock 块外部触发事件，避免长时间持有锁
                    if (_isLeftButtonDown && _isRightButtonDown)
                    {
                        SimultaneousLeftRightDown?.Invoke();
                        // 触发后是否需要重置状态？取决于具体需求
                         _isLeftButtonDown = false; // 例如，如果只触发一次
                         _isRightButtonDown = false;
                    }
                }
            }

            // 必须调用 CallNextHookEx，否则其他程序或系统将无法接收到这些消息
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 通知GC不需要再调用Finalize
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) 
                return;
            //if (disposing)
            //{
            //    // 释放托管资源 (如果有)
            //    // 例如取消事件订阅等
            //}

            // 释放非托管资源 (钩子句柄)
            UninstallHook();

            _disposed = true;
        }

    }
}
