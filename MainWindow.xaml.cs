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
        private string encryptKey = null;
        ObservableCollection<String> deviceIds = new ObservableCollection<String>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnInitializeDevice(object sender, RoutedEventArgs e)
        {
            string device = this.DiscoveredDevicesList.SelectedItem as string;

            if (device != null)
            {
                var mqttServer = MqttServerText.Text;
                var mqttPortStr = MqttPortText.Text;
                int mqttPortVal;
                bool isValidPort = int.TryParse(mqttPortStr, out mqttPortVal);

                if(!string.IsNullOrWhiteSpace(mqttServer) && isValidPort)
                {
                    handler.InitDevice(device, mqttServer, mqttPortVal);
                }
                else
                {
                    AddStatus("Enter valid port and ip or domain for mqtt server");
                }
            }
            else
            {
                lockService = new LockService(loggedInUser, MqttServerText.Text + ":" + "3000");
                string dev = "123";
                lockService.StartDeviceRegistration(dev, (result, key) =>
                {
                    encryptKey = key;
                });

                AddStatus("A device was not selected \n");
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
                    AddStatus("\nlogged in to Auth0 as: " +(string)loggedInResult.Result.Profile["email"] + "\n");
                    loggedInUser = loggedInResult.Result;
                    lockService = new LockService(loggedInUser, MqttServerText.Text + ":" + "3000");
                    this.LoginButton.Visibility = System.Windows.Visibility.Hidden;
                    InitiateDiscover();
                    this.InitializeButton.IsEnabled = true;
                    this.FindDevicesButton.IsEnabled = true;
                }
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnFindDevices(object sender, RoutedEventArgs e)
        {
            handler.QueryDevices();
        }

        private void AddStatus(string status)
        {
            this.StatusOutput.Text = this.StatusOutput.Text + status + "\n";
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
                message =>
                {
                    Dispatcher.BeginInvoke(new Action(() => {
                        AddStatus(message);
                    }));
                });
            this.DiscoveredDevicesList.ItemsSource = deviceIds;
            handler.QueryDevices();
        }
    }
}
