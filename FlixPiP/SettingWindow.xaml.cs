using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FlixPiP
{
    public partial class SettingWindow : Window
    {
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
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // ブックマークの保存
            var lines = BookmarkTextBox.Text
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l));
            BookmarkManager.SaveBookmarks(lines);

            this.Close();
        }
    }
}