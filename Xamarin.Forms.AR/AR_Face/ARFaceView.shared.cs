using System;

namespace Xamarin.Forms.AR
{
    public class ARFaceView : View
    {
        public ARFaceView() : base()
        {
            #region Required work-around to prevent linker from removing the platform-specific implementation
#if __ANDROID__
            if (DateTime.Now.Ticks < 0)
                _ = new Xamarin.Forms.AR.Platform.Android.ARFaceViewRenderer(Xamarin.Essentials.Platform.CurrentActivity);
#elif __IOS__
            if (DateTime.Now.Ticks < 0)
                _ = new Xamarin.Forms.AR.Platform.iOS.ARFaceViewRenderer();
#endif
            #endregion
        }
    }
}
