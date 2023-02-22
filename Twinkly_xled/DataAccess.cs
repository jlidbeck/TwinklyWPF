using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Twinkly_xled
{
    public class DataAccess
    {
        public HttpClient HttpClient { get; private set; }

        public bool Error { get; private set; } = false;
        public string ErrorMessage { get; private set; }
        public HttpStatusCode HttpStatus { get; private set; } = HttpStatusCode.OK;

        static public Exception LastError { get; private set; }

        private IPAddress tw_IP { get; set; }
        public string IPAddressString
        {
            get => tw_IP.ToString();
            private set { tw_IP = System.Net.IPAddress.Parse(value); }
        }

        public System.Net.IPAddress IPAddress
        {
            get { return tw_IP; }
            private set
            {
                tw_IP = value;
                if (value != null)
                    HttpClient = new HttpClient() { BaseAddress = new Uri($"http://{tw_IP}/xled/v1/") };
                else
                    HttpClient = null;
                Error = false;
                //HttpResponseMessage = null;
                HttpStatus = HttpStatusCode.OK;
                ExpiresAt = new DateTime();
            }
        }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn => (ExpiresAt - DateTime.Now);


        public DataAccess(IPAddress ipAddress)
        {
            IPAddress = ipAddress;
        }

        public override string ToString()
        {
            return $"DataAccess: {IPAddress} Token expires: {ExpiresAt}";
        }

        // Discover devices on network using a UDP request.
        // Returns all responding IP addresses.
        // Note: returned IP addresses are not unique--typically each address responds twice.
        // From observation, first response occurs within 200-400 ms, last response within 800 ms.
        static public async IAsyncEnumerable<string> DiscoverAsync()
        {
            const int PORT_NUMBER = 5555;

            LastError = null;

            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;

                // send
                byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");

                try
                {
                    // broadcast request
                    await udp.SendAsync(sendbuf, sendbuf.Length, 
                                        new IPEndPoint(IPAddress.Broadcast, PORT_NUMBER));

                    var stopwatch = Stopwatch.StartNew();

                    while (true)
                    {
                        // wait up to 1s between responses
                        var task = udp.ReceiveAsync();
                        if (!task.Wait(3000) || stopwatch.ElapsedMilliseconds > 9000)
                            yield break;  // stop when no reply received for 1 second
                        UdpReceiveResult result = task.Result;

                        yield return result.RemoteEndPoint.Address.ToString();
                    }
                }
                finally
                {
                    udp.Close();
                }
            }
        }

        // UDP port 7777 for realtime 
        public int RTFX(byte[] buffer)
        {
            const int PORT_NUMBER = 7777;

            try
            {
                using (var client = new UdpClient())
                {

                    // send
                    return client.Send(buffer, buffer.Length, new IPEndPoint(IPAddress, PORT_NUMBER));
                    //return client.SendAsync(buffer, buffer.Length, new IPEndPoint(IPAddress, PORT_NUMBER));
                    // Hope it made it 

                }
            }
            catch (SocketException err)
            {
                // this can happen if the queue is full or a buffer is too small
                Error = true;
                ErrorMessage = err.Message;
                throw;  // should we even catch this here?
            }
        }


        /// <summary>
        /// GET - read information from the twinkly API
        /// Throws exception if IPAddress has not been set
        /// </summary>
        public async Task<string> Get(string url)
        {
            Error = false;
            //HttpResponseMessage = null;
            HttpStatus = HttpStatusCode.OK;
            try
            {
                var HttpResponseMessage = await HttpClient.GetAsync(url);
                //HttpStatus = HttpResponseMessage.StatusCode;
                if (HttpResponseMessage.IsSuccessStatusCode)
                {
                    return await HttpResponseMessage.Content.ReadAsStringAsync();
                }
                else
                {
                    Error = true;
                    HttpStatus = HttpResponseMessage.StatusCode;
                    ErrorMessage = HttpResponseMessage.StatusCode.ToString();
                    return ErrorMessage;
                }
            }
            catch (HttpRequestException ex)
            {
                Error = true;

                // actual timeouts have statuscode==null.. why?
                HttpStatus = ex.StatusCode ?? HttpStatusCode.InternalServerError;
                ErrorMessage = $"ERROR {ex.Message}";
                return ErrorMessage;
            }
            catch (Exception ex)
            {
                //HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                ErrorMessage = $"ERROR {ex.Message}";
                return ErrorMessage;
            }
        }

        /// <summary>
        /// POST - change information on the twinkly device
        /// Throws exception if IPAddress has not been set
        /// </summary>
        public async Task<string> Post(string url, string content)
        {
            Error = false;
            //HttpResponseMessage = null;
            HttpStatus = HttpStatusCode.OK;
            try
            {
                var HttpResponseMessage = await HttpClient.PostAsync(url, new StringContent(content));
                //HttpStatus = result.StatusCode;
                if (HttpResponseMessage.StatusCode == HttpStatusCode.OK)
                {
                    return await HttpResponseMessage.Content.ReadAsStringAsync();
                }
                else
                {
                    Error = true;
                    return HttpResponseMessage.StatusCode.ToString();
                }
            }
            catch (Exception ex)
            {
                //HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                return $"ERROR {ex.Message}";
            }
        }

        // Note the use of X-Auth-Token indicates a less than state of the art authentication system
        public void SetAuthToken(string token, int expires)
        {
            if (HttpClient.DefaultRequestHeaders.Contains("X-Auth-Token"))
                HttpClient.DefaultRequestHeaders.Remove("X-Auth-Token");

            if (string.IsNullOrEmpty(token))
            {
                ExpiresAt = DateTime.Now;
            }
            else
            {
                HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", token);
                ExpiresAt = DateTime.Now.AddSeconds(expires);
            }

            Debug.WriteLine($"Auth Token {token} expires at {ExpiresAt:T}");
        }

        public string GetAuthToken()
        {
            if (ExpiresIn.TotalMinutes > 0)
            {
                return HttpClient.DefaultRequestHeaders.GetValues("X-Auth-Token").FirstOrDefault();
            }
            return null;
        }
    }
}
