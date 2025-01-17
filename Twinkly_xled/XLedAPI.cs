﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;

namespace Twinkly_xled
{
    // -------------------------------------------------------------------------
    // A .net C# library to communicate with Twinkly RGB Christmas lights
    // lights are located via UDP - currently you can't override if it doesn't respond
    // Authentication is handled automatically - but will expire after 4hrs
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    //        Node.js - https://github.com/linkineo/tinky-twinkly
    //        another - https://github.com/timon/twinkly/blob/master/API.md
    // another Node.js- https://github.com/cosmicChild1987/twinkly-api
    // recent node.js - https://github.com/patrickbs96/ioBroker.twinkly
    //       Mongoose - https://github.com/d4rkmen/twinkly 
    //    Angular CLI - https://github.com/chris-guilliams/Christmas-Treevia
    // --------------------------------------------------------------------------
    public class XLedAPI
    {
        public DataAccess data { get; private set; }

        public int Status { get; private set; }

        public bool Authenticated => (data?.ExpiresAt > DateTime.Now);

        public IPAddress IPAddress
        {
            get => data?.IPAddress;
            set
            {
                Debug.Assert(value != null);
                data = new DataAccess(value);
            }
        }

        public XLedAPI()
        {
        }


        #region Unauthenticated requests

        public async Task<GestaltResult> GetGestalt()
        {
            var json = await data.Get("gestalt");
            if (!data.Error)
            {
                Status = (int)data.HttpStatus;
                var gestaltResult = JsonSerializer.Deserialize<GestaltResult>(json);
                gestaltResult.Timestamp = DateTime.Now;

                // v.1:
                //RT_Buffer = new byte[gestaltResult.number_of_led * gestaltResult.bytes_per_led + 10];
                // v.3:
                // max 900 bytes of data can be sent (not including 12-byte header).
                RT_Buffer = new byte[Math.Min(900, gestaltResult.number_of_led * gestaltResult.bytes_per_led) + 12];

                // first byte is UDP packet version.
                // 1: Generation I
                // 2: Generation II up to firmware v.2.4.6
                // 3: up to firmware 2.4.14

                RT_Buffer[0] = 0x03;

                return gestaltResult;
            }
            else
            {
                Status = (int)data.HttpStatus;
                return new GestaltResult() { code = (int)data.HttpStatus, Timestamp = DateTime.Now };
            }
        }

        public async Task<FWResult> GetFirmwareVersion()
        {
            var json = await data.Get("fw/version");
            if (!data.Error)
            {
                Status = (int)data.HttpStatus;
                var result = JsonSerializer.Deserialize<FWResult>(json);
                return result;
            }
            else
            {
                return new FWResult() { code = (int)data.HttpStatus };
            }
        }

        #endregion

        #region Login

        // uses Challenge/Response authentication
        public async Task<bool> Login()
        {
            using (var rijndael = System.Security.Cryptography.Rijndael.Create())
            {
                rijndael.GenerateKey();
                var key = Convert.ToBase64String(rijndael.Key);
                var content = JsonSerializer.Serialize(new Challenge() { challenge = key });

                var json = await data.Post("login", content);
                var result = JsonSerializer.Deserialize<LoginResult>(json);

                if (result.code != 1000)
                {
                    Status = result.code;
                    return false;
                }

                data.SetAuthToken(result.authentication_token, result.authentication_token_expires_in);

                // verify
                content = JsonSerializer.Serialize(new Verify() { challenge_response = result.challenge_response });
                json = await data.Post("verify", content);
                var result2 = JsonSerializer.Deserialize<VerifyResult>(json);

                if (result2.code != 1000)
                {
                    Status = result2.code;
                    return false;
                }

                return true;
            }
        }

        // Probably invalidate access token. Doesn’t work.
        public async Task<bool> Logout()
        {
            if (Authenticated)
            {
                var json = await data.Post("logout", "{}");
                var result = JsonSerializer.Deserialize<VerifyResult>(json);

                if (result.code != 1000)
                {
                    Status = result.code;
                    return false;
                }

                // remove auth header to ensure logout
                data.SetAuthToken(null, 0);

                return true;
            }
            return true;
        }
        #endregion

