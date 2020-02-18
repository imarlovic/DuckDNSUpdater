using System;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Popups;
using Windows.Storage;
using DuckDNSUpdater.UWP.RefreshTask;
using System.IO;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DuckDNSUpdater
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly ObservableCollection<UpdateInterval> Intervals = new ObservableCollection<UpdateInterval>()
        {
            new UpdateInterval { Label = "15 minutes", Value = 15u },
            new UpdateInterval { Label = "30 minutes", Value = 30u },
            new UpdateInterval { Label = "1 hour", Value = 60u },
            new UpdateInterval { Label = "2 hours", Value = 2u * 60u },
            new UpdateInterval { Label = "5 hours", Value = 5u * 60u },
            new UpdateInterval { Label = "10 hours", Value = 10u * 60u },
            new UpdateInterval { Label = "1 day", Value = 24u * 60u },
        };

        private UpdateInterval SelectedInterval = null;
        private string DomainNames = null;
        private string Token = null;

        private bool OkButtonEnabled = false;

        private const string BackgroundTaskName = "duck_dns_updater_refresh_task";

        private IBackgroundTaskRegistration RefreshTask;
        private TimeTrigger RefreshTrigger;
        public MainPage()
        {
            this.InitializeComponent();

            LoadConfiguration();

            RefreshTask = GetActiveRefreshTaskRegistration();

            if(RefreshTask != null)
            {
                RefreshTrigger = (TimeTrigger)((BackgroundTaskRegistration)RefreshTask).Trigger;
                if(RefreshTrigger != null)
                {
                    SelectedInterval = Intervals.FirstOrDefault(i => i.Value == RefreshTrigger.FreshnessTime);
                }
            }

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(500, 168));
            ApplicationView.PreferredLaunchViewSize = new Size(500, 168);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }
        private async void LoadConfiguration()
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                var settingsFile = await localFolder.GetFileAsync("duck_dns_updater_config.cfg");
                if (settingsFile != null)
                {
                    var settings = await FileIO.ReadTextAsync(settingsFile);
                    var settingsValues = settings.Split("|");

                    DomainNames = settingsValues[0];
                    Token = settingsValues[1];
                    SelectedInterval = Intervals.FirstOrDefault(i => i.Value == uint.Parse(settingsValues[2]));

                    OkButton.Focus(FocusState.Programmatic);
                }
            }
            catch (FileNotFoundException)
            {

            }
        }
        private async void SaveConfiguration()
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var settingsFile = await localFolder.CreateFileAsync("duck_dns_updater_config.cfg", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(settingsFile, $"{DomainNames}|{Token}|{SelectedInterval.Value}");
        }
        private IBackgroundTaskRegistration GetActiveRefreshTaskRegistration()
        {
            var registration = BackgroundTaskRegistration.AllTasks.FirstOrDefault(btr => btr.Value.Name == BackgroundTaskName);

            return registration.Value;
        }
        private async Task<BackgroundTaskRegistration> RegisterRefreshTaskAsync()
        {
            // If the user denies access, the task will not run.
            var access = await BackgroundExecutionManager.RequestAccessAsync();

            if (access != BackgroundAccessStatus.AlwaysAllowed && access != BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
            {
                return null;
            }
                

            RefreshTrigger = new TimeTrigger(SelectedInterval.Value, false);

            var existingTask = GetActiveRefreshTaskRegistration();

            if(existingTask != null)
            {
                existingTask.Unregister(true);
            }

            // Register the background task.
            var builder = new BackgroundTaskBuilder
            {
                Name = BackgroundTaskName,
                TaskEntryPoint = typeof(DuckDNSUpdater.UWP.RefreshTask.DuckDnsRefreshTask).FullName
            };

            builder.SetTrigger(RefreshTrigger);

            //builder.AddCondition(condition);

            BackgroundTaskRegistration task = builder.Register();

            return task;
        }

        private async Task RegisterStartupAsync()
        {
            StartupTask startupTask = await StartupTask.GetAsync("DuckDNSUpdaterStartup"); // Pass the task ID you specified in the appxmanifest file
            
            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    // Task is disabled but can be enabled.
                    StartupTaskState newState = await startupTask.RequestEnableAsync(); // ensure that you are on a UI thread when you call RequestEnableAsync()
                    Debug.WriteLine("Request to enable startup, result = {0}", newState);
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    MessageDialog dialog = new MessageDialog(
                        "You have disabled this app's ability to run " +
                        "as soon as you sign in, but if you change your mind, " +
                        "you can enable this in the Startup tab in Task Manager.",
                        "Duck DNS Updater");
                    await dialog.ShowAsync();
                    break;
                case StartupTaskState.DisabledByPolicy:
                    Debug.WriteLine("Startup disabled by group policy, or not supported on this device");
                    break;
                case StartupTaskState.Enabled:
                    Debug.WriteLine("Startup is enabled.");
                    break;
            }
        }

        private async void Button_ClickAsync(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            RefreshTask = await RegisterRefreshTaskAsync();
            Debug.WriteLine($"Registered refresh task that will run every {SelectedInterval.Label}");

            await DuckDnsRefreshTask.TryUpdateAsyncWithAsyncAction(RefreshTask);

            Application.Current.Exit();
        }

        private void CheckButtonState(object sender, TextChangedEventArgs e)
        {
            OkButton.IsEnabled = !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(DomainNames) && SelectedInterval != null;
        }

        private void CheckButtonState(object sender, SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(DomainNames) && SelectedInterval != null;
        }
    }
}
