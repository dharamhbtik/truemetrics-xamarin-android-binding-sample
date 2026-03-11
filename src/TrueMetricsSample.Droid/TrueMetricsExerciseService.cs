using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.App;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Xamarin.Forms;
using TrueMetricsSample;
using IO.Truemetrics.Truemetricssdk;
using IO.Truemetrics.Truemetricssdk.Config;
using IO.Truemetrics.Truemetricssdk.Engine.State;
using Android;
using Android.Runtime;

[assembly: Dependency(typeof(TrueMetricsSample.Droid.TrueMetricsExerciseService))]
namespace TrueMetricsSample.Droid
{
    public class TrueMetricsExerciseService : ITrueMetricsExerciseService
    {
        static TruemetricsSdk _sdk;
        static ForegroundNotificationFactoryImpl _notifFactory;

        static async Task<T> RunSdkAsync<T>(Func<T> func, int timeoutMs)
        {
            var task = Task.Run(func);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != task)
                throw new TimeoutException("SDK call timed out (" + timeoutMs + "ms)");
            return await task.ConfigureAwait(false);
        }

        static Task RunSdkAsync(Action action, int timeoutMs)
        {
            return RunSdkAsync(() =>
            {
                action();
                return true;
            }, timeoutMs);
        }

        static string SnapshotStateUnsafe()
        {
            if (_sdk == null) return "SDK=null";
            try
            {
                return "" +
                       "DeviceId='" + (_sdk.DeviceId ?? string.Empty) + "'" +
                       ", AllSensorsEnabled=" + _sdk.AllSensorsEnabled +
                       ", IsRecordingInProgress=" + _sdk.IsRecordingInProgress +
                       ", IsRecordingStopped=" + _sdk.IsRecordingStopped +
                       ", RecordingStartTime=" + _sdk.RecordingStartTime;
            }
            catch (Exception ex)
            {
                return "SnapshotError: " + ex.Message;
            }
        }

        static Task<string> SnapshotStateAsync()
        {
            return Task.Run(() => SnapshotStateUnsafe());
        }

