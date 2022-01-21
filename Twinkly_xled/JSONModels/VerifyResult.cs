namespace Twinkly_xled.JSONModels
{
    // used by calls that only return a code in their response, e.g.
    // Verify, set Timer, set brightness, set mode
    public class VerifyResult
    {
        public int code { get; set; } // 1000 is ok 1104 is not

        public override string ToString()
        {
            ResultCode? resultCode = (ResultCode?)code;
            return $"code: {code} {resultCode?.ToString()}";
        }
        public bool IsOK => code == 1000;
    }

    public enum ResultCode
    {
        Ok                      = 1000,
        Error                   = 1001,
        InvalidArgumentValue    = 1101,
        Error1102               = 1102,
        Error1103               = 1103,
        ValueError              = 1104,    // Error - value too long? Or missing required object key?
        JSONError               = 1105,
        InvalidArgumentKey      = 1105,
        Ok1107                  = 1107,     // unknown
        Ok1108                  = 1108,     // unknown
        FirmwareUpgradeError    = 1205      // Error with firmware upgrade - SHA1SUM does not match
    }
}
