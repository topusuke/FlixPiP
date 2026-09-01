using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlixPiP
{
    public partial class SettingWindow : Window
    {
        private bool _isRecordingShortcut;
        private HashSet<Key> _currentPressedKeys = new();
        // ショートカットUIを再実装するまで、入力処理が利用する状態を保持する。
        private readonly TextBox ShortcutTextBox = new();
        public SettingWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // ブックマークのロード
            var bookmarks = BookmarkManager.LoadBookmarks();
            BookmarkTextBox.Text = string.Join(Environment.NewLine, bookmarks);

            WindowHeightTextBox.Text = WindowsSize.LoadHeight().ToString(CultureInfo.CurrentCulture);
            WindowWidthTextBox.Text = WindowsSize.LoadWidth().ToString(CultureInfo.CurrentCulture);

            // ショートカットのロード
            var keys = ShortcutManager.LoadShortcutKeys();
            if (keys != null && keys.Any())
            {
                ShortcutTextBox.Text = string.Join(" + ", keys);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // ブックマークの保存
            var lines = BookmarkTextBox.Text
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l));
            BookmarkManager.SaveBookmarks(lines);

            if (!double.TryParse(WindowHeightTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out double height)
                || !double.TryParse(WindowWidthTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out double width)
                || !double.IsFinite(height) || !double.IsFinite(width)
                || height <= 0 || width <= 0)
            {
                MessageBox.Show(this, "高さと横幅には、0より大きい数値を入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WindowsSize.Save(height, width);
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.Height = height;
                mainWindow.Width = width;
            }

            // ショートカットの保存は登録ボタンで保存済みのためここではそのまま閉じる
            this.Close();
        }

        private void RegisterShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingShortcut) return;
            _isRecordingShortcut = true;
            _currentPressedKeys.Clear();
            ShortcutTextBox.Text = "キーを押してください... (Enterで確定, Escでキャンセル)";
            this.PreviewKeyDown += SettingWindow_PreviewKeyDown;
            this.PreviewKeyUp += SettingWindow_PreviewKeyUp;
            this.LostKeyboardFocus += SettingWindow_LostKeyboardFocus;
            Keyboard.Focus(this);
        }

        private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            ShortcutTextBox.Text = string.Empty;
            ShortcutManager.SaveShortcutKeys(Enumerable.Empty<string>());
        }

        private void SettingWindow_LostKeyboardFocus(object? sender, KeyboardFocusChangedEventArgs e)
        {
            // 録音中にフォーカスが外れたら録音を終了
            if (_isRecordingShortcut) StopRecording(save: false);
        }

        private void SettingWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // 更新された押下中キー一覧を取得して表示
            UpdateCurrentPressedKeysDisplay();
        }

        private void SettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecordingShortcut) return;

            // Enterで確定
            if (e.Key == Key.Enter)
            {
                var keys = GetCurrentlyPressedKeys().Where(k => k != Key.Enter && k != Key.Escape).ToList();
                SaveCapturedShortcut(keys);
                StopRecording(save: true);
                e.Handled = true;
                return;
            }

            // Escでキャンセル
            if (e.Key == Key.Escape)
            {
                StopRecording(save: false);
                e.Handled = true;
                return;
            }

            UpdateCurrentPressedKeysDisplay();
            e.Handled = true;
        }

        private void UpdateCurrentPressedKeysDisplay()
        {
            var keys = GetCurrentlyPressedKeys().Where(k => k != Key.None && k != Key.Enter && k != Key.Escape).ToList();
            _currentPressedKeys = new HashSet<Key>(keys);
            ShortcutTextBox.Text = keys.Any() ? string.Join(" + ", keys) : "(なし)";
        }

        private List<Key> GetCurrentlyPressedKeys()
        {
            var list = new List<Key>();
            foreach (Key k in Enum.GetValues(typeof(Key)))
            {
                if (k == Key.None) continue;
                try
                {
                    if (Keyboard.IsKeyDown(k)) list.Add(k);
                }
                catch
                {
                    // 一部の列挙値で IsKeyDown が例外を投げる可能性があるため無視
                }
            }
            return list;
        }

        private void SaveCapturedShortcut(IList<Key> keys)
        {
            var names = keys.Select(k => k.ToString()).ToList();
            ShortcutManager.SaveShortcutKeys(names);
            ShortcutTextBox.Text = names.Any() ? string.Join(" + ", names) : string.Empty;
        }

        private void StopRecording(bool save)
        {
            _isRecordingShortcut = false;
            this.PreviewKeyDown -= SettingWindow_PreviewKeyDown;
            this.PreviewKeyUp -= SettingWindow_PreviewKeyUp;
            this.LostKeyboardFocus -= SettingWindow_LostKeyboardFocus;
            if (!save)
            {
                // 元の設定を再読み込み
                var keys = ShortcutManager.LoadShortcutKeys();
                ShortcutTextBox.Text = keys != null && keys.Any() ? string.Join(" + ", keys) : string.Empty;
            }
        }
    }
}
