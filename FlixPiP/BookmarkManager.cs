using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace FlixPiP
{
    public static class BookmarkManager
    {
        public static IEnumerable<string> LoadBookmarks()
        {
            var col = Properties.Settings.Default.Bookmarks;
            if (col == null) return Enumerable.Empty<string>();
            return col.Cast<string>().ToList();
        }

        public static void SaveBookmarks(IEnumerable<string> bookmarks)
        {
            var col = new StringCollection();
            foreach (var b in bookmarks)
            {
                if (!string.IsNullOrWhiteSpace(b)) col.Add(b);
            }

            Properties.Settings.Default.Bookmarks = col;
            Properties.Settings.Default.Save();
        }

        public static void AddBookmark(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var col = Properties.Settings.Default.Bookmarks ?? new StringCollection();
            if (!col.Contains(url)) col.Add(url);
            Properties.Settings.Default.Bookmarks = col;
            Properties.Settings.Default.Save();
            Debug.WriteLine(url);
        }

        public static void RemoveBookmark(string url)
        {
            if (url == "All")
            {
                Properties.Settings.Default.Bookmarks = new StringCollection();
                Properties.Settings.Default.Save();
                return;
            }
            var col = Properties.Settings.Default.Bookmarks;
            if (col == null) return;
            if (col.Contains(url)) col.Remove(url);
            Properties.Settings.Default.Bookmarks = col;
            Properties.Settings.Default.Save();
        }
    }
}
