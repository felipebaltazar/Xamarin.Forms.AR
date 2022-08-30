using ARKit;
using Foundation;
using UIKit;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class iOSARFaceView : UIView
    {
        private readonly ARSCNView _mainView;

        public iOSARFaceView()
        {
            _mainView = BuildSceneView();

            Add(_mainView);

            AddConstraints(NSLayoutConstraint.FromVisualFormat(
                    "V:|[mainView]|",
                    NSLayoutFormatOptions.DirectionLeftToRight,
                    null,
                    new NSDictionary("mainView", _mainView)));

            AddConstraints(NSLayoutConstraint.FromVisualFormat(
                "H:|[mainView]|",
                NSLayoutFormatOptions.AlignAllTop,
                null,
                new NSDictionary("mainView", _mainView)));
        }

        protected virtual ARSCNView BuildSceneView() =>
            new ARSCNView() { TranslatesAutoresizingMaskIntoConstraints = false };
    }
}

