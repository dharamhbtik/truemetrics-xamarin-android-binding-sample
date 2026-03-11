using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TrueMetricsSample.Helpers;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace TrueMetricsSample.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly StringBuilder _log = new StringBuilder();
        
        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                }
            }
        }
        
        public bool IsNotBusy => !IsBusy;

        // Dependencies
        ITrueMetricsExerciseService Svc => DependencyService.Get<ITrueMetricsExerciseService>();

        // Commands
        public ICommand InitCommand { get; }
        public ICommand GetDeviceIdCommand { get; }
        public ICommand GetSensorStatisticsCommand { get; }
        public ICommand EnableSensorsCommand { get; }
        public ICommand DisableSensorsCommand { get; }
        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand MetadataDemoCommand { get; }
        public ICommand DeinitCommand { get; }
        public ICommand RunAllCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel()
        {
            InitCommand = new Command(async () => await RunActionAsync("Init requested...", svc => svc.InitAsync(Constants.TrueMetricsApiKey)));
            GetDeviceIdCommand = new Command(async () => await RunActionAsync("Getting Device Id...", svc => svc.GetDeviceIdAsync()));
            GetSensorStatisticsCommand = new Command(async () => await RunActionAsync("Getting sensor statistics...", svc => svc.GetSensorStatisticsAsync()));
            EnableSensorsCommand = new Command(async () => await RunActionAsync("Enabling all sensors...", svc => svc.EnableSensorsAsync()));
            DisableSensorsCommand = new Command(async () => await RunActionAsync("Disabling all sensors...", svc => svc.DisableSensorsAsync()));
            StartRecordingCommand = new Command(async () => await RunActionAsync("Starting recording...", svc => svc.StartRecordingAsync()));
            StopRecordingCommand = new Command(async () => await RunActionAsync("Stopping recording...", svc => svc.StopRecordingAsync()));
            MetadataDemoCommand = new Command(async () => await RunActionAsync("Running metadata demo...", svc => svc.MetadataDemoAsync()));
            DeinitCommand = new Command(async () => await RunActionAsync("Deinitializing SDK...", svc => svc.DeinitializeAsync()));
            RunAllCommand = new Command(async () => await RunActionAsync("Running full exercise...", svc => svc.RunAllAsync(Constants.TrueMetricsApiKey)));

            CopyLogCommand = new Command(async () => await OnCopyLogClickedAsync());
            ClearLogCommand = new Command(OnClearLogClicked);

            Append("Ready.");
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                var svc = Svc;
                if (svc == null) return;
                var status = await svc.GetStatusAsync();
                if (!string.IsNullOrWhiteSpace(status))
                    StatusText = status.TrimEnd();
            }
            catch
            {
            }
        }

        private async Task RunActionAsync(string startMessage, Func<ITrueMetricsExerciseService, Task<string>> action)
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                if (!string.IsNullOrEmpty(startMessage))
                    Append(startMessage);

                var svc = Svc;
                if (svc == null)
                {
                    Append("ERROR: ITrueMetricsExerciseService not registered. Check TrueMetricsSample.Droid implementation.");
                    return;
                }

                var result = await action(svc);
                if (!string.IsNullOrWhiteSpace(result))
                    Append(result.TrimEnd());

                await RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                Append("EXCEPTION: " + ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OnCopyLogClickedAsync()
        {
            try
            {
                await Clipboard.SetTextAsync(_log.ToString());
                Append("Log copied to clipboard.");
            }
            catch (Exception ex)
            {
                Append("EXCEPTION: " + ex);
            }
        }

        private void OnClearLogClicked()
        {
            _log.Clear();
            LogText = string.Empty;
            Append("Log cleared.");
        }

        private void Append(string msg)
        {
            _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            LogText = _log.ToString();
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}
