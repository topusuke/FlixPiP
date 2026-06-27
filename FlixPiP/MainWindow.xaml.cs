using System;
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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const uint LWA_ALPHA = 0x2;

        // ホットキー用の定数
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const uint VK_UP = 0x26;
        private const uint VK_DOWN = 0x28;
        private const uint VK_LEFT = 0x25;
        private const uint VK_RIGHT = 0x26;

        // ホットキーID定義
        private const int HK_MODE_OP = 9001;   // Ctrl + ↑ (操作モード)
        private const int HK_MODE_GAME = 9002; // Ctrl + ↓ (すり抜けモード)
        private const int HK_CLOSE = 9003;     // Ctrl + → (アプリ終了)
        private const int HK_NAV_GOOGLE = 9004;// Ctrl + ← (Googleを開く)

        private IntPtr _windowHandle;
        private int _initialStyle;

        private Window _currentOpacityWindow;
        private System.Windows.Threading.DispatcherTimer _opacityTimer;
        private byte _currentOpacity = 127; // 透明度の設定 （初期値50%）

        public MainWindow()
        {
            InitializeComponent();

            webView.EnsureCoreWebView2Async();
            webView.Source = new Uri("https://google.com");

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            webView.NavigationCompleted += WebView_NavigationCompleted;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            _initialStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);

            // 操作可能状態
            SetWindowLong(_windowHandle, GWL_EXSTYLE, _initialStyle | WS_EX_LAYERED);
            SetLayeredWindowAttributes(_windowHandle, 0, 255, LWA_ALPHA);

            // グローバルホットキー登録
            RegisterHotKey(_windowHandle, HK_MODE_OP, MOD_CONTROL, VK_UP);
            RegisterHotKey(_windowHandle, HK_MODE_GAME, MOD_CONTROL, VK_DOWN);
            RegisterHotKey(_windowHandle, HK_CLOSE, MOD_CONTROL, 0x27); // 0x27 = 矢印右 (Ctrl + →)
            RegisterHotKey(_windowHandle, HK_NAV_GOOGLE, MOD_CONTROL, 0x25); // 0x25 = 矢印左 (Ctrl + ←)

            // Alt + 矢印のホットキー登録（透明度変更用）
            RegisterHotKey(_windowHandle, 9005, MOD_SHIFT, VK_UP);    // Shift + ↑ (透明度UP)
            RegisterHotKey(_windowHandle, 9006, MOD_SHIFT, VK_DOWN);  // Shift + ↓ (透明度DOWN)

            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
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
            }
        }

        // 矢印キーによる移動とサイズ変更の処理
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            double moveStep = 15; // 1回に動くピクセル数
            double resizeStep = 20;

            // Altキーが押されているかどうか
            bool isAlttPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (isAlttPressed)
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
    }
}