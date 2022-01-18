namespace Twinkly_xled.JSONModels
{
    public class XYZ
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class Layout
    {
        //(integer), e.g. 0
        public int aspectXY { get; set; }
        //(integer), e.g. 0
        public int aspectXZ { get; set; }
        //(array)
        public XYZ[] coordinates { get; set; }
        //"linear", "2d", or "3d"
        public string source { get; set; }
        //(bool), e.g. false
        public bool synthesized { get; set; }
    }

    public class GetLayoutResult : Layout
    {
        public string uuid { get; set; }

        public int code { get; set; }
    }

    public class SetLayoutResult
    {
        // application return code.
        public int code { get; set; }
        //
        public int parsed_coordinates { get; set; }
    }
}


