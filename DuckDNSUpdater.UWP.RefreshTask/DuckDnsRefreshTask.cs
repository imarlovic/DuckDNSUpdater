using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Notifications;

namespace DuckDNSUpdater.UWP.RefreshTask
{
    public sealed class DuckDnsRefreshTask : IBackgroundTask
    {
        
        private BackgroundTaskDeferral _deferral;
        private Guid _instanceId;
        private static HttpClient _httpClient;
        private static HttpClient HttpClient 
        {
            get
            {
                if (_httpClient is null)
                {
                    _httpClient = new HttpClient()
                    {
                        BaseAddress = new Uri("https://www.duckdns.org"),
                    };
                }

                return _httpClient;
            }
        }
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            _instanceId = taskInstance.InstanceId;

            await TryUpdateAsync(taskInstance.Task);

            _deferral.Complete();
        }

        public static IAsyncAction TryUpdateAsyncWithAsyncAction(IBackgroundTaskRegistration taskRegistration)
        {
            return TryUpdateAsync(taskRegistration).AsAsyncAction();
        }

        private static async Task TryUpdateAsync(IBackgroundTaskRegistration taskRegistration)
        {
            var configuration = await GetConfigurationAsync();

            if (configuration.IsValid)
            {
                try
                {
                    var response = await HttpClient.GetAsync($"/update?domains={configuration.DomainNames}&token={configuration.Token}&verbose=true");
                    var content = await response.Content.ReadAsStringAsync();

                    var updateResponse = new UpdateResponse(content);

                    if (updateResponse.Success == UpdateResponseSuccess.OK)
                    {
                        switch (updateResponse.Status)
                        {
                            case UpdateResponseStatus.UPDATED:
                                ShowToastNotification("Successfully updated Public IP");
                                break;
                            case UpdateResponseStatus.NOCHANGE:
                                break;
                        }
                    }
                    else
                    {
                        ShowToastNotification($"Failed to update Public IP | {content}");
                    }
                }
                catch (Exception e)
                {
                    ShowToastNotification($"Update request failed | {e.Message}");
                }
            }
            else
            {
                ShowToastNotification("Configuration invalid", 60 * 24);

                taskRegistration?.Unregister(false);
            }
        }

        private static async Task<DuckDNSUpdaterConfiguration> GetConfigurationAsync()
        {
            var configuration = new DuckDNSUpdaterConfiguration();

            var localFolder = ApplicationData.Current.LocalFolder;
            var settingsFile = await localFolder.GetFileAsync("duck_dns_updater_config.cfg");
            if (settingsFile != null)
            {
                var settings = await FileIO.ReadTextAsync(settingsFile);
                var settingsValues = settings.Split("|");

                configuration.DomainNames = settingsValues[0];
                configuration.Token = settingsValues[1];
            }
            else
            {
                configuration.DomainNames = null;
                configuration.Token = null;
            }

            return configuration;
        }

        private static void ShowToastNotification(string content, int expirationTimeInMinutes = 15)
        {
            // In a real app, these would be initialized with actual data
            string title = "Duck DNS Updater";

            // Construct the visuals of the toast
            ToastVisual visual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children = {
                        new AdaptiveText()
                        {
                            Text = title
                        },
                        new AdaptiveText()
                        {
                            Text = content
                        },
                    }
                }
            };

            // Now we can construct the final toast content
            ToastContent toastContent = new ToastContent()
            {
                Visual = visual
            };

            // And create the toast notification
            var toast = new ToastNotification(toastContent.GetXml())
            {
                ExpirationTime = DateTime.Now.AddMinutes(expirationTimeInMinutes)
            };

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }

    public sealed class UpdateResponse
    {
        public UpdateResponse(string response)
        {
            var lines = response.Split('\n');

            Success = (UpdateResponseSuccess)Enum.Parse(typeof(UpdateResponseSuccess), lines[0]);
            IPv4 = lines[1];
            IPv6 = lines[2];
            Status = (UpdateResponseStatus)Enum.Parse(typeof(UpdateResponseStatus), lines[3]);
        }
        public UpdateResponseSuccess Success { get; set; }
        public string IPv4 { get; set; }
        public string IPv6 { get; set; }
        public UpdateResponseStatus Status { get; set; }
    }

    public enum UpdateResponseSuccess
    {
        OK,
        KO
    }

    public enum UpdateResponseStatus
    {
        UPDATED,
        NOCHANGE
    }

    public sealed class DuckDNSUpdaterConfiguration 
    {
        public string DomainNames { get; set; }
        public string Token { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(DomainNames) && !string.IsNullOrEmpty(Token);
    }

    public sealed class UpdateInterval
    {
        public string Label { get; set; }
        public uint Value { get; set; }
    }
}
