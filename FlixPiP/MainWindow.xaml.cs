using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace FlixPiP
{
    public partial class MainWindow : Window
    {
        // Win32 API 宣言
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y,
            int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr Hwnd;
            public IntPtr HwndInsertAfter;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public uint Flags;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const uint LWA_ALPHA = 0x2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int VK_RBUTTON = 0x02;
        private const int WebViewInitializationAttempts = 3;

        private const string RightDragScript = """
            (() => {
                if (window.__flixPiPRightDragInstalled) return;
                window.__flixPiPRightDragInstalled = true;

                let dragging = false;
                let pointerId = null;

                document.addEventListener('contextmenu', event => {
                    event.preventDefault();
                }, true);

                document.addEventListener('pointerdown', event => {
                    if (event.button !== 2) return;
                    dragging = true;
                    pointerId = event.pointerId;
                    event.target.setPointerCapture?.(event.pointerId);
                    window.chrome.webview.postMessage('flixpip:right-drag:start');
                    event.preventDefault();
                }, true);

                document.addEventListener('pointermove', event => {
                    if (!dragging || event.pointerId !== pointerId) return;
                    window.chrome.webview.postMessage('flixpip:right-drag:move');
                    event.preventDefault();
                }, true);

                const endDrag = event => {
                    if (!dragging || (event.pointerId !== undefined && event.pointerId !== pointerId)) return;
                    dragging = false;
                    pointerId = null;
                    window.chrome.webview.postMessage('flixpip:right-drag:end');
                    event.preventDefault();
                };

                document.addEventListener('pointerup', endDrag, true);
                document.addEventListener('pointercancel', endDrag, true);
                window.addEventListener('blur', () => {
                    if (!dragging) return;
                    dragging = false;
                    pointerId = null;
                    window.chrome.webview.postMessage('flixpip:right-drag:end');
                });
            })();
            """;

        // ホットキー用の定数
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const uint VK_UP = 0x26;
        private const uint VK_DOWN = 0x28;
        private const uint VK_LEFT = 0x25;
        private const uint VK_RIGHT = 0x27;

        // ホットキーID定義
        private const int HK_MODE_OP = 9001;   // Ctrl + ↑ (操作モード)
        private const int HK_MODE_GAME = 9002; // Ctrl + ↓ (すり抜けモード)
        private const int HK_CLOSE = 9003;     // Ctrl + → (アプリ終了)
        private const int HK_NAV_GOOGLE = 9004;// Ctrl + ← (Googleを開く)

        private IntPtr _windowHandle;
        private HwndSource? _windowSource;
        private bool _isRightDragging;
        private POINT _dragStartCursor;
        private RECT _dragStartWindow;
        private int _initialStyle;
        private bool _isWebViewConfigured;
        private bool _isClosing;

        private Window? _currentOpacityWindow;
        private System.Windows.Threading.DispatcherTimer? _opacityTimer;
        private byte _currentOpacity = 127; // 透明度の設定 （初期値50%）

        public int Bookmarknumber = 0; // 現在のブックマーク番号

        private Window? _ShowURLWindow;
        private System.Windows.Threading.DispatcherTimer? _ShowURLTimer;
        private SettingWindow? _settingWindow;

        public MainWindow()
        {
            InitializeComponent();

            LoadWindowSize();

            Loaded += MainWindow_Loaded;

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            while (!_isClosing)
            {
                if (await TryInitializeWebViewAsync())
                    return;

                MessageBoxResult result = MessageBox.Show(
                    this,
                    "WebView2を初期化できませんでした。\n\nMicrosoft Edge WebView2 Runtimeのインストール状態や、空き容量、アクセス権限を確認してください。\n\n再試行しますか？",
                    "ブラウザー初期化エラー",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes);

                if (result != MessageBoxResult.Yes)
                {
                    Close();
                    return;
                }
            }
        }

        private async Task<bool> TryInitializeWebViewAsync()
        {
            for (int attempt = 1; attempt <= WebViewInitializationAttempts && !_isClosing; attempt++)
            {
                try
                {
                    await webView.EnsureCoreWebView2Async();
                    if (webView.CoreWebView2 == null)
                        throw new InvalidOperationException("WebView2 initialization completed without a CoreWebView2 instance.");

                    await ConfigureWebViewAsync(webView.CoreWebView2);
                    webView.Source = new Uri("https://google.com");
                    return true;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Debug.WriteLine($"WebView2 initialization attempt {attempt} failed: {exception}");
                    if (attempt < WebViewInitializationAttempts && !_isClosing)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                    }
                }
            }

            return false;
        }

        private async Task ConfigureWebViewAsync(CoreWebView2 coreWebView)
        {
            if (_isWebViewConfigured)
                return;

            coreWebView.Settings.AreDefaultContextMenusEnabled = false;
            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(RightDragScript);
            coreWebView.WebMessageReceived += WebView_WebMessageReceived;
            coreWebView.NavigationStarting += WebView_NavigationStarting;
            coreWebView.NewWindowRequested += WebView_NewWindowRequested;
            _isWebViewConfigured = true;
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (IsAllowedWebUrl(e.Uri))
                return;

            e.Cancel = true;
            Debug.WriteLine($"Blocked navigation to disallowed URI: {e.Uri}");
        }

        private static bool IsAllowedWebUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return false;

            if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return true;

            return Properties.Settings.Default.AllowHttp
                && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        }

        private void WebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (!IsAllowedWebUrl(e.Uri) || webView.CoreWebView2 == null)
            {
                Debug.WriteLine($"Blocked new window request to disallowed URI: {e.Uri}");
                return;
            }

            // ポップアップを作らず、許可したURLを現在のWebViewで開く。
            webView.CoreWebView2.Navigate(e.Uri);
        }

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message;
            try
            {
                message = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                return;
            }

            switch (message)
            {
                case "flixpip:right-drag:start":
                    StartRightDrag();
                    break;
                case "flixpip:right-drag:move":
                    ContinueRightDrag();
                    break;
                case "flixpip:right-drag:end":
                    _isRightDragging = false;
                    break;
            }
        }

        private void StartRightDrag()
        {
            if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) == 0
                || !GetCursorPos(out _dragStartCursor)
                || !GetWindowRect(_windowHandle, out _dragStartWindow))
            {
                _isRightDragging = false;
                return;
            }

            _isRightDragging = true;
        }

        private void ContinueRightDrag()
        {
            if (!_isRightDragging)
                return;

            if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) == 0 || !GetCursorPos(out POINT cursor))
            {
                _isRightDragging = false;
                return;
            }

            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                _dragStartWindow.Left + cursor.X - _dragStartCursor.X,
                _dragStartWindow.Top + cursor.Y - _dragStartCursor.Y,
                0,
                0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            _windowSource = HwndSource.FromHwnd(_windowHandle);
            _windowSource?.AddHook(WindowMessageHook);
            _initialStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);

            RestoreWindowPosition();

            // 操作可能状態
            SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED);
            SetLayeredWindowAttributes(_windowHandle, 0, 255, LWA_ALPHA);

            // グローバルホットキー登録
            RegisterHotKey(_windowHandle, 9001, MOD_CONTROL, VK_UP);
            RegisterHotKey(_windowHandle, 9002, MOD_CONTROL, VK_DOWN);
            RegisterHotKey(_windowHandle, 9003, MOD_CONTROL, 0x27); // 0x27 = 矢印右 (Ctrl + →)
            RegisterHotKey(_windowHandle, 9004, MOD_CONTROL, 0x25); // 0x25 = 矢印左 (Ctrl + ←)

            // Alt + 矢印のホットキー登録（透明度変更用）
            RegisterHotKey(_windowHandle, 9005, MOD_SHIFT, VK_UP);    // Shift
            RegisterHotKey(_windowHandle, 9006, MOD_SHIFT, VK_DOWN);  // Shift
            RegisterHotKey(_windowHandle, 9007, MOD_SHIFT, VK_LEFT);  // Shift
            RegisterHotKey(_windowHandle, 9008, MOD_SHIFT, VK_RIGHT);  // Shift

            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;
            SaveWindowPosition();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            PreviewKeyDown -= MainWindow_PreviewKeyDown;
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcher_ThreadFilterMessage;

            for (int hotKeyId = 9001; hotKeyId <= 9008; hotKeyId++)
            {
                UnregisterHotKey(_windowHandle, hotKeyId);
            }

            _opacityTimer?.Stop();
            _ShowURLTimer?.Stop();
            _currentOpacityWindow?.Close();
            _ShowURLWindow?.Close();
            _opacityTimer = null;
            _ShowURLTimer = null;
            _currentOpacityWindow = null;
            _ShowURLWindow = null;

            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
                webView.CoreWebView2.NavigationStarting -= WebView_NavigationStarting;
                webView.CoreWebView2.NewWindowRequested -= WebView_NewWindowRequested;
            }

            if (_windowSource != null)
            {
                _windowSource.RemoveHook(WindowMessageHook);
            }

            base.OnClosed(e);
        }

        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WM_WINDOWPOSCHANGING || lParam == IntPtr.Zero)
                return IntPtr.Zero;

            var position = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if (!GetWindowRect(hwnd, out RECT currentBounds))
                return IntPtr.Zero;

            int width = (position.Flags & SWP_NOSIZE) != 0
                ? currentBounds.Right - currentBounds.Left
                : position.Width;
            int height = (position.Flags & SWP_NOSIZE) != 0
                ? currentBounds.Bottom - currentBounds.Top
                : position.Height;
            int x = (position.Flags & SWP_NOMOVE) != 0 ? currentBounds.Left : position.X;
            int y = (position.Flags & SWP_NOMOVE) != 0 ? currentBounds.Top : position.Y;

            var proposedBounds = new RECT
            {
                Left = x,
                Top = y,
                Right = x + width,
                Bottom = y + height
            };
            IntPtr monitor = MonitorFromRect(ref proposedBounds, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
                return IntPtr.Zero;

            int workWidth = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
            int workHeight = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
            width = Math.Min(width, workWidth);
            height = Math.Min(height, workHeight);
            x = Math.Clamp(x, monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Right - width);
            y = Math.Clamp(y, monitorInfo.WorkArea.Top, monitorInfo.WorkArea.Bottom - height);

            position.X = x;
            position.Y = y;
            position.Width = width;
            position.Height = height;
            position.Flags &= ~(SWP_NOMOVE | SWP_NOSIZE);
            Marshal.StructureToPtr(position, lParam, false);
            return IntPtr.Zero;
        }

        private void RestoreWindowPosition()
        {
            if (!Properties.Settings.Default.HasWindowPosition || !GetWindowRect(_windowHandle, out RECT bounds))
                return;

            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                Properties.Settings.Default.WindowLeft,
                Properties.Settings.Default.WindowTop,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private void SaveWindowPosition()
        {
            if (_windowHandle == IntPtr.Zero || !GetWindowRect(_windowHandle, out RECT bounds))
                return;

            Properties.Settings.Default.WindowLeft = bounds.Left;
            Properties.Settings.Default.WindowTop = bounds.Top;
            Properties.Settings.Default.HasWindowPosition = true;
            Properties.Settings.Default.Save();
        }

        // ホットキーを処理
        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg.message == WM_HOTKEY)
            {
                int id = msg.wParam.ToInt32();
                if (id == HK_MODE_OP)
                {
                    // 操作モード
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED);
                    SetLayeredWindowAttributes(_windowHandle, 0, 255, LWA_ALPHA);
                    this.Focus(); // キー移動を受け付けるためにフォーカスを当てる
                    handled = true;
                }
                else if (id == HK_MODE_GAME)
                {
                    // すり抜けモード
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                    SetLayeredWindowAttributes(_windowHandle, 0, _currentOpacity, LWA_ALPHA);
                    handled = true;
                }
                else if (id == HK_CLOSE)
                {
                    if (MessageBox.Show("FlixPiPを終了しますか？",
                                        "確認画面",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        this.Close();
                    }
                }
                else if (id == HK_NAV_GOOGLE)
                {
                    // WebView2のページをGoogleに切り替える
                    webView.CoreWebView2?.Navigate("https://www.google.com");
                    handled = true;
                }
                else if (id == 9005)
                {
                    // Shift + ↑ で透明度UP
                    _currentOpacity = (byte)Math.Min(255, _currentOpacity + 15);
                    SetLayeredWindowAttributes(_windowHandle, 0, _currentOpacity, LWA_ALPHA);
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                    ShowTemporaryOpacityDisplay();
                    handled = true;
                }
                else if (id == 9006)
                {
                    // Shift + ↓ で透明度DOWN
                    _currentOpacity = (byte)Math.Max(0, _currentOpacity - 15);
                    SetLayeredWindowAttributes(_windowHandle, 0, _currentOpacity, LWA_ALPHA);
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                    ShowTemporaryOpacityDisplay();
                    handled = true;
                }
                else if (id == 9007) // 設定画面を開く
                {
                    OpenSettingWindow();
                }
            }
        }

        // 数字キーによるブックマーク呼び出し
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (!isShiftPressed)
                return;

            int bookmarkIndex = e.Key switch
            {
                Key.D1 => 0,
                Key.D2 => 1,
                Key.D3 => 2,
                Key.D4 => 3,
                Key.D5 => 4,
                Key.D6 => 5,
                Key.D7 => 6,
                Key.D8 => 7,
                Key.D9 => 8,
                _ => -1
            };

            if (bookmarkIndex < 0)
                return;

            LoadCurrentPageFromBookmarks(bookmarkIndex);
            e.Handled = true;
        }


        private void ShowTemporaryOpacityDisplay()
        {
            // 既存のタイマーがあれば停止
            if (_opacityTimer != null)
            {
                _opacityTimer?.Stop();
            }

            // 既存のウィンドウがあれば閉じる
            if (_currentOpacityWindow != null)
            {
                _currentOpacityWindow.Close();
            }

            double percentageOpacity = Math.Round((_currentOpacity / 255.0) * 100);
            _currentOpacityWindow = new Window
            {
                Title = "透明度",
                Content = new TextBlock
                {
                    Text = $"透明度: {percentageOpacity}%",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = System.Windows.TextAlignment.Center,
                    Padding = new Thickness(30)
                },
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0)),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            _currentOpacityWindow.Show();

            // 2秒後に自動で閉じる
            _opacityTimer = new System.Windows.Threading.DispatcherTimer();
            _opacityTimer.Interval = TimeSpan.FromSeconds(2);
            _opacityTimer.Tick += (s, e) =>
            {
                _opacityTimer?.Stop();
                if (_currentOpacityWindow != null)
                {
                    _currentOpacityWindow.Close();
                    _currentOpacityWindow = null;
                }
            };
            _opacityTimer.Start();
        }
        // 現在のURLをブックマークする
        private void AddCurrentPageToBookmarks()
        {
            var url = webView?.Source?.ToString();
            if (!string.IsNullOrEmpty(url)) BookmarkManager.AddBookmark(url);
        }
        // ブックマークをロードする
        private void LoadCurrentPageFromBookmarks(int number)
        {
            var bookmarks = BookmarkManager.LoadBookmarks()?.ToList() ?? new List<string>();
            if (number >= 0 && number < bookmarks.Count)
            {
                var url = bookmarks[number];
                if (!string.IsNullOrEmpty(url) && webView?.CoreWebView2 != null)
                {
                    if (!IsAllowedWebUrl(url))
                    {
                        MessageBox.Show($"設定で許可されていないURLは開けません: {url}");
                        Debug.WriteLine($"Disallowed URL in bookmarks: {url}");
                        return;
                    }
                    webView.CoreWebView2.Navigate(url);
                    Debug.WriteLine($"Navigated to bookmark[{number}]: {url}");
                    showurl(url);
                }
            }
            else
            {
                MessageBox.Show($"その番号にブックマークは存在しません: {number + 1} (登録されている数={bookmarks.Count})");
                Debug.WriteLine($"Bookmark index out of range: {number} (count={bookmarks.Count})");
            }
        }
        private void showurl(string url)
        {

            // 既存のタイマーがあれば停止
            if (_ShowURLTimer != null)
            {
                _ShowURLTimer?.Stop();
            }

            // 既存のウィンドウがあれば閉じる
            if (_ShowURLWindow != null)
            {
                _ShowURLWindow.Close();
            }
            _ShowURLWindow = new Window
            {
                Title = "ブックマーク",
                Content = new TextBlock
                {
                    Text = $"{url}を開きました",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = System.Windows.TextAlignment.Center,
                    Padding = new Thickness(30)
                },
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0)),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            _ShowURLWindow.Show();

            // 2秒後に自動で閉じる
            _ShowURLTimer = new System.Windows.Threading.DispatcherTimer();
            _ShowURLTimer.Interval = TimeSpan.FromSeconds(2);
            _ShowURLTimer.Tick += (s, e) =>
            {
                _ShowURLTimer?.Stop();
                if (_ShowURLWindow != null)
                {
                    _ShowURLWindow.Close();
                    _ShowURLWindow = null;
                }
            };
            _ShowURLTimer.Start();
        }

        private void OpenSettingWindow()
        {
            if (_settingWindow != null)
            {
                if (_settingWindow.WindowState == WindowState.Minimized)
                {
                    _settingWindow.WindowState = WindowState.Normal;
                }

                _settingWindow.Activate();
                return;
            }

            _settingWindow = new SettingWindow
            {
                Owner = this
            };

            try
            {
                _settingWindow.ShowDialog();
            }
            finally
            {
                _settingWindow = null;
            }
        }

        private void LoadWindowSize()
        {
            Height = WindowsSize.LoadHeight();
            Width = WindowsSize.LoadWidth();
        }
    }
}
