using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace LockInitClient
{
    public class AddDeviceMsg
    {
        public string userId { get; set; }
        public string deviceId { get; set; }
    }
    
    public class LockService
    {
        private Auth0.Windows.Auth0User user;
        private HttpClient client;
        private string serverBase;
        private string deviceRegistrationPath = "/api/device/register";

        public LockService(Auth0.Windows.Auth0User user, string serverBase)
        {
            this.user = user;
            this.client = new HttpClient();
            this.client.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.IdToken);
            this.client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            this.serverBase = serverBase;
        }
        
        private string GetUri(string method)
        {
            return "http://" + serverBase + method;
        }

        public void StartDeviceRegistration(string deviceId, Action<bool, string> response)
        {
            var msg = new AddDeviceMsg()
            {
                deviceId = deviceId,
                userId = (string)user.Profile["user_id"]
            };

            client.PostAsJsonAsync<AddDeviceMsg>(GetUri(deviceRegistrationPath), msg).ContinueWith(async taskresp =>
            {
                if (taskresp.IsCompleted)
                {
                    HttpResponseMessage result = taskresp.Result;
                    string respContent = await result.Content.ReadAsStringAsync();
                    // TODO: deserialize json
                    var resMsg = Newtonsoft.Json.Linq.JObject.Parse(respContent);
                    var key = (string)resMsg["Key"];
                    response(true, key);
                }
                else
                {
                    if (taskresp.Exception != null)
                    {
                        System.Diagnostics.Trace.TraceError("failed on LockService::StartDeviceRegistration {0}", taskresp.Exception.ToString());
                        response(false, null);
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceError("failed on LockService::StartDeviceRegistration faulted: {0} completed: {1}",
                            taskresp.IsFaulted, taskresp.IsCanceled);
                    }
                }
            }); 
        }
    }
}
