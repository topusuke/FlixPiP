namespace FlixPiP
{
    public static class WindowsSize
    {
        public static double LoadHeight()
        {
            return Properties.Settings.Default.WindowHeight;
        }

        public static double LoadWidth()
        {
            return Properties.Settings.Default.WindowWidth;
        }

        public static void Save(double height, double width)
        {
            Properties.Settings.Default.WindowHeight = height;
            Properties.Settings.Default.WindowWidth = width;
            Properties.Settings.Default.Save();
        }
    }
}
