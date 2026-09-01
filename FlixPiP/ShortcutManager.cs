using System.Collections.Specialized;

namespace FlixPiP
{
    public static class ShortcutManager
    {
        private const string SettingName = "ShortcutKeys";

        public static IEnumerable<string> LoadShortcutKeys()
        {
            // 設定が未定義の場合にも安全に空の一覧を返す
            if (Properties.Settings.Default.Properties[SettingName] == null)
                return Enumerable.Empty<string>();

            if (Properties.Settings.Default[SettingName] is not StringCollection collection)
                return Enumerable.Empty<string>();

            return collection.Cast<string>().ToList();
        }

        public static void SaveShortcutKeys(IEnumerable<string> keys)
        {
            var collection = new StringCollection();
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    collection.Add(key);
            }

            Properties.Settings.Default[SettingName] = collection;
            Properties.Settings.Default.Save();
        }
    }
}
