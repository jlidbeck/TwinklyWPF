namespace Twinkly_xled.JSONModels
{
    // used by calls that only return a code in their response, e.g.
    // Verify, set Timer, set brightness, set mode
    public class VerifyResult
    {
        public int code { get; set; } // 1000 is ok 1104 is not
    }
}
