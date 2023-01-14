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

        private IPAddress tw_IP { get; set; }
        public string IPAddressString
        {
            get => tw_IP.ToString();
            set { tw_IP = System.Net.IPAddress.Parse(value); }
        }

        public System.Net.IPAddress IPAddress
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
                //HttpResponseMessage = null;
                HttpStatus = HttpStatusCode.OK;
                ExpiresAt = new DateTime();
            }
        }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn => (ExpiresAt - DateTime.Now);


        //public DataAccess()
        //{
        //    // now call Discover() or set IPAddress
        //}

        public DataAccess(IPAddress ipAddress)
        {
            IPAddress = ipAddress;
        }

        public override string ToString()
        {
            return $"DataAccess: {IPAddress} Token expires: {ExpiresAt}";
        }

        // UDP Scan for the lights. Returns all responding IP addresses 
        static public ICollection<string> Discover()
        {
            var addresses = new SortedSet<string>();

            const int PORT_NUMBER = 5555;

            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;

                // send
                byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");

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
                            var result = await udp.ReceiveAsync();

                            // don't need to parse the message - we know who responded
                            // <ip>OK<device_name>
                            Debug.WriteLine($"Reply: {result.RemoteEndPoint.Address}: {BitConverter.ToString(result.Buffer)}");
                            addresses.Add(result.RemoteEndPoint.Address.ToString());
                        }
                    });
                    task.Wait(3000);
                }
                catch (SocketException err)
                {
                    // If using synchronous receive, we expect a timeout. (ReceiveAsync does not do this.)
                    // Any other error is unexpected, so rethrow
                    if (err.SocketErrorCode != SocketError.TimedOut)
                    {
                        // Unexpected error
                        //Error = true;
                        Debug.WriteLine($"Terminating: {err.Message}");
                        throw;
                    }
                }
                finally
                {
                    udp.Close();
                }
            }   // using udp

            Debug.WriteLine($"Discover: found {addresses.Count()} devices");

            //if (IPAddress == null && addresses.Count() > 0)
            //{
            //    IPAddress = IPAddress.Parse(addresses.FirstOrDefault());
            //}

            return addresses;
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
            return null;
        }
    }
}
