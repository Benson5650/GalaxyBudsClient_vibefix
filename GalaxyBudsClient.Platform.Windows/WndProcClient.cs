using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace GalaxyBudsClient.Platform.Windows
{
    // PostMessage is needed by WndProcClient.Invoke() to wake the message loop from other threads.
    internal static class User32PostMessage
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }

    public partial class WndProcClient : IDisposable
    {
        public event EventHandler<WindowMessage>? MessageReceived;

        private readonly Thread _thread;
        private readonly TaskCompletionSource<IntPtr> _hwndTcs = new();
        private readonly CancellationTokenSource _cts = new();

        // Must call WindowHandle only after constructor returns (blocks until HWND is ready).
        public IntPtr WindowHandle => _hwndTcs.Task.Result;

        // Work queue: actions that must run on the HWND's owner thread.
        private readonly Queue<(Action action, ManualResetEventSlim done, ExceptionDispatchInfoHolder? error)> _workQueue = new();

        private sealed class ExceptionDispatchInfoHolder
        {
            public System.Runtime.ExceptionServices.ExceptionDispatchInfo? Info;
        }

        /// <summary>
        /// Marshals <paramref name="action"/> to the HWND-owner thread and blocks until it completes.
        /// Required for any Win32 call that requires thread affinity (e.g. RegisterHotKey, UnregisterHotKey).
        /// </summary>
        public void Invoke(Action action)
        {
            if (Thread.CurrentThread == _thread)
            {
                action();
                return;
            }

            var done = new ManualResetEventSlim(false);
            var errorHolder = new ExceptionDispatchInfoHolder();

            lock (_workQueue)
            {
                _workQueue.Enqueue((action, done, errorHolder));
            }

            // Wake the message-loop thread by posting a WM_APP message.
            User32PostMessage.PostMessage(_hwndTcs.Task.Result, (uint)WindowsMessage.WM_APP, IntPtr.Zero, IntPtr.Zero);
            done.Wait();

            errorHolder.Info?.Throw();
        }

        public WndProcClient()
        {
            _thread = new Thread(MessageThreadProc)
            {
                IsBackground = true,
                Name = "WndProcClient Thread"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            // Block until HWND is created (propagates any init exception).
            _hwndTcs.Task.GetAwaiter().GetResult();
        }

        private void MessageThreadProc()
        {
            // --- Create HWND on this thread ---
            var wndClassEx = new Unmanaged.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<Unmanaged.WNDCLASSEX>(),
                lpfnWndProc = WndProcCallback,
                hInstance = Unmanaged.GetModuleHandle(null),
                lpszClassName = "MessageWindow " + Guid.NewGuid(),
            };

            ushort atom = Unmanaged.RegisterClassEx(ref wndClassEx);
            if (atom == 0)
            {
                Log.Error("Interop.Win32.WndProcClient: atom is null");
                _hwndTcs.SetException(new Win32Exception());
                return;
            }

            var hwnd = Unmanaged.CreateWindowEx(0, atom, null, 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, Unmanaged.GetModuleHandle(null), IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
            {
                Log.Error("Interop.Win32.WndProcClient: hWnd is null");
                _hwndTcs.SetException(new Win32Exception());
                return;
            }

            _hwndTcs.SetResult(hwnd);

            // --- Message loop ---
            while (!_cts.IsCancellationRequested)
            {
                var result = Unmanaged.GetMessage(out var msg, IntPtr.Zero, 0, 0);
                if (result == 0)
                    break; // WM_QUIT

                if (result < 0)
                {
                    Log.Error("WndProcClient: Unmanaged error in message loop. Error Code: {Code}",
                        Marshal.GetLastWin32Error());
                    break;
                }

                // Drain work-queue items posted via Invoke().
                if (msg.message == (uint)WindowsMessage.WM_APP)
                {
                    DrainWorkQueue();
                    continue;
                }

                Unmanaged.TranslateMessage(ref msg);
                Unmanaged.DispatchMessage(ref msg);
            }

            // Final drain so callers blocked in Invoke() don't hang.
            DrainWorkQueue();
        }

        private void DrainWorkQueue()
        {
            while (true)
            {
                (Action action, ManualResetEventSlim done, ExceptionDispatchInfoHolder? errorHolder) item;
                lock (_workQueue)
                {
                    if (_workQueue.Count == 0) break;
                    item = _workQueue.Dequeue();
                }
                try
                {
                    item.action();
                }
                catch (Exception ex)
                {
                    if (item.errorHolder != null)
                        item.errorHolder.Info = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    item.done.Set();
                }
            }
        }

        private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var message = new WindowMessage
            {
                hWnd = hWnd,
                Msg = (WindowsMessage)msg,
                wParam = wParam,
                lParam = lParam
            };
            MessageReceived?.Invoke(this, message);
            return Unmanaged.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            _cts.Cancel();
            // Post WM_QUIT to break the message loop.
            if (_hwndTcs.Task.IsCompletedSuccessfully)
                User32PostMessage.PostMessage(_hwndTcs.Task.Result, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
    }
}