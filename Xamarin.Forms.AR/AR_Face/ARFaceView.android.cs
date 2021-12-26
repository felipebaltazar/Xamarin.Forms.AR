using System;
using System.ComponentModel;
using Android.App;
using Android.Content;
using Android.Opengl;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.AR.Core;
using Google.AR.Core.Exceptions;
using Java.IO;
using Javax.Microedition.Khronos.Opengles;
using Xamarin.Essentials;
using Xamarin.Forms.AR.Helpers;
using Xamarin.Forms.AR.Models;
using Xamarin.Forms.Platform.Android;
using Xamarin.Forms.Platform.Android.FastRenderers;
using static Google.AR.Core.AugmentedFace;
using AView = Android.Views.View;
using Config = Google.AR.Core.Config;
using XPlatform = Xamarin.Essentials.Platform;

namespace Xamarin.Forms.AR.Platform.Android
{
    public class ARFaceViewRenderer : FrameLayout, IViewRenderer, IVisualElementRenderer, GLSurfaceView.IRenderer
    {
        private static readonly float[] DEFAULT_COLOR = new float[] { 0f, 0f, 0f, 0f };

        private int? defaultLabelFor;
        private bool disposed;
        private bool userRequestedInstall = true;
        private Session session;
        private ARFaceView element;
        private VisualElementTracker visualElementTracker;
        private VisualElementRenderer visualElementRenderer;

        private DisplayRotationHelper displayRotationHelper;
        private readonly TrackingStateHelper trackingStateHelper;
        private readonly BackgroundRenderer backgroundRenderer = new BackgroundRenderer();
        private readonly AugmentedFaceRenderer augmentedFaceRenderer = new AugmentedFaceRenderer();
        private readonly ObjectRenderer noseObject = new ObjectRenderer();
        private readonly ObjectRenderer rightEarObject = new ObjectRenderer();
        private readonly ObjectRenderer leftEarObject = new ObjectRenderer();

        // Temporary matrix allocated here to reduce number of allocations for each frame.
        private readonly float[] noseMatrix = new float[16];
        private readonly float[] rightEarMatrix = new float[16];
        private readonly float[] leftEarMatrix = new float[16];

        VisualElement IVisualElementRenderer.Element => Element;

        ViewGroup IVisualElementRenderer.ViewGroup => null;

        VisualElementTracker IVisualElementRenderer.Tracker => visualElementTracker;

        AView IVisualElementRenderer.View => this;

        public VisualElement Element
        {
            get => element;
            set
            {
                if (element == value)
                    return;

                var oldElement = element;
                element = value as ARFaceView;

                OnElementChanged(new ElementChangedEventArgs<ARFaceView>(oldElement, element));
            }
        }

        public event EventHandler<VisualElementChangedEventArgs> ElementChanged;

        public event EventHandler<PropertyChangedEventArgs> ElementPropertyChanged;

        public ARFaceViewRenderer(Context context) : base(context)
        {
            visualElementRenderer = new VisualElementRenderer(this);
            trackingStateHelper = new TrackingStateHelper(XPlatform.CurrentActivity);
        }

        public SizeRequest GetDesiredSize(int widthConstraint, int heightConstraint)
        {
            Measure(widthConstraint, heightConstraint);
            var result = new SizeRequest(new Size(MeasuredWidth, MeasuredHeight), new Size(Context.ToPixels(20), Context.ToPixels(20)));
            return result;
        }

        public void MeasureExactly() =>
            MeasureExactly(this, Element, Context);

        void IVisualElementRenderer.SetElement(VisualElement element)
        {
            if (!(element is ARFaceView arfaceView))
                throw new ArgumentException($"{nameof(element)} must be of type {nameof(ARFaceView)}");

            // Performance.Start(out var reference);

            if (visualElementTracker == null)
                visualElementTracker = new VisualElementTracker(this);

            Element = arfaceView;

            // Performance.Stop(reference);
        }

        void IVisualElementRenderer.SetLabelFor(int? id)
        {
            if (defaultLabelFor == null)
                defaultLabelFor = LabelFor;

            LabelFor = (int)(id ?? defaultLabelFor);
        }

        void IVisualElementRenderer.UpdateLayout() =>
            visualElementTracker?.UpdateLayout();

