namespace CompileScore.Common
{
    using System.Windows.Media;

    class Colors
    {
        public static Brush GetSeverityBrush(uint severity)
        {
            switch (severity)
            {
                case 1: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)200, (byte)200, (byte)200));
                case 2: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)30,  (byte)255, (byte)0));
                case 3: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)0,   (byte)112, (byte)221));
                case 4: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)163, (byte)53,  (byte)238));
                case 5: return new SolidColorBrush(Color.FromArgb((byte)255, (byte)255, (byte)128, (byte)0)); 
            }

            return new SolidColorBrush(Color.FromArgb((byte)255, (byte)0, (byte)0, (byte)0));
        }

    }
}
