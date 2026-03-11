# TrueMetrics Xamarin.Android Binding Sample (Dependency + Runtime Troubleshooting)

This repository contains a **Xamarin.Forms + Xamarin.Android** sample solution used to integrate the **TrueMetrics Android SDK** via a classic **Xamarin.Android Binding** project.

It exists primarily to:

- reproduce and fix **dependency alignment** issues when consuming the TrueMetrics Android AAR in Xamarin
- provide a runnable sample app for investigating runtime issues (blank `DeviceId`, recording not starting, sensor stats empty)
- document the exact set of AndroidX / Kotlin / Ktor / Coroutines / Koin dependencies required by the TrueMetrics SDK

> The native Android demo (Gradle) works as expected, but the same SDK may behave differently when consumed via Xamarin binding. This repo helps isolate those differences.

## What’s inside

- `TrueMetricsBindingSample.sln`
- `src/TrueMetricsSdk.Binding/`  
  Xamarin **binding project** for the vendor `truemetricssdk-1.4.3.aar`.
- `src/TrueMetricsSample/`  
  Xamarin.Forms UI project.
- `src/TrueMetricsSample.Droid/`  
  Xamarin.Android application hosting the binding and packaging Java/AAR dependencies.

## Requirements

- macOS
- Visual Studio for Mac (or msbuild via Mono)
- Xamarin.Android tooling installed
- Android SDK installed (API 33 recommended)
- A physical Android device is recommended (background location + sensors)

## Configure API key

The app uses the API key from:

- `src/TrueMetricsSample/Helpers/Constants.cs` (`Constants.TrueMetricsApiKey`)

Do **not** hardcode real keys for public repos. For sharing publicly, replace it with a placeholder.

## Build

From the repository root:

```bash
msbuild TrueMetricsBindingSample.sln -t:Build -p:Configuration=Debug -m
```

## Run

Deploy `TrueMetricsSample.Droid` to a device/emulator from the IDE.

### Runtime permissions

The sample requests:

- `ACCESS_FINE_LOCATION`
- `ACCESS_COARSE_LOCATION`
- `ACTIVITY_RECOGNITION`
- `READ_PHONE_STATE`
- Android 13+: `POST_NOTIFICATIONS`
- Android 10+: `ACCESS_BACKGROUND_LOCATION` (requested separately after foreground location)

> Android may require granting background location from Settings depending on OS version/vendor ROM.

## Using the app

The sample does **not** auto-run SDK calls on startup.

Suggested button sequence:

1. `Init`
2. `Enable Sensors`
3. `Start Recording`
4. `Get Device Id`
5. `Get Sensor Statistics`

The UI shows:

- **Live status** (SDK snapshot: device id, sensor enabled flag, recording flags/start time)
- **Output log** (copyable)

## Notes on dependency alignment (important)

TrueMetrics SDK pulls in a complex set of transitive dependencies (AndroidX + Kotlin ecosystem). In Xamarin, you often need to:

- ensure only **one** version of Kotlin/Coroutines/Ktor/Koin is packaged
- avoid duplicate classes from mixed NuGet + vendored JAR/AAR sources
- explicitly include some AARs/JARs that are normally resolved by Gradle

This solution includes a `UseVendorMavenDeps` switch in project files to help isolate “vendor-aligned” dependency sets.

## Troubleshooting

### ANR / UI jank

Avoid calling heavy SDK methods repeatedly on the UI thread. The sample is structured so SDK calls happen only on button taps.

### `DeviceId` blank / recording does not start

This repo is specifically meant to reproduce this issue.

Things to verify:

- background location permission is granted
- notifications permission is granted (Android 13+)
- the foreground service is allowed to start (OEM restrictions / battery optimizations)
- merged AndroidManifest contains required services/providers/receivers from the SDK AAR

## Disclaimer

This repo is **not** the official TrueMetrics SDK and does not contain SDK source code.

It is a community/sample troubleshooting project to help:

- integrate the vendor SDK into Xamarin.Android
- provide actionable reproduction steps and dependency lists

## License

Add your preferred license (MIT/Apache-2.0/etc.) before publishing publicly.
