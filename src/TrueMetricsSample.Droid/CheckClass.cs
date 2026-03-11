using System;
using Android.Runtime;

namespace TrueMetricsSample.Droid
{
    public class CheckClass
    {
        public static void Check()
        {
            try {
                var clazz = Java.Lang.Class.ForName("io.truemetrics.truemetricssdk.StatusListener");
                System.Diagnostics.Debug.WriteLine("FOUND StatusListener: " + clazz);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine("NOT FOUND: " + e);
            }
        }
    }
}
