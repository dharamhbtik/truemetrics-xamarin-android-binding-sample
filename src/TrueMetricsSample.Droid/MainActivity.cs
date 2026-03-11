using Android.App;
using Android.Content.PM;
using Android.OS;
using Android;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

namespace TrueMetricsSample.Droid
{
    [Activity(Label = "TrueMetricsSample", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round", Theme = "@style/MainTheme", MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public class MainActivity : FormsAppCompatActivity
    {
        const int RuntimePermissionsRequestCode = 9001;
        const int BackgroundLocationRequestCode = 9002;

        bool _foregroundPermissionsGranted;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Forms.Init(this, savedInstanceState);

            LoadApplication(new TrueMetricsSample.App());

            // Request foreground permissions first — matches native sample which
            // handles ACCESS_BACKGROUND_LOCATION separately via askPermissions callback
            RequestForegroundPermissions();
        }

        void RequestForegroundPermissions()
        {
            if ((int)Build.VERSION.SdkInt < 23)
            {
                _foregroundPermissionsGranted = true;
                return;
            }

            var toRequest = new System.Collections.Generic.List<string>();

            void AddIfNotGranted(string permission)
            {
                if (ContextCompat.CheckSelfPermission(this, permission) != Permission.Granted)
                    toRequest.Add(permission);
            }

            AddIfNotGranted(Manifest.Permission.AccessCoarseLocation);
            AddIfNotGranted(Manifest.Permission.AccessFineLocation);
            AddIfNotGranted(Manifest.Permission.ActivityRecognition);
            AddIfNotGranted(Manifest.Permission.ReadPhoneState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                AddIfNotGranted(Manifest.Permission.PostNotifications);

            if (toRequest.Count > 0)
            {
                // Request foreground permissions — background location will be
                // requested AFTER these are granted (Android requirement)
                ActivityCompat.RequestPermissions(this, toRequest.ToArray(), RuntimePermissionsRequestCode);
            }
            else
            {
                _foregroundPermissionsGranted = true;
                // Foreground already granted, now try background
                RequestBackgroundLocationIfNeeded();
            }
        }

        void RequestBackgroundLocationIfNeeded()
        {
            if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.Q)
                return;

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessBackgroundLocation) == Permission.Granted)
                return;

            // On Android 10+, background location must be requested AFTER foreground
            // location has been granted, and in a separate request.
            // The native sample shows a dialog explaining why background location is needed.
            if (_foregroundPermissionsGranted)
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.AccessBackgroundLocation }, BackgroundLocationRequestCode);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == RuntimePermissionsRequestCode)
            {
                // Check if fine or coarse location was granted
                bool locationGranted = false;
                for (int i = 0; i < permissions.Length; i++)
                {
                    if ((permissions[i] == Manifest.Permission.AccessFineLocation ||
                         permissions[i] == Manifest.Permission.AccessCoarseLocation) &&
                        grantResults[i] == Permission.Granted)
                    {
                        locationGranted = true;
                    }
                }

                _foregroundPermissionsGranted = true;

                // Now request background location separately (Android 10+ requirement)
                if (locationGranted)
                {
                    RequestBackgroundLocationIfNeeded();
                }
            }
        }
    }
}
