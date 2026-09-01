namespace FlixPiP
{
    public static class WindowsSize
    {
        public const double DefaultHeight = 300;
        public const double DefaultWidth = 533;
        public const double MinimumHeight = 150;
        public const double MinimumWidth = 200;

        public static double LoadHeight()
        {
            double height = Properties.Settings.Default.WindowHeight;
            return IsValidHeight(height) ? height : DefaultHeight;
        }

        public static double LoadWidth()
        {
            double width = Properties.Settings.Default.WindowWidth;
            return IsValidWidth(width) ? width : DefaultWidth;
        }

        public static void Save(double height, double width)
        {
            Properties.Settings.Default.WindowHeight = height;
            Properties.Settings.Default.WindowWidth = width;
            Properties.Settings.Default.Save();
        }

        public static bool IsValidHeight(double height)
        {
            return double.IsFinite(height) && height >= MinimumHeight;
        }

        public static bool IsValidWidth(double width)
        {
            return double.IsFinite(width) && width >= MinimumWidth;
        }
    }
}
