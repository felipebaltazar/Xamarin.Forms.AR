using ARKit;
using CoreFoundation;
using Foundation;
using SceneKit;
using System.Collections.Generic;
using UIKit;
using Xamarin.Forms.AR.Platform.iOS;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class ARFaceViewController : UIViewController, IARSessionDelegate
    {
        private ARSCNView sceneView;
        private ARSessionDelegate arsessionDelegate;
        private ARSCNViewDelegate arsceneviewDelegate;

        public Dictionary<ARFaceAnchor, VirtualContentController> FaceAnchorsAndContentControllers { get; } =
            new Dictionary<ARFaceAnchor, VirtualContentController>();

        public VirtualContentType SelectedVirtualContent;

        public ARFaceViewController()
        {
            arsessionDelegate = new ARFaceViewSessionDelegate(sceneView, this);
            arsceneviewDelegate = new ARFaceViewDelegate(sceneView, this);
        }


        public override bool PrefersHomeIndicatorAutoHidden
        {
            get
            {
                return true;
            }
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            sceneView.Delegate = arsceneviewDelegate;
            sceneView.Session.Delegate = arsessionDelegate;
            sceneView.AutomaticallyUpdatesLighting = true;
            SelectedVirtualContent = VirtualContentType.Geometry;
        }
    }
}

public class ARFaceViewSessionDelegate : ARSessionDelegate
{
    private ARSCNView sceneView;
    private ARFaceViewController controller;

    public ARFaceViewSessionDelegate(ARSCNView sceneView, ARFaceViewController controller)
    {
        this.sceneView = sceneView;
        this.controller = controller;
    }

    public override void DidFail(ARSession session, NSError error)
    {
        base.DidFail(session, error);

        if (error is ARError errorWithInfo)
        {
            var errorMessage =
                $"{errorWithInfo.LocalizedDescription}\n" +
                $"{errorWithInfo.localizedFailureReason}\n" +
                $"{errorWithInfo.localizedRecoverySuggestion}";

            DispatchQueue.MainQueue.DispatchAsync(() => DisplayErrorMessage("The AR session failed.", errorMessage));
        }
    }


    public void ResetTracking()
    {
        if (!ARFaceTrackingConfiguration.IsSupported)
            return;

        var configuration = new ARFaceTrackingConfiguration();

        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            configuration.MaximumNumberOfTrackedFaces = ARFaceTrackingConfiguration.SupportedNumberOfTrackedFaces;

        configuration.LightEstimationEnabled = true;
        sceneView.Session.Run(configuration, ARSessionRunOptions.ResetTracking | ARSessionRunOptions.RemoveExistingAnchors);
        controller.FaceAnchorsAndContentControllers.Clear();
    }

    public void DisplayErrorMessage(string title, string message)
    {
        //  Present an alert informing about the error that has occurred.
        var alertController = UIAlertController.Create(
            title,
            message,
            UIAlertControllerStyle.Alert);

        var restartAction = UIAlertAction.Create("Restart Session", UIAlertActionStyle.Default, (a) =>
        {
            alertController.DismissViewController(true, null);
            this.ResetTracking();
        });

        alertController.AddAction(restartAction);
        controller.PresentViewController(alertController, true, null);
    }
}

public class ARFaceViewDelegate : ARSCNViewDelegate
{
    private ARSCNView sceneView;
    private ARFaceViewController controller;

    public ARFaceViewDelegate(ARSCNView sceneView, ARFaceViewController controller)
    {
        this.sceneView = sceneView;
        this.controller = controller;
    }

    [Export("renderer:didAddNode:forAnchor:")]
    public override void DidAddNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
    {
        base.DidAddNode(renderer, node, anchor);

        if (anchor is ARFaceAnchor faceAnchor)
        {

            //  If this is the first time with this anchor, get the controller to create content.
            //  Otherwise (switching content), will change content when setting `selectedVirtualContent`.
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                var contentController = controller.SelectedVirtualContent.MakeController();
                if (node.ChildNodes.Length == 0)
                {
                    var contentNode = contentController.GetNode(renderer, faceAnchor);
                    node.AddChildNode(contentNode);

                    if (controller.FaceAnchorsAndContentControllers.ContainsKey(faceAnchor))
                        controller.FaceAnchorsAndContentControllers[faceAnchor] = contentController;
                    else
                        controller.FaceAnchorsAndContentControllers.Add(faceAnchor, contentController);
                }
            });
        }
    }

    [Export("renderer:didUpdateNode:forAnchor:")]
    public override void DidUpdateNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
    {
        base.DidUpdateNode(renderer, node, anchor);

        if (anchor is ARFaceAnchor faceAnchor)
        {
            var contentController = controller.FaceAnchorsAndContentControllers[faceAnchor];
            contentController.DidUpdateNode(renderer, contentController.ContentNode, faceAnchor);
        }
    }

    [Export("renderer:didRemoveNode:forAnchor:")]
    public override void DidRemoveNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
    {
        base.DidRemoveNode(renderer, node, anchor);
        if (anchor is ARFaceAnchor faceAnchor)
        {
            controller.FaceAnchorsAndContentControllers[faceAnchor] = null;
        }
    }
}
