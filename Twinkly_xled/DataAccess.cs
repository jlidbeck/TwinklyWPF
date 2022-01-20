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
        //public HttpStatusCode HttpStatus { get; private set; } = HttpStatusCode.OK;
        public HttpResponseMessage HttpResponseMessage;
        public HttpStatusCode HttpStatus => HttpResponseMessage != null 
                                            ? HttpResponseMessage.StatusCode 
                                            : HttpStatusCode.InternalServerError;

        private IPAddress tw_IP { get; set; }
        public IPAddress IPAddress
        {
            get { return tw_IP; }
            set
            {
                tw_IP = value;
                if (value != null)
                    HttpClient = new HttpClient() { BaseAddress = new Uri($"http://{tw_IP}/xled/v1/") };
                else
                    HttpClient = null;
                Error = false;
                HttpResponseMessage = null;
                ExpiresAt = new DateTime();
            }
        }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn => (ExpiresAt - DateTime.Now);
        public bool Authenticated => (ExpiresIn.TotalMinutes > 0);


        public DataAccess()
        {
            // now call Locate() or set IPAddress
        }

        //public DataAccess(IPAddress ipAddress)
        //{
        //}

        public override string ToString()
        {
            return $"DataAccess: {IPAddress} Auth: {Authenticated}";
        }

        // UDP Scan for the lights - can only deal with the first one 
        public List<IPAddress> Locate()
        {
            var devices = new List<IPAddress>();

            const int PORT_NUMBER = 5555;

            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                udp.Client.ReceiveTimeout = 1000; // 1 sec, synchronous only

                // send
                byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");

                var addresses = new SortedSet<string>();

                try
                {
                    udp.Send(sendbuf, sendbuf.Length, new IPEndPoint(
                             IPAddress.Broadcast,
                             PORT_NUMBER));

                    var task = Task.Run(async () =>
                    {
                        while (true)
                        {
                            // receive
                            UdpReceiveResult result = await udp.ReceiveAsync();

                            // don't need to parse the message - we know who responded
                            // <ip>OK<device_name>
                            Debug.WriteLine($"Reply: {result.RemoteEndPoint.Address}: {BitConverter.ToString(result.Buffer)}");
                            addresses.Add(result.RemoteEndPoint.Address.ToString());
                        }
                    });
                    task.Wait(2000);
                }
                catch (SocketException err)
                {
                    // If using synchronous receive, we expect a timeout. If any other error, rethrow
                    if (err.SocketErrorCode != SocketError.TimedOut)
                    {
                        // Timed out
                        Debug.WriteLine($"Terminating: {err.Message}");
                        throw;
                    }
                }

                Debug.WriteLine($"** got {addresses.Count()} addresses **");
                foreach (var ip in addresses)
                {
                    devices.Add(IPAddress.Parse(ip));
                }
            }   // using udp

            return devices;
        }

        // UDP port 7777 for realtime 
        public void RTFX(byte[] buffer)
        {
            const int PORT_NUMBER = 7777;

            using (var client = new UdpClient())
            {

                // send
                client.Send(buffer, buffer.Length, new IPEndPoint(IPAddress, PORT_NUMBER));

                // Hope it made it 

            }
        }


        /// <summary>
        /// GET - read information from the twinkly API
        /// </summary>
        public async Task<string> Get(string url)
        {
            Error = false;
            HttpResponseMessage = null;
            try
            {
                HttpResponseMessage = await HttpClient.GetAsync(url);
                //HttpStatus = HttpResponseMessage.StatusCode;
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

        /// <summary>
        /// POST - change information on the twinkly device
        /// </summary>
        public async Task<string> Post(string url, string content)
        {
            Error = false;
            HttpResponseMessage = null;
            try
            {
                HttpResponseMessage = await HttpClient.PostAsync(url, new StringContent(content));
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
        public void Authenticate(string token, int expires)
        {
            if (HttpClient.DefaultRequestHeaders.Contains("X-Auth-Token"))
                HttpClient.DefaultRequestHeaders.Remove("X-Auth-Token");

            HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", token);
            ExpiresAt = DateTime.Now.AddSeconds(expires);

            Debug.WriteLine($"Auth Token {token} expires at {ExpiresAt:T}");
        }

        public string GetAuthToken()
        {
            if (ExpiresIn.TotalMinutes > 0)
            {
                return HttpClient.DefaultRequestHeaders.GetValues("X-Auth-Token").FirstOrDefault();
            }
            return string.Empty;
        }
    }
}