        protected virtual async void OnElementChanged(ElementChangedEventArgs<ARFaceView> e)
        {
            if (e.OldElement != null)
            {
                e.OldElement.PropertyChanged -= OnElementPropertyChanged;
            }

            if (e.NewElement != null)
            {
                this.EnsureId();

                e.NewElement.PropertyChanged += OnElementPropertyChanged;

                ElevationHelper.SetElevation(this, e.NewElement);

                var arAvailability = ArCoreApk.Instance.CheckAvailability(Context);
                if (arAvailability.IsUnsupported) //TODO: Create a unsuported view renderer
                    return;

                // ARCore requires camera permission to operate.
                var permissionResult = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if(permissionResult != PermissionStatus.Granted)
                {
                    permissionResult = await Permissions.RequestAsync<Permissions.Camera>();

                    if (permissionResult != PermissionStatus.Granted)
                        return;
                }

                try
                {
                    if (session is null)
                    {
                        var installResult = ArCoreApk.Instance.RequestInstall(XPlatform.CurrentActivity, userRequestedInstall);

                        if (installResult == ArCoreApk.InstallStatus.Installed)
                        {
                            // Success: Safe to create the AR session.
                            session = new Session(Context);
                        }
                        else if (installResult == ArCoreApk.InstallStatus.InstallRequested)
                        {
                            // When this method returns `INSTALL_REQUESTED`:
                            // 1. ARCore pauses this activity.
                            // 2. ARCore prompts the user to install or update Google Play
                            //    Services for AR (market://details?id=com.google.ar.core).
                            // 3. ARCore downloads the latest device profile data.
                            // 4. ARCore resumes this activity. The next invocation of
                            //    requestInstall() will either return `INSTALLED` or throw an
                            //    exception if the installation or update did not succeed.
                            userRequestedInstall = false;
                            return;
                        }
                    }
                }
                catch (UnavailableUserDeclinedInstallationException ex)
                {
                    //TODO: Feedback for user that dont want to install arcore
                    return;
                }

                displayRotationHelper = new DisplayRotationHelper(Context);

                var surfaceView = new GLSurfaceView(Context);

                // Set up renderer.
                surfaceView.PreserveEGLContextOnPause = true;
                surfaceView.SetEGLContextClientVersion(2);
                surfaceView.SetEGLConfigChooser(8, 8, 8, 8, 16, 0); // Alpha used for plane blending.
                surfaceView.SetRenderer(this);
                surfaceView.RenderMode = Rendermode.Continuously;
                surfaceView.SetWillNotDraw(false);
                this.AddView(surfaceView);
            }

            // Set a camera configuration that usese the front-facing camera.
            var filter =
                new CameraConfigFilter(session).SetFacingDirection(CameraConfig.FacingDirection.Front);

            var cameraConfig = session.GetSupportedCameraConfigs(filter)[0];
            session.CameraConfig = cameraConfig;

            var config = new Config(session);
            config.SetAugmentedFaceMode(Config.AugmentedFaceMode.Mesh3d);
            session.Configure(config);

            ElementChanged?.Invoke(this, new VisualElementChangedEventArgs(e.OldElement, e.NewElement));
        }

        protected virtual void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        private static void MeasureExactly(AView control, VisualElement element, Context context)
        {
            if (control == null || element == null)
                return;

            var width = element.Width;
            var height = element.Height;

            if (width <= 0 || height <= 0)
                return;

            var realWidth = (int)context.ToPixels(width);
            var realHeight = (int)context.ToPixels(height);

            var widthMeasureSpec = MeasureSpecFactory.MakeMeasureSpec(realWidth, MeasureSpecMode.Exactly);
            var heightMeasureSpec = MeasureSpecFactory.MakeMeasureSpec(realHeight, MeasureSpecMode.Exactly);

            control.Measure(widthMeasureSpec, heightMeasureSpec);
        }

