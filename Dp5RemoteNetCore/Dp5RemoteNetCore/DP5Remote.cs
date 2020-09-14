using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Linq;

/// <summary>
/// This namespace contains code to access the DP5 remote API via the RESTful interface.  The API has more functions than shown
/// below; the full documentation for this can be accessed at http://<host>:<port>/swagger-ui.html
/// </summary>
namespace DP5RemoteNetCore
{
    /// <summary>
    /// Class to execute DP5 calls; note that this code contains no error handling; it is recommended to include this in 
    /// production code
    /// </summary>
    internal class DP5Remote
    {
        private string host;
        private int port;

        private HttpClient client = new HttpClient();

        /// <summary>
        /// Default contructor, assumes that the server is on localhost and listenign on port 8777.  Note that this expects the DP5 service to be running
        /// if it is not then execute C:\Program Files\Ziath\DP5\resources\dp5-server\dp5-headless.exe (assuming you are on English language windows 
        /// and using a default install location)
        /// </summary>
        public DP5Remote() : this("localhost", 8777)
        {
        }

        /// <summary>
        /// Sample code to interact with DP5; this queris some parameters and executes a scan.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            DP5Remote dp5Remote = new DP5Remote();
            Console.WriteLine($"Version = {dp5Remote.Version}");
            Console.WriteLine($"Sttaus = {dp5Remote.Status}");
            Console.WriteLine($"Licence = {dp5Remote.Licence}");
            JArray containers = dp5Remote.Containers;
            Console.WriteLine($"Containers = {containers}");
            string uid = containers.FirstOrDefault()?["uid"].Value<string>();
            Console.WriteLine($"scanning {uid}");
            Console.WriteLine(dp5Remote.scan(uid));
            dp5Remote.shutdown();
        }


        /// <summary>
        /// Public construcotr, note that this does not connect and assumes that the server is on localhost and listenign on port 8777.  
        /// Note that this expects the DP5 service to be running if it is not then execute 
        /// C:\Program Files\Ziath\DP5\resources\dp5-server\dp5-headless.exe (assuming you are on English language windows and using a 
        /// default install location)
        /// </summary>
        /// <param name="host">The host the DP5 headless server is running on</param>
        /// <param name="port">The psot the DP5 headless server is listening on</param>
        public DP5Remote(String host, int port)
        {
            this.host = host;
            this.port = port;
        }

        /// <summary>
        /// The version of the DP5 server which is running
        /// </summary>
        public string Version
        {
            get
            {
                return JObject.Parse(GetStringSync("system/version")).SelectToken("version").Value<string>();
         
            }
        }

        /// <summary>
        /// The status of the DP5 server; can be IDLE, REMOTE or BUSY
        /// </summary>
        public string Status
        {
            get
            {
                return JObject.Parse(GetStringSync("system/status")).SelectToken("status").Value<string>();

            }
        }

        /// <summary>
        /// Returns the licence as a JSON object, it is recommended to create a class to represent the licence when used in production
        /// </summary>
        public JObject Licence
        {
            get
            {
                return JObject.Parse(GetStringSync("licence"));
            }
        }
        /// <summary>
        /// A JSON array containing all the containers configured in the system; it is recommended to create a class to represent the container
        /// when used in production.
        /// </summary>
        public JArray Containers
        {
            get
            {
                return JArray.Parse(GetStringSync("containers"));
            }
        }

        /// <summary>
        /// Executes a scan according to the provided UID and returns the results as a JObject.  It is recommended to create classes to repesent
        /// the scan result in production.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public JObject scan(string uid)
        {
            return JObject.Parse(PostStringSync("scan", new Dictionary<string, string>() { { "container_uid", uid } }));
        }

        /// <summary>
        /// Closes the DP5 server process
        /// </summary>
        public void shutdown()
        {
            client.PutAsync(ConstructUrl("system/shutdown"), new StringContent("")).Wait();
        }

        /// <summary>
        /// Executes a GET and waits indefinitely for the return
        /// </summary>
        /// <param name="path">The section of the URL to add to the stub of the DP5 API URL</param>
        /// <returns>The body of the response</returns>
        private string GetStringSync(string path)
        {
            Task<string> getTask = client.GetStringAsync(ConstructUrl(path, new Dictionary<string, string>()));
            getTask.Wait();
            return getTask.Result;
        }

        /// <summary>
        /// Executes a GET and waits indefinitely for the return
        /// </summary>
        /// <param name="path">The section of the URL to add to the stub of the DP5 API URL</param>
        /// <param name="qParams">A dictionary of query paramers to use</param>
        /// <returns>The body of the response</returns>
        private string GetStringSync(string path, Dictionary<string, string> qParams)
        {
            Task<string> getTask = client.GetStringAsync(ConstructUrl(path, qParams));
            getTask.Wait();
            return getTask.Result;
        }

        /// <summary>
        /// Executes a POST and waits indefinitely for the return
        /// </summary>
        /// <param name="path">The section of the URL to add to the stub of the DP5 API URL</param>
        /// <param name="qParams">A dictionary of query paramers to use</param>
        /// <returns>The body of the response</returns>
        private string PostStringSync(string path, Dictionary<string, string> qParams)
        {
            Task<HttpResponseMessage> responseTask = client.PostAsync(ConstructUrl(path, qParams), new StringContent(""));
            responseTask.Wait();
            Task<string> contentTask =  responseTask.Result.Content.ReadAsStringAsync();
            contentTask.Wait();
            return contentTask.Result;
        }

        /// <summary>
        /// Appends the given path to the stub of the DP5 API URL
        /// </summary>
        /// <param name="path">The path to append to the stub</param>
        /// <returns>The complete URL</returns>
        private String ConstructUrl(string path)
        {
            return ConstructUrl(path, new Dictionary<string, string>());
        }

        /// <summary>
        /// Appends the given path to the stub of the DP5 API URL plus the given query params
        /// </summary>
        /// <param name="path">The path to append to the stub</param>
        /// <param name="qParams">A key/value pair of the query params</param>
        /// <returns></returns>
        private string ConstructUrl(string path, Dictionary<string, string> qParams)
        {
            UriBuilder ub = new UriBuilder($"{this.Stub}/{path}");
            NameValueCollection nvc = HttpUtility.ParseQueryString(ub.Query);
            foreach (KeyValuePair<string, string> kvp in qParams){
                nvc[kvp.Key] = kvp.Value;
            }
            
            ub.Query = nvc.ToString();
            return ub.ToString();
        }

        /// <summary>
        /// The stub of the DP5 API URL; this is set according to the given host and port
        /// </summary>
        private string Stub
        {
            get
            {
                return $"http://{this.host}:{this.port}/dp5/remote/v1";
            }
        }

    }
}
