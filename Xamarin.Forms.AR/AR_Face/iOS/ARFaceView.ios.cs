using ARKit;
using Foundation;
using System;
using UIKit;
using Xamarin.Forms.Platform.iOS;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class ARFaceViewRenderer : ViewRenderer<ARFaceView, UIView>
    {
        private bool disposed;

        protected override void OnElementChanged(ElementChangedEventArgs<ARFaceView> e)
        {
            base.OnElementChanged(e);
            
            if (Control is null && !disposed)
            {
                var mainView = new UIView();
                SetNativeControl(mainView);

                _ = Control ?? throw new NullReferenceException($"{nameof(Control)} cannot be null");

                if (ARFaceTrackingConfiguration.IsSupported)
                {
                    AddSubview(BuildARFaceView());
                    AddConstraints(NSLayoutConstraint.FromVisualFormat(
                        "V:|[mainView]|",
                        NSLayoutFormatOptions.DirectionLeftToRight,
                        null,
                        new NSDictionary("mainView", mainView)));

                    AddConstraints(NSLayoutConstraint.FromVisualFormat(
                        "H:|[mainView]|",
                        NSLayoutFormatOptions.AlignAllTop,
                        null,
                        new NSDictionary("mainView", mainView)));

                    ViewController.AddChildViewController(BuildARViewController());
                }
                else
                {
                    //TODO: Draw a "unsuported" template from xamarin forms and a default "unsuported" view
                    throw new NotSupportedException("AR face tracking not supported on this device");
                }
            }
        }

        protected virtual iOSARFaceView BuildARFaceView() =>
            new iOSARFaceView();

        protected virtual ARFaceViewController BuildARViewController() =>
            new ARFaceViewController();

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
