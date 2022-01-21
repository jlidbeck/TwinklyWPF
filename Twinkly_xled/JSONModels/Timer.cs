namespace Twinkly_xled.JSONModels
{

    // all times in seconds after midnight
    // {time_now:25954,time_on:-1,time_off:-1,tz:,code:1000}
    // POST /xled/v1/timer
    public class Timer
    {
        public int time_now { get; set; }
        public int time_on { get; set; }
        public int time_off { get; set; }
    }

    // GET /xled/v1/timer
    public class GetTimerResult : Timer
    {
        public int code { get; set; }
    }
}
