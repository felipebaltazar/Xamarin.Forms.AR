using System;
using UIKit;
using Xamarin.Forms.Platform.iOS;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class ARFaceViewRenderer : ViewRenderer<ARFaceView, iOSARFaceView>
    {
        private bool disposed;

        protected override void OnElementChanged(ElementChangedEventArgs<ARFaceView> e)
        {
            base.OnElementChanged(e);

            if (Control is null && !disposed)
            {
                SetNativeControl(new iOSARFaceView());

                _ = Control ?? throw new NullReferenceException($"{nameof(Control)} cannot be null");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            disposed = true;
            Control?.Dispose();
            base.Dispose(disposing);
        }
    }
}