        public void OnDrawFrame(IGL10 gl)
        {
            // Clear screen to notify driver it should not load any pixels from previous frame.
            GLES20.GlClear(GLES20.GlColorBufferBit | GLES20.GlDepthBufferBit);

            if (session is null)
                return;

            // Notify ARCore session that the view size changed so that the perspective matrix and
            // the video background can be properly adjusted.
            //displayRotationHelper.updateSessionIfNeeded(session);

            try
            {
                session.SetCameraTextureName(backgroundRenderer.GetTextureId());

                // Obtain the current frame from ARSession. When the configuration is set to
                // UpdateMode.BLOCKING (it is by default), this will throttle the rendering to the
                // camera framerate.
                var frame = session.Update();
                var camera = frame.Camera;

                // Get projection matrix.
                var projectionMatrix = new float[16];
                camera.GetProjectionMatrix(projectionMatrix, 0, 0.1f, 100.0f);

                // Get camera matrix and draw.
                var viewMatrix = new float[16];
                camera.GetViewMatrix(viewMatrix, 0);

                // Compute lighting from average intensity of the image.
                // The first three components are color scaling factors.
                // The last one is the average pixel intensity in gamma space.
                var colorCorrectionRgba = new float[4];
                frame.LightEstimate.GetColorCorrection(colorCorrectionRgba, 0);

                // If frame is ready, render camera preview image to the GL surface.
                backgroundRenderer.Draw(frame);

                // Keep the screen unlocked while tracking, but allow it to lock when tracking stops.
                trackingStateHelper.UpdateKeepScreenOnFlag(camera.TrackingState);

                // ARCore's face detection works best on upright faces, relative to gravity.
                // If the device cannot determine a screen side aligned with gravity, face
                // detection may not work optimally.
                var faces = session.GetAllTrackables(Java.Lang.Class.FromType(typeof(AugmentedFace)));
                foreach (AugmentedFace face in faces)
                {
                    if (face.TrackingState != TrackingState.Tracking) {
                      continue;
                    }

                    float scaleFactor = 1.0f;

                    // Face objects use transparency so they must be rendered back to front without depth write.
                    GLES20.GlDepthMask(false);

                    // Each face's region poses, mesh vertices, and mesh normals are updated every frame.

                    // 1. Render the face mesh first, behind any 3D objects attached to the face regions.
                    float[] modelMatrix = new float[16];
                    face.CenterPose.ToMatrix(modelMatrix, 0);
                    augmentedFaceRenderer.Draw(
                        projectionMatrix, viewMatrix, modelMatrix, colorCorrectionRgba, face);

                    // 2. Next, render the 3D objects attached to the forehead.
                    face.GetRegionPose(RegionType.ForeheadRight).ToMatrix(rightEarMatrix, 0);
                    rightEarObject.UpdateModelMatrix(rightEarMatrix, scaleFactor);
                    rightEarObject.Draw(viewMatrix, projectionMatrix, colorCorrectionRgba, DEFAULT_COLOR);

                    face.GetRegionPose(RegionType.ForeheadLeft).ToMatrix(leftEarMatrix, 0);
                    leftEarObject.UpdateModelMatrix(leftEarMatrix, scaleFactor);
                    leftEarObject.Draw(viewMatrix, projectionMatrix, colorCorrectionRgba, DEFAULT_COLOR);

                    // 3. Render the nose last so that it is not occluded by face mesh or by 3D objects attached
                    // to the forehead regions.
                    face.GetRegionPose(RegionType.NoseTip).ToMatrix(noseMatrix, 0);
                    noseObject.UpdateModelMatrix(noseMatrix, scaleFactor);
                    noseObject.Draw(viewMatrix, projectionMatrix, colorCorrectionRgba, DEFAULT_COLOR);
                  }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                GLES20.GlDepthMask(true);
            }
        }

        public void OnSurfaceChanged(IGL10 gl, int width, int height)
        {
            displayRotationHelper.OnSurfaceChanged(width, height);
            GLES20.GlViewport(0, 0, width, height);
        }

        public void OnSurfaceCreated(IGL10 gl, Javax.Microedition.Khronos.Egl.EGLConfig config)
        {
            GLES20.GlClearColor(0.1f, 0.1f, 0.1f, 1.0f);

            // Prepare the rendering objects. This involves reading shaders, so may throw an IOException.
            try
            {
                // Create the texture and pass it to ARCore session to be filled during update().
                backgroundRenderer.CreateOnGlThread(Context);
                augmentedFaceRenderer.CreateOnGlThread(Context, "models/freckles.png");
                augmentedFaceRenderer.SetMaterialProperties(0.0f, 1.0f, 0.1f, 6.0f);
                noseObject.CreateOnGlThread(Context, "models/nose.obj", "models/nose_fur.png");
                noseObject.SetMaterialProperties(0.0f, 1.0f, 0.1f, 6.0f);
                noseObject.SetBlendMode(BlendMode.AlphaBlending);
                rightEarObject.CreateOnGlThread(Context, "models/forehead_right.obj", "models/ear_fur.png");
                rightEarObject.SetMaterialProperties(0.0f, 1.0f, 0.1f, 6.0f);
                rightEarObject.SetBlendMode(BlendMode.AlphaBlending);
                leftEarObject.CreateOnGlThread(Context, "models/forehead_left.obj", "models/ear_fur.png");
                leftEarObject.SetMaterialProperties(0.0f, 1.0f, 0.1f, 6.0f);
                leftEarObject.SetBlendMode(BlendMode.AlphaBlending);

            }
            catch (IOException e)
            {
            }
        }

        // This is an internal class, so for now we just replicate it here
        static class MeasureSpecFactory
        {
            public static int GetSize(int measureSpec)
            {
                const int modeMask = 0x3 << 30;
                return measureSpec & ~modeMask;
            }

            // Literally does the same thing as the android code, 1000x faster because no bridge cross
            // benchmarked by calling 1,000,000 times in a loop on actual device
            public static int MakeMeasureSpec(int size, MeasureSpecMode mode) =>
                size + (int)mode;
        }
    }
}