        /// <summary>
        /// Ensure all required runtime permissions are granted BEFORE initializing the SDK.
        /// This is critical — the native sample uses StatusListener.askPermissions() callback,
        /// but that interface is not available in the Xamarin binding. We must pre-grant permissions.
        /// </summary>
        static void EnsurePermissionsGranted(StringBuilder log)
        {
            var ctx = Xamarin.Essentials.Platform.CurrentActivity ?? (Context)Android.App.Application.Context;
            var activity = ctx as Activity;

            if (activity == null)
            {
                log.AppendLine("WARNING: No activity context available for permission requests.");
                return;
            }

            if ((int)Build.VERSION.SdkInt < 23)
            {
                log.AppendLine("API < 23, permissions auto-granted.");
                return;
            }

            var needed = new List<string>();

            void CheckAndAdd(string perm)
            {
                if (ContextCompat.CheckSelfPermission(activity, perm) != Android.Content.PM.Permission.Granted)
                    needed.Add(perm);
            }

            CheckAndAdd(Manifest.Permission.AccessCoarseLocation);
            CheckAndAdd(Manifest.Permission.AccessFineLocation);
            CheckAndAdd(Manifest.Permission.ActivityRecognition);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                CheckAndAdd(Manifest.Permission.PostNotifications);

            if (needed.Count > 0)
            {
                log.AppendLine($"Requesting {needed.Count} permissions: {string.Join(", ", needed)}");
                ActivityCompat.RequestPermissions(activity, needed.ToArray(), 9001);
            }
            else
            {
                log.AppendLine("All foreground permissions already granted.");
            }

            // Background location must be requested separately after foreground location
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                if (ContextCompat.CheckSelfPermission(activity, Manifest.Permission.AccessBackgroundLocation) != Android.Content.PM.Permission.Granted)
                {
                    log.AppendLine("Background location not yet granted (must be granted via Settings on Android 10+).");
                }
                else
                {
                    log.AppendLine("Background location already granted.");
                }
            }
        }

        public async Task<string> InitAsync(string apiKey)
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                var ctx = Xamarin.Essentials.Platform.CurrentActivity ?? (Context)Android.App.Application.Context;
                Log("Context acquired: " + ctx.PackageName + ", IsActivity=" + (ctx is Android.App.Activity));

                // Log permission states
                if ((int)Build.VERSION.SdkInt >= 23)
                {
                    Log("PermissionFineLocation: " + (ctx.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) == Android.Content.PM.Permission.Granted));
                    Log("PermissionCoarseLocation: " + (ctx.CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation) == Android.Content.PM.Permission.Granted));
                    Log("PermissionActivityRecognition: " + (ctx.CheckSelfPermission(Android.Manifest.Permission.ActivityRecognition) == Android.Content.PM.Permission.Granted));

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                        Log("PermissionBackgroundLocation: " + (ctx.CheckSelfPermission(Android.Manifest.Permission.AccessBackgroundLocation) == Android.Content.PM.Permission.Granted));

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                        Log("PermissionPostNotifications: " + (ctx.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted));
                }

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_WITH_REAL_API_KEY")
                {
                    Log("ERROR: Please set a real API key in the UI.");
                    return sb.ToString();
                }

                // Ensure permissions are granted before SDK init — this is critical!
                // The native sample does this via StatusListener.askPermissions() which
                // we don't have access to in the binding.
                EnsurePermissionsGranted(sb);

                // Build config WITHOUT ExplicitStart — let the SDK auto-start like the native sample.
                // The native sample uses Config(apiKey, foregroundNotification, debug=true) which
                // auto-starts recording. The SdkConfiguration.Builder equivalent is to use
                // AutoStartOnInit (0) rather than ExplicitStart (-1).
                var configBuilder = new SdkConfiguration.Builder(apiKey)
                    .DelayAutoStartRecording(SdkConfiguration.AutoStartOnInit);
                _notifFactory = new ForegroundNotificationFactoryImpl();
                configBuilder.ForegroundNotificationFactory(_notifFactory);
                var config = configBuilder.Build();

                Log($"Config built: ApiKey length={config.ApiKey?.Length ?? 0}, DelayAutoStart={config.DelayAutoStartRecording}");

                await RunSdkAsync(() => { _sdk = TruemetricsSdk.Init(ctx, config); }, timeoutMs: 5000).ConfigureAwait(false);
                Log("SDK initialized via TruemetricsSdk.Init().");

                var instance = TruemetricsSdk.Instance;
                Log("SDK instance resolved: " + (instance != null));

                // Wait for initialization to complete — the SDK needs time to fetch config from
                // the server and set up sensors. In the native sample, this is handled by
                // StatusListener.onStateChange(State.INITIALIZED).
                Log("Waiting for SDK to fully initialize (up to 15s)...");

                try
                {
                    var devId = await RunSdkAsync(() => _sdk?.DeviceId, timeoutMs: 2000).ConfigureAwait(false);
                    Log("DeviceId(after init, 2s timeout): " + (devId ?? string.Empty));
                }
                catch (TimeoutException)
                {
                    Log("WARNING: DeviceId read timed out after init (2s)." );
                }

                Log("State(after init): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return sb.ToString();
        }

        public async Task<string> GetStatusAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                var hasSdk = _sdk != null;
                Log("SdkInitialized: " + hasSdk);

                if (!hasSdk)
                    return sb.ToString();

                Log("State: " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return sb.ToString();
        }

        public async Task<string> GetSensorStatisticsAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                Log("State(before SensorStatistics): " + await SnapshotStateAsync().ConfigureAwait(false));

                global::System.Collections.Generic.IList<global::IO.Truemetrics.Truemetricssdk.Engine.Stats.SensorStatistics> stats;
                try
                {
                    stats = await RunSdkAsync(() => _sdk.SensorStatistics, timeoutMs: 2000).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    Log("WARNING: SensorStatistics read timed out (2s).");
                    return sb.ToString();
                }
                if (stats == null)
                {
                    Log("SensorStatistics: null");
                    return sb.ToString();
                }

                Log("SensorStatistics count: " + stats.Count);
                for (var i = 0; i < stats.Count; i++)
                {
                    var stat = stats[i];
                    if (stat == null)
                    {
                        Log("#" + i + ": null");
                        continue;
                    }

                    Log("--- Sensor #" + (i + 1) + " ---");
                    Log("Sensor: " + (stat.SensorName?.ToString() ?? string.Empty));
                    Log("Configured: " + stat.ConfiguredFrequencyHz + " Hz");
                    Log("Actual: " + stat.ActualFrequencyHz + " Hz");
                    Log("Quality: " + stat.Quality);
                }

                Log("State(after SensorStatistics): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return sb.ToString();
        }

        public async Task<string> GetDeviceIdAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                var ctx = Android.App.Application.Context;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    var postNotif = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(ctx, Manifest.Permission.PostNotifications);
                    Log("PostNotificationsGranted: " + (postNotif == Android.Content.PM.Permission.Granted));
                }

                // DeviceId getter may block (e.g., binder/IO) and can cause ANR if called on the UI thread.
                // Retrieve it on a background thread with a short timeout.
                var readTask = Task.Run(() => _sdk.DeviceId);
                var completed = await Task.WhenAny(readTask, Task.Delay(2000)).ConfigureAwait(false);
                if (completed != readTask)
                {
                    Log("WARNING: DeviceId read timed out (2s). Skipping to avoid ANR.");
                    return sb.ToString();
                }

                var deviceId = await readTask.ConfigureAwait(false);
                Log("DeviceId: " + (deviceId ?? string.Empty));
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return sb.ToString();
        }

        public async Task<string> EnableSensorsAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                await RunSdkAsync(() => { _sdk.AllSensorsEnabled = true; }, timeoutMs: 2000).ConfigureAwait(false);
                Log("AllSensorsEnabled property set to true.");
                Log("AllSensorsEnabled: " + await RunSdkAsync(() => _sdk.AllSensorsEnabled, timeoutMs: 2000).ConfigureAwait(false));
                Log("State(after EnableSensors): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("EnableSensors threw: " + ex.GetType().Name + " - " + ex.Message);
            }

            return sb.ToString();
        }

        public async Task<string> DisableSensorsAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                await RunSdkAsync(() => { _sdk.AllSensorsEnabled = false; }, timeoutMs: 2000).ConfigureAwait(false);
                Log("AllSensorsEnabled property set to false.");
                Log("AllSensorsEnabled: " + await RunSdkAsync(() => _sdk.AllSensorsEnabled, timeoutMs: 2000).ConfigureAwait(false));
                Log("State(after DisableSensors): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("DisableSensors threw: " + ex.GetType().Name + " - " + ex.Message);
            }

            return sb.ToString();
        }

        public async Task<string> StartRecordingAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                Log("State(before StartRecording): " + await SnapshotStateAsync().ConfigureAwait(false));

                // Check if already recording
                var alreadyRecording = await RunSdkAsync(() => _sdk.IsRecordingInProgress, timeoutMs: 2000).ConfigureAwait(false);
                if (alreadyRecording)
                {
                    Log("Recording is already in progress (auto-started by SDK).");
                    Log("State(already recording): " + await SnapshotStateAsync().ConfigureAwait(false));
                    return sb.ToString();
                }

                var sensorsEnabled = await RunSdkAsync(() => _sdk.AllSensorsEnabled, timeoutMs: 2000).ConfigureAwait(false);
                if (!sensorsEnabled)
                {
                    Log("AllSensorsEnabled was false; set to true.");
                    await RunSdkAsync(() => { _sdk.AllSensorsEnabled = true; }, timeoutMs: 2000).ConfigureAwait(false);
                    Log("AllSensorsEnabled(now): " + await RunSdkAsync(() => _sdk.AllSensorsEnabled, timeoutMs: 2000).ConfigureAwait(false));
                }

                await RunSdkAsync(() => _sdk.StartRecording(), timeoutMs: 2000).ConfigureAwait(false);
                Log("StartRecording invoked.");

                bool started;
                try
                {
                    started = await RunSdkAsync(() => _sdk.IsRecordingInProgress || _sdk.RecordingStartTime > 0, timeoutMs: 2000).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    started = false;
                }

                Log("RecordingStarted(observed=" + started + ")");
                Log("State(after StartRecording wait): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("StartRecording threw: " + ex.GetType().Name + " - " + ex.Message);
            }

            return sb.ToString();
        }

        public async Task<string> StopRecordingAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return sb.ToString();
                }

                Log("State(before StopRecording): " + await SnapshotStateAsync().ConfigureAwait(false));

                await RunSdkAsync(() => _sdk.StopRecording(), timeoutMs: 2000).ConfigureAwait(false);
                Log("StopRecording invoked.");

                bool stopped;
                try
                {
                    stopped = await RunSdkAsync(() => _sdk.IsRecordingStopped || !_sdk.IsRecordingInProgress, timeoutMs: 2000).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    stopped = false;
                }

                Log("RecordingStopped(observed=" + stopped + ")");
                Log("State(after StopRecording wait): " + await SnapshotStateAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log("StopRecording threw: " + ex.GetType().Name + " - " + ex.Message);
            }

            return sb.ToString();
        }

        public Task<string> MetadataDemoAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("ERROR: SDK is not initialized. Tap Init first.");
                    return Task.FromResult(sb.ToString());
                }

                var metadataTemplateName = "driver";
                var tag = "trip";

                _sdk.CreateMetadataTemplate(metadataTemplateName, new System.Collections.Generic.Dictionary<string, string>
                {
                    { "fleet", "A" },
                    { "region", "EU" }
                });
                Log("Created metadata template: " + metadataTemplateName);

                var templateNames = _sdk.MetadataTemplateNames;
                Log("MetadataTemplateNames count: " + (templateNames?.Count ?? 0));

                var template = _sdk.GetMetadataTemplate(metadataTemplateName);
                Log("GetMetadataTemplate keys: " + (template?.Count ?? 0));

                var created = _sdk.CreateMetadataFromTemplate(tag, metadataTemplateName);
                Log("CreateMetadataFromTemplate => " + created);

                _sdk.AppendToMetadataTag(tag, "vehicleId", "VH-001");
                _sdk.AppendToMetadataTag(tag, new System.Collections.Generic.Dictionary<string, string>
                {
                    { "shift", "morning" },
                    { "route", "R12" }
                });

                var byTag = _sdk.GetMetadataByTag(tag);
                Log("GetMetadataByTag count: " + (byTag?.Count ?? 0));

                _sdk.LogMetadata(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "event", "app_open" },
                    { "ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
                });
                Log("LogMetadata sent.");

                var loggedByTag = _sdk.LogMetadataByTag(tag);
                Log("LogMetadataByTag => " + loggedByTag);

                var removedKey = _sdk.RemoveFromMetadataTag(tag, "route");
                Log("RemoveFromMetadataTag(route) => " + removedKey);

                var removedTag = _sdk.RemoveMetadataTag(tag);
                Log("RemoveMetadataTag => " + removedTag);

                var removedTemplate = _sdk.RemoveMetadataTemplate(metadataTemplateName);
                Log("RemoveMetadataTemplate => " + removedTemplate);

                _sdk.ClearAllMetadata();
                Log("ClearAllMetadata done.");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return Task.FromResult(sb.ToString());
        }

        public Task<string> DeinitializeAsync()
        {
            var sb = new StringBuilder();
            void Log(string s) => sb.AppendLine(s);

            try
            {
                if (_sdk == null)
                {
                    Log("SDK is already null (not initialized or already deinitialized).");
                    return Task.FromResult(sb.ToString());
                }

                _sdk.Deinitialize();
                _sdk = null;
                Log("Deinitialize invoked.");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            return Task.FromResult(sb.ToString());
        }

        public async Task<string> RunAllAsync(string apiKey)
        {
            var sb = new StringBuilder();

            sb.AppendLine(await InitAsync(apiKey));
            sb.AppendLine(await GetDeviceIdAsync());
            sb.AppendLine(await MetadataDemoAsync());
            sb.AppendLine(await StartRecordingAsync());
            sb.AppendLine(await StopRecordingAsync());
            sb.AppendLine(await DeinitializeAsync());

            sb.AppendLine("Exercise completed.");
            return sb.ToString();
        }

        public Task<string> RunAsync(string apiKey)
        {
            return RunAllAsync(apiKey);
        }

        sealed class ForegroundNotificationFactoryImpl : Java.Lang.Object, IForegroundNotificationFactory
        {
            const string ChannelId = "truemetrics_fg";

            public Notification CreateNotification(Context context)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== CreateNotification called! ===");
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        var channel = new NotificationChannel(ChannelId, "TrueMetrics Service", NotificationImportance.Low)
                        {
                            Description = "TrueMetrics foreground service"
                        };
                        channel.SetShowBadge(false);

                        var nm = (NotificationManager)context.GetSystemService(Context.NotificationService);
                        nm.CreateNotificationChannel(channel);
                    }

                    var builder = new NotificationCompat.Builder(context, ChannelId)
                        .SetContentTitle("TrueMetrics")
                        .SetContentText("Recording sensors")
                        .SetSmallIcon(context.ApplicationInfo.Icon)
                        .SetOngoing(true);

                    var notif = builder.Build();
                    System.Diagnostics.Debug.WriteLine("=== CreateNotification success! ===");
                    return notif;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("=== CreateNotification CRASH: " + ex);
                    throw;
                }
            }
        }
    }
}