        #region Authentication required

        #region DeviceName

        //// Property facade to Get/Set the device name - Live
        //public string DeviceName
        //{
        //    get
        //    {
        //        if (Authenticated)
        //        {
        //            return GetDeviceName().Result.name;
        //        }
        //        else
        //        {
        //            return GetGestalt().Result.device_name;
        //        }
        //    }
        //    set
        //    {
        //        var result = SetDeviceName(value).Result;
        //        //result.code should be 1000
        //    }
        //}


        // this is available unauthenticated from GetGestalt
        public async Task<DeviceNameResult> GetDeviceName()
        {
            if (Authenticated)
            {
                var json = await data.Get("device_name");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var name = JsonSerializer.Deserialize<DeviceNameResult>(json);

                    return name;
                }
                else
                {
                    return new DeviceNameResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new DeviceNameResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        // Max 32 chars - 1103 if too long. - could check length or truncate
        public async Task<DeviceNameResult> SetDeviceName(string newname)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new DeviceName() { name = newname });
                var json = await data.Post("device_name", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<DeviceNameResult>(json);

                    return result;
                }
                else
                {
                    return new DeviceNameResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new DeviceNameResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }
        #endregion

        #region Timer

        // Gets time when lights should be turned on and time to turn them off.
        // times are second since midnight -1 for not set
        public async Task<GetTimerResult> GetTimer()
        {
            if (Authenticated)
            {
                var json = await data.Get("timer");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<GetTimerResult>(json);

                    return result;
                }
                else
                {
                    return new GetTimerResult() { code = (int)data.HttpStatus };
                }
            }

            return new GetTimerResult() { code = (int)HttpStatusCode.Unauthorized };
        }

        // on/off -1 for N/A (else Seconds after midnight - 3600 per hour)
        public Task<VerifyResult> SetTimer(DateTime now, int on, int off)
        {
            return SetTimer(new Timer()
            {
                time_now = (int)now.TimeOfDay.TotalSeconds,
                time_on = on,
                time_off = off
            });
        }
        public async Task<VerifyResult> SetTimer(Timer timer)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(timer);
                Debug.WriteLine(content);
                var json = await data.Post("timer", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }
        #endregion

        //       led/
        //          mode
        //          effects/
        //            current 
        //          config
        //          movie/
        //            full - upload a movie
        //            config  
        //          out/brightness
        //          driver_params     POST /xled/v1/led/driver_params - but what is body ?
        //          reset

        #region Operation Mode

        public async Task<ModeResult> GetOperationMode()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/mode");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var mode = JsonSerializer.Deserialize<ModeResult>(json);

