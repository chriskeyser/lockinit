using System;
using System.Threading.Tasks;
using System.Windows;
using Auth0.Windows;
using System.Windows.Interop;
using System.Configuration;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace LockInitClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Auth0Client auth0 = new Auth0Client(ConfigurationManager.AppSettings["auth0:Domain"],
                                            ConfigurationManager.AppSettings["auth0:ClientId"]);
        private Auth0User loggedInUser;
        private LockInitHandler handler;
        private LockService lockService;
        private bool isListingRegisteredLocks = false;
        ObservableCollection<String> deviceIds = new ObservableCollection<String>();
        ObservableCollection<String> logs = new ObservableCollection<String>();

        public MainWindow()
        {
            InitializeComponent();
            this.LogList.ItemsSource = logs;
        }

        private void OnClearDevice(object sender, RoutedEventArgs e)
        {
            //TODO: should reset device as well?  If so how is it safe to do that? From the 
            // serivce with encrpted command?  Or should assume device reset happens via physical
            // interaction with device?
            string device = this.DiscoveredDevicesList.SelectedItem as string;
            string mqttServer = null;
            int mqttPort;

            if (isListingRegisteredLocks && device != null)
            {
                if (GetServerAndPort(out mqttServer, out mqttPort))
                {
                    var serviceAddr = mqttServer + ":" + "3000";
                    lockService = new LockService(loggedInUser, serviceAddr);
                    lockService.DeviceDeregistrationAsync(device, (succeeded, errCode) =>
                    {
                        if (succeeded)
                        {
                            this.AddStatus("deregistered device");
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                this.deviceIds.Remove(device);
                            }));
                        }
                        else
                        {
                            this.AddStatus(string.Format("deregistered failed, error: {0}", errCode));
                        }
                    });
                }
            }
            else
            {
                AddStatus("List and select a lock");
            }
        }

        private void OnListDevices(object sender, RoutedEventArgs e)
        {
            string mqttServer = null; 
            int mqttPort;

            if (GetServerAndPort(out mqttServer, out mqttPort))
            {
                var serviceAddr = mqttServer + ":" + "3000";
                lockService = new LockService(loggedInUser, serviceAddr);
                lockService.ListLocksAync((succeeded, locks, errCode) =>
                {
                    if (succeeded)
                    {
                        isListingRegisteredLocks = true;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            this.AddStatus("Got configured locks");
                            this.DeviceListTitle.Content = "Configured Locks";
                            this.deviceIds.Clear();

                            foreach(var lockid in locks) 
                            {
                                this.deviceIds.Add(lockid);
                            }
                        }));
                    }
                    else
                    {
                        this.AddStatus("Init failed to service, error: " + errCode);
                    }
                });
            }
        }

        private void OnInitializeDevice(object sender, RoutedEventArgs e)
        {
            string device = this.DiscoveredDevicesList.SelectedItem as string;
            string mqttServer = null;
            int mqttPort;

            if (!isListingRegisteredLocks && device != null)
            {
                if (GetServerAndPort(out mqttServer, out mqttPort))
                {
                    var serviceAddr = mqttServer + ":" + "3000";
                    lockService = new LockService(loggedInUser, serviceAddr);
                    lockService.DeviceRegistrationAsync(device, (succeeded, key, errCode) =>
                    {
                        if (succeeded)
                        {
                            this.AddStatus("initialized with service, setting device");
                            handler.InitDevice(device, mqttServer, mqttPort, key);
                        }
                        else
                        {
                            this.AddStatus("Init failed to service, error: " + errCode);
                        }
                    });
                }
            }
            else
            {
                AddStatus("Discover and select a lock");
            }
        }

        private void OnLogin(object sender, RoutedEventArgs e)
        {
            auth0.LoginAsync(new WindowWrapper(new WindowInteropHelper(this).Handle)).ContinueWith(loggedInResult =>
            {
                if (loggedInResult.IsFaulted)
                {
                    AddStatus("failure: " + loggedInResult.Exception.InnerException.ToString());
                }
                else
                {
                    loggedInUser = loggedInResult.Result;
                    AddStatus("\nlogged in to Auth0 as: " 
                        + (string)loggedInUser.Profile["email"] + 
                        " id: " +  (string) loggedInUser.Profile["user_id"]);
 
                    lockService = new LockService(loggedInUser, MqttServerText.Text + ":" + "3000");
                    this.LoginButton.Visibility = System.Windows.Visibility.Hidden;
                    InitiateDiscover();
                    this.InitializeButton.IsEnabled = true;
                    this.FindDevicesButton.IsEnabled = true;
                    this.ListCurrentLocksButton.IsEnabled = true;
                    this.ClearLockButton.IsEnabled = true;
                }
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnFindDevices(object sender, RoutedEventArgs e)
        {
            isListingRegisteredLocks = false;
            this.deviceIds.Clear();
            handler.QueryDevices();
        }

        private void AddStatus(string status)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                logs.Insert(0, status);
            }));
        }

        private void InitiateDiscover()
        {
            handler = new LockInitHandler(
                device =>
                {
                    Dispatcher.BeginInvoke(new Action(() => {
                        if (this.deviceIds.Contains(device) == false)
                        {
                            this.deviceIds.Add(device);
                        }
                    }));
                },
                AddStatus
             );

            this.DiscoveredDevicesList.ItemsSource = deviceIds;
            handler.QueryDevices();
        }


        private bool GetServerAndPort(out string mqttServer, out int mqttPort)
        {
            string device = this.DiscoveredDevicesList.SelectedItem as string;
            mqttServer = MqttServerText.Text;
            var mqttPortStr = MqttPortText.Text;
            bool isValidPort = int.TryParse(mqttPortStr, out mqttPort);

            if (!string.IsNullOrWhiteSpace(mqttServer) && isValidPort)
            {
                return true;
            }

            AddStatus("Server and port not configured");

            return false;
        }
    }
}
