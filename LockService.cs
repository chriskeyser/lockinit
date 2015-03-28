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
    public class DeviceMsg
    {
         public string deviceId { get; set; }
    }

    public class LockService
    {
        private Auth0.Windows.Auth0User user;
        private HttpClient client;
        private string serverBase;
        private readonly string deviceRegistrationPath = "/api/device/register";
        private readonly string deviceDeregistrationPath = "/api/device/deregister";
        private readonly string listDevicePath = "/api/device/list";

        public LockService(Auth0.Windows.Auth0User user, string serverBase)
        {
            this.user = user;
            this.client = new HttpClient();
            this.client.DefaultRequestHeaders.Add("Authorization", "Bearer " + user.IdToken);
            this.client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            this.serverBase = serverBase;
        }
        
        public void ListLocksAync(Action<bool, List<string>, string> callback)
        {
            client.GetAsync(GetUri(listDevicePath)).ContinueWith((requestTask) =>
            {
                if (IsSuccessful("ListLockAsync", requestTask))
                {
                    HttpResponseMessage response = requestTask.Result;

                    if (response.IsSuccessStatusCode)
                    {
                        response.Content.ReadAsStringAsync().ContinueWith((readTask) =>
                        {
                            var result = Newtonsoft.Json.Linq.JObject.Parse(readTask.Result);
                            List<string> locks = (result["Locks"].ToObject<List<string>>());
                            callback(true, locks, response.StatusCode.ToString());
                        });
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceError("http failure status code: {0} {1}", response.StatusCode, response.ReasonPhrase);
                        callback(false, null, response.ReasonPhrase);
                    }
                }
                else
                {
                    callback(false, null, requestTask.Exception.Message);
                }
            });
        }

        public void DeviceDeregistrationAsync(string deviceId, Action<bool, string> callback)
        {
            var msg = new DeviceMsg()
            {
                deviceId = deviceId
            };

            client.PostAsJsonAsync<DeviceMsg>(GetUri(deviceDeregistrationPath), msg).ContinueWith(taskresp =>
            {
                if (IsSuccessful("DeviceDegisterationAsync", taskresp))
                {
                    HttpResponseMessage result = taskresp.Result;
                    if (result.IsSuccessStatusCode )
                    {
                        callback(true, null);
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceError("http failure status code: {0} {1}", result.StatusCode, result.ReasonPhrase);
                        callback(false, result.ReasonPhrase);
                    }
                }
                else
                {
                    callback(false, taskresp.Exception.Message);
                }
            }); 
        }

        public void DeviceRegistrationAsync(string deviceId, Action<bool, byte[], string> callback)
        {
            var msg = new DeviceMsg()
            {
                deviceId = deviceId
            };

            client.PostAsJsonAsync<DeviceMsg>(GetUri(deviceRegistrationPath), msg).ContinueWith(async taskresp =>
            {
                if (IsSuccessful("PostAsJsonAsync", taskresp))
                {
                    HttpResponseMessage result = taskresp.Result;

                    if (result.IsSuccessStatusCode)
                    {
                        string respContent = await result.Content.ReadAsStringAsync();
                        var resMsg = Newtonsoft.Json.Linq.JObject.Parse(respContent);
                        byte[] keyBytes = (resMsg["Key"].ToObject<List<Byte>>()).ToArray();
                        callback(true, keyBytes, null);
                    } 
                    else
                    {
                        System.Diagnostics.Trace.TraceError("http failure status code: {0} {1}", result.StatusCode, result.ReasonPhrase);
                        callback(false, null, result.ReasonPhrase);
                    }
                }
                else
                {
                    callback(false, null, taskresp.Exception.Message);
                } 
            }); 
        }

        private string GetUri(string method)
        {
            return "http://" + serverBase + method;
        }

        private bool IsSuccessful(string method, Task<HttpResponseMessage> task)
        {
            if (task.IsCompleted && task.Status != TaskStatus.Faulted)
            {
                return true;
            }

            LogTaskError(method, task);
            return false;
        }

        private void LogTaskError(string method, Task<HttpResponseMessage> taskresp)
        {
            if (taskresp.Exception != null)
            {
                System.Diagnostics.Trace.TraceError("failed on {0} {1}", method, taskresp.Exception.ToString());
            }
            else
            {
                System.Diagnostics.Trace.TraceError("failed on {0} faulted: {1} completed: {2}",
                    method, taskresp.IsFaulted, taskresp.IsCanceled);
            }
        }
    }
}