                    return mode;
                }
                else
                {
                    return new ModeResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new ModeResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        ///  Use this to Turn on or Turn off the lights "movie" or "off" 
        ///  Also used to set "rt" mode so UDP 7777 will respond - rt stops animation from movie
        /// </summary>
        public async Task<VerifyResult> SetOperationMode(LedModes mode)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new Mode() { mode = mode.ToString() });
                var json = await data.Post("led/mode", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Layout

        /// <summary>
        /// Since firmware version 1.99.18.
        /// </summary>
        /// <returns></returns>
        public async Task<GetLayoutResult> GetLayout()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/layout/full");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<GetLayoutResult>(json);

                    return result;
                }
                else
                {
                    return new GetLayoutResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new GetLayoutResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        ///  Since firmware version 1.99.18.
        /// </summary>
        public async Task<SetLayoutResult> SetLayout(Layout layout)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(layout);

                var json = await data.Post("led/layout/full", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<SetLayoutResult>(json);

                    return result;
                }
                else
                {
                    return new SetLayoutResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new SetLayoutResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Get LED Color

        // Gets the color shown when in color mode.
        // Since firmware version 2.7.1
        public async Task<LedColorResult> GetLedColor()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/color");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<LedColorResult>(json);

                    return result;
                }
                else
                {
                    return new LedColorResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new LedColorResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public async Task<VerifyResult> SetLedColor(HSV color)
        {
            if (Authenticated)
            {
                var json = await data.Post("led/color", JsonSerializer.Serialize(color));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Effects (Get only)

        // How many effects ? - what can we do with an effect ?
        public async Task<EffectsResult> Effects()
        {
            if (Authenticated)
            {
                // this is coming back with the length on the front and truncates the end of the json
                var json = await data.Get("led/effects");
                if (!data.Error)
                {
                    if (!json.StartsWith("{"))
                    {
                        // hack for malformed resonse :(
                        json = json.Substring(6) + "00F\"]}";
                    }

                    if (json.StartsWith("{"))
                    {
                        Status = (int)data.HttpStatus;
                        var eff = JsonSerializer.Deserialize<EffectsResult>(json);

                        return eff;
                    }
                    else
                    {
                        Debug.WriteLine($"Truncated JSON from led/effects {json}");
                        return new EffectsResult() { code = (int)HttpStatusCode.BadRequest };
                    }
                }
                else
                {
                    return new EffectsResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new EffectsResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        public async Task<EffectsCurrentResult> CurrentEffects()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/effects/current");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var eff = JsonSerializer.Deserialize<EffectsCurrentResult>(json);

                    return eff;
                }
                else
                {
                    return new EffectsCurrentResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new EffectsCurrentResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        // if you are interested in effects call both at the same time 
        public async Task<MergedEffectsResult> EffectsAllinOne()
        {
            var result1 = await CurrentEffects();
            var result2 = await Effects();

            return new MergedEffectsResult()
            {
                effects_number = result2.effects_number,
                effect_id = result1.effect_id,
                unique_id = result1.unique_id,
                unique_ids = result2.unique_ids,
                code = Math.Max(result1.code, result2.code)
            };
        }

        #endregion

        #region LED Config

        // For 400 LEDS - it reports 2 sets of 200 - how to use ?
        public async Task<LedConfigResult> GetLedConfig()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/config");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<LedConfigResult>(json);

                    return result;
                }
                else
                {
                    return new LedConfigResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new LedConfigResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public async Task<VerifyResult> SetLedConfig(ConfigStrings[] strings)
        {
            if (Authenticated)
            {
                LedConfigResult config = new LedConfigResult() { strings = strings };
                var json = await data.Post("led/config", JsonSerializer.Serialize(config));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Movie
        // The frame_delay value is in msec

        public async Task<FullMovieResult> UploadMovie(byte[] movie)
        {
            if (Authenticated)
            {
                // Frames ? Leds ?
                // Content-Type application/octet-stream
                var json = await data.Post("led/movie/full", "byte array goes here ?");

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<FullMovieResult>(json);

                    return result;
                }
                else
                {
                    return new FullMovieResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new FullMovieResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public async Task<CurrentMovieConfig> GetMovieConfig()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/movie/config");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<CurrentMovieConfig>(json);

                    return result;
                }
                else
                {
                    return new CurrentMovieConfig() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new CurrentMovieConfig() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // After you upload the movie 
        public Task<VerifyResult> SetMovieConfig(int frames_number, int frame_delay, int leds_number)
        {
            MovieConfig config = new MovieConfig() { frames_number = frames_number, frame_delay = frame_delay, leds_number = leds_number };
            return SetMovieConfig(config);
        }

        public async Task<VerifyResult> SetMovieConfig(MovieConfig config)
        {
            if (Authenticated)
            {
                var json = await data.Post("led/movie/config", JsonSerializer.Serialize(config));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }


        #endregion

        #region LED Brightness

        public async Task<BrightnessResult> GetBrightness()
        {
            if (Authenticated)
            {
                var json = await data.Get("led/out/brightness");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var bright = JsonSerializer.Deserialize<BrightnessResult>(json);

                    return bright;
                }
                else
                {
                    return new BrightnessResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new BrightnessResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // 0-100 setting to 100 or higher will disable dimming
        public async Task<VerifyResult> SetBrightness(byte brightness)
        {
            if (Authenticated)
            {
                Brightness bright;
                if (brightness >= 100)
                    bright = new Brightness() { mode = "disabled", value = 100 };
                else
                    bright = new Brightness() { mode = "enabled", value = brightness };

                var json = await data.Post("led/out/brightness", JsonSerializer.Serialize(bright));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED reset

        // Reset does what ?
        // (patrickbs96) Temporary removing Reset as API path not exists
        // (cosmicChild1987) - posts {} to this 
        public async Task<VerifyResult> Reset()
        {
            if (Authenticated)
            {
                //var json = await data.Get("led/reset");
                var json = await data.Post("led/reset", "{}");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Update Firmware

        // can you really write better fw without bricking it ?
        // Not implemented: Update firmware - POST /xled/v1/fw/update
        // Not implemented: Upload first stage of firmware - POST /xled/v1/fw/0/update
        // Not implemented: Upload second stage of firmware - POST /xled/v1/fw/1/update

        #endregion

        #region Network

        // Not implemented: Initiate WiFi network scan - GET /xled/v1/network/scan
        // Not implemented: Get results of WiFi network scan - GET /xled/v1/network/scan_results
        // Not implemented: Get network status - GET /xled/v1/network/status
        // Not implemented: Set network status - POST /xled/v1/network/status

        #endregion

        #region MQTT

        // d4rkmen says : Unfortunatley, the newest devices (Gen2) use SSL connection to MQTT broker port 8883, which makes it impossible to use custom broker 
        // because of hardcoded CA inside the firmware.
        public async Task<MQTTConfigResult> GetMQTTConfig()
        {
            if (Authenticated)
            {
                var json = await data.Get("mqtt/config");
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var mqtt = JsonSerializer.Deserialize<MQTTConfigResult>(json);

                    return mqtt;
                }
                else
                {
                    return new MQTTConfigResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new MQTTConfigResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Not implemented: SetMQTTConfig - POST /xled/v1/mqtt/config

        #endregion

        #region Paint

        // the realtime buffer has a key and 3 bytes for every light
        private byte[] RT_Buffer;

        // Use RT 7777 UDP to set all lights to the same color 
        // pass color as byte array RGB
        public async Task SingleColor(byte[] c)
        {
            Debug.Assert(c.Length == 3);

//            if (Authenticated && c.Length == 3)
            {
                // Authentication
                RT_Buffer[0] = 0x01;    // Gen.1 format, for testing
                //var token = data.GetAuthToken();
                var token = await GetAuthToken(true);
                var tokenbytes = Convert.FromBase64String(token);
                tokenbytes.CopyTo(RT_Buffer, 1);

                // Color Data
                for (int i = 10; i < RT_Buffer.Length; i += c.Length)
                {
                    Buffer.BlockCopy(c, 0, RT_Buffer, i, c.Length);
                }

                var changemode = await SetOperationMode(LedModes.rt);
                if (changemode.code == 1000)
                {
                    data.RTFX(RT_Buffer);
                }
            }
        }

        async Task<string> GetAuthToken(bool autoReauth)
        {
            if (data.ExpiresIn.TotalMinutes <= 1)
            {
                if (!autoReauth)
                    return null;

                await Login();
            }

            return data.GetAuthToken();
        }

        // Use RT 7777 UDP to set all lights
        // pass color as byte array RGB
        // warning: mode must be "rt" (this function does not set it)
        public void SendFrame(byte[] frameData, int offset=0, int length=20, byte frameFragment=0)
        {
            // Authentication
            var token = data.GetAuthToken();

            if (token == null)
                return;

            RT_Buffer[0] = 0x03;
            var tokenbytes = Convert.FromBase64String(token);
            tokenbytes.CopyTo(RT_Buffer, 1);

            RT_Buffer[9] = 0;
            RT_Buffer[10] = 0;
            RT_Buffer[11] = frameFragment;

            // repeat frameData as many times as needed to fill buffer
            //for (int i = 10; i < RT_Buffer.Length; i += frameData.Length)
            //{
            //    Buffer.BlockCopy(frameData, 0, RT_Buffer, i, Math.Min(frameData.Length, RT_Buffer.Length - i));
            //}

            // new: copy only range
            Buffer.BlockCopy(frameData, offset, RT_Buffer, 12, length);

            data.RTFX(RT_Buffer);
        }

        #endregion

        #endregion

    }
}

