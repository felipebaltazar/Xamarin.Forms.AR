using System;
using ARKit;
using UIKit;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class iOSARFaceView : UIView
    {
        private ARSCNView sceneView;

        public iOSARFaceView()
        {
            sceneView = new ARSCNView() { TranslatesAutoresizingMaskIntoConstraints = false };
        }

    }
}

