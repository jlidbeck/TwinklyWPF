namespace Twinkly_xled.JSONModels
{
    public class LedColor 
    {
        // GET returns all 6 values.
        // SET can specify either HSV or RGB.
        public int hue { get; set; }
        public int saturation { get; set; }
        public int value { get; set; }
        public int red { get; set; }
        public int green { get; set; }
        public int blue { get; set; }
    }

    public class RGB
    {
        // GET returns all 6 values.
        // SET can specify either HSV or RGB.
        public int red { get; set; }
        public int green { get; set; }
        public int blue { get; set; }
    }

    public class HSV
    {
        // GET returns all 6 values.
        // SET can specify either HSV or RGB.
        public int hue { get; set; }
        public int saturation { get; set; }
        public int value { get; set; }
    }

    public class LedColorResult : LedColor
    {
        public int code { get; set; }
    }

}
