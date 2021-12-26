using Android.App;
using Android.Views;
using Google.AR.Core;

namespace Xamarin.Forms.AR.Helpers
{
    public class TrackingStateHelper
    {
        private readonly Activity activity;

        private TrackingState previousTrackingState;

        public TrackingStateHelper(Activity activity)
        {
            this.activity = activity;
        }

        public void UpdateKeepScreenOnFlag(TrackingState trackingState)
        {
            if (trackingState == previousTrackingState)
                return;

            previousTrackingState = trackingState;
            if(trackingState == TrackingState.Paused || trackingState == TrackingState.Stopped)
            {
                activity.RunOnUiThread(
                        () => activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn));
            }
            else
            {
                activity.RunOnUiThread(
                        () => activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn));
            }
        }

        public static string GetTrackingFailureReasonString(Camera camera)
        {
            var reason = camera.TrackingFailureReason;
            return "Unknown tracking failure reason: " + reason;
        }
    }
}
