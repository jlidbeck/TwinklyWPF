using System.Diagnostics;

namespace Twinkly_xled.JSONModels
{
    [DebuggerDisplay("{x}, {y}, {z}")]
    public struct XYZ
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    [DebuggerDisplay("[{coordinates.Length}] {source}")]
    public class Layout
    {
        public int aspectXY { get; set; }
        public int aspectXZ { get; set; }
        public XYZ[] coordinates { get; set; }
        //"linear", "2d", or "3d"
        public string source { get; set; }
        public bool synthesized { get; set; }
    }

    public class GetLayoutResult : Layout
    {
        public string uuid { get; set; }

        public int code { get; set; }
    }

    public class SetLayoutResult
    {
        public int code { get; set; }

        public int parsed_coordinates { get; set; }
    }
}


