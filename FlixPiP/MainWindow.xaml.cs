using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

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
        private int _initialStyle;

        private Window _currentOpacityWindow;
        private System.Windows.Threading.DispatcherTimer _opacityTimer;
        private byte _currentOpacity = 127; // 透明度の設定 （初期値50%）

        public int Bookmarknumber = 0; // 現在のブックマーク番号

        private Window _ShowURLWindow;
        private System.Windows.Threading.DispatcherTimer _ShowURLTimer;

        public MainWindow()
        {
            InitializeComponent();

            LoadWindowSize();

            webView.EnsureCoreWebView2Async();
            webView.Source = new Uri("https://google.com");

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            webView.NavigationCompleted += WebView_NavigationCompleted;
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
            SaveWindowPosition();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
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
                    webView.CoreWebView2.Navigate("https://www.google.com");
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

        // 矢印キーによる移動とサイズ変更の処理
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            double moveStep = 15; // 1回に動くピクセル数
            double resizeStep = 20;

            // Altキーが押されているかどうか
            bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (isShiftPressed)
            {
                if (e.Key == Key.D1)
                {
                    LoadCurrentPageFromBookmarks(0);
                }
                else if (e.Key == Key.D2)
                {
                    LoadCurrentPageFromBookmarks(1);
                }
                else if (e.Key == Key.D3)
                {
                    LoadCurrentPageFromBookmarks(2);
                }
                else if (e.Key == Key.D4)
                {
                    LoadCurrentPageFromBookmarks(3);
                }
                else if (e.Key == Key.D5)
                {
                    LoadCurrentPageFromBookmarks(4);
                }
                else if (e.Key == Key.D6)
                {
                    LoadCurrentPageFromBookmarks(5);
                }
                else if (e.Key == Key.D7)
                {
                    LoadCurrentPageFromBookmarks(6);
                }
                else if (e.Key == Key.D8)
                {
                    LoadCurrentPageFromBookmarks(7);
                }
                else if (e.Key == Key.D9)
                {
                    LoadCurrentPageFromBookmarks(8);
                }
                e.Handled = true;
            }
            else if (isAltPressed)
            {
                // サイズ変更モード：Alt + 矢印
                if (e.Key == Key.Left) this.Width = Math.Max(200, this.Width - resizeStep);
                if (e.Key == Key.Right) this.Width = Math.Min(1920, this.Width + resizeStep);
                if (e.Key == Key.Up) this.Height = Math.Max(150, this.Height - resizeStep);
                if (e.Key == Key.Down) this.Height = Math.Min(1080, this.Height + resizeStep);
                e.Handled = true;
            }
            else
            {
                // 位置移動モード：矢印のみ
                if (e.Key == Key.Left) this.Left -= moveStep;
                if (e.Key == Key.Right) this.Left += moveStep;
                if (e.Key == Key.Up) this.Top -= moveStep;
                if (e.Key == Key.Down) this.Top += moveStep;

                // 矢印入力をブラウザ側に奪われないようにブロックする
                if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
                {
                    e.Handled = true;
                }
            }
        }


        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            await webView.EnsureCoreWebView2Async();
        }

        private void ShowTemporaryOpacityDisplay()
        {
            // 既存のタイマーがあれば停止
            if (_opacityTimer != null)
            {
                _opacityTimer.Stop();
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
                _opacityTimer.Stop();
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
                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        MessageBox.Show($"無効なURLです: {url}");
                        Debug.WriteLine($"Invalid URL in bookmarks: {url}");
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
                _ShowURLTimer.Stop();
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
                _ShowURLTimer.Stop();
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
            SettingWindow settingWindow = new SettingWindow();
            settingWindow.Owner = this; // 親ウィンドウを設定
            settingWindow.ShowDialog(); // モーダルで表示
        }

        private void LoadWindowSize()
        {
            Height = WindowsSize.LoadHeight();
            Width = WindowsSize.LoadWidth();
        }
    }
}
