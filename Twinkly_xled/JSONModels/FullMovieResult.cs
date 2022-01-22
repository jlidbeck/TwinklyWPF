using System;
using System.Text.Json.Serialization;

namespace Twinkly_xled.JSONModels
{
    // POST /xled/v1/led/movie/full
    public class FullMovieResult
    {
        public int frames_number { get; set; } // check that this is the number of frames you sent ?
        public int code { get; set; }
    }

    // POST /xled/v1/led/movie/config
    // {frame_delay} and {leds_number} get and set values only for one device, even if it is grouped.
    // Different frame delays for grouped devices cause the animations to run at different speeds,
    // though they all loop together, apparently with the speed of the fastest device.
    // This is probably not the desired effect.
    public class MovieConfig
    {
        public int frame_delay { get; set; }    // milliseconds 
        public int leds_number { get; set; }    // seems to be total number of LEDs to use. GET returns only the number of LEDs on the master device, not the group
        public int frames_number { get; set; }  // how many frames in the movie
    }

    //{
    //  "frame_delay":11,
    //  "leds_number":20,
    //  "loop_type":0,
    //  "frames_number":255,
    //  "sync":{
    //      "mode":"master",
    //      "master_id":"groupname",
    //      "compat_mode":0
    //   },
    //  "code":1000
    //}
    // GET /xled/v1/led/movie/config
    public class CurrentMovieConfig : MovieConfig
    {
        public int loop_type { get; set; }
        public SyncDef sync { get; set; }
        public int code { get; set; }

        [JsonIgnore]
        public bool IsOK => code == (int)ResultCode.Ok;
        [JsonIgnore]
        public string GroupName => sync?.master_id != null ? sync.master_id : sync?.slave_id != null ? sync.slave_id : null;
    }

    public class SyncDef
    {
        public string mode { get; set; }        // "slave" or "master"
        public string slave_id { get; set; }    // if mode is "slave", the device group name
        public string master_id { get; set; }   // if mode is "master", the device group name
        public int compat_mode { get; set; }
    }

}
