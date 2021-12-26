using System;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Hardware.Display;
using Android.Views;
using Google.AR.Core;
using static Android.Hardware.Display.DisplayManager;

namespace Xamarin.Forms.AR
{
    internal class DisplayRotationHelper : Java.Lang.Object, IDisplayListener
    {
        private bool viewportChanged;
        private int viewportWidth;
        private int viewportHeight;
        private readonly Display display;
        private readonly DisplayManager displayManager;
        private readonly CameraManager cameraManager;

        public DisplayRotationHelper(Context context): base()
        {
            displayManager = (DisplayManager)context.GetSystemService(Context.DisplayService);
            cameraManager = (CameraManager)context.GetSystemService(Context.CameraService);

            var windowManager = (IWindowManager)context.GetSystemService(Context.WindowService);
            display = windowManager.DefaultDisplay;
        }

        public void OnResume()
        {
            displayManager.RegisterDisplayListener(this, null);
        }

        public void OnPause()
        {
            displayManager.UnregisterDisplayListener(this);
        }

        public void OnSurfaceChanged(int width, int height)
        {
            viewportWidth = width;
            viewportHeight = height;
            viewportChanged = true;
        }

        public void UpdateSessionIfNeeded(Session session)
        {
            if (viewportChanged)
            {
                var displayRotation = display.Rotation;
                session.SetDisplayGeometry((int)displayRotation, viewportWidth, viewportHeight);
                viewportChanged = false;
            }
        }

        public float GetCameraSensorRelativeViewportAspectRatio(string cameraId)
        {
            float aspectRatio;
            int cameraSensorToDisplayRotation = GetCameraSensorToDisplayRotation(cameraId);
            switch (cameraSensorToDisplayRotation)
            {
                case 90:
                case 270:
                    aspectRatio = (float)viewportHeight / (float)viewportWidth;
                    break;
                case 0:
                case 180:
                    aspectRatio = (float)viewportWidth / (float)viewportHeight;
                    break;
                default:
                    throw new NotImplementedException("Unhandled rotation: " + cameraSensorToDisplayRotation);
            }
            return aspectRatio;
        }

        public int GetCameraSensorToDisplayRotation(string cameraId)
        {
            CameraCharacteristics characteristics;
            try
            {
                characteristics = cameraManager.GetCameraCharacteristics(cameraId);
            }
            catch (CameraAccessException e)
            {
                throw new NotImplementedException("Unable to determine display orientation", e);
            }

            // Camera sensor orientation.
            var sensorOrientation =  (Java.Lang.Integer)characteristics.Get(CameraCharacteristics.SensorOrientation);

            // Current display orientation.
            int displayOrientation = ToDegrees(display.Rotation);

            // Make sure we return 0, 90, 180, or 270 degrees.
            return (sensorOrientation.IntValue() - displayOrientation + 360) % 360;
        }

        private int ToDegrees(SurfaceOrientation rotation)
        {
            switch (rotation)
            {
                case SurfaceOrientation.Rotation0:
                    return 0;
                case SurfaceOrientation.Rotation90:
                    return 90;
                case SurfaceOrientation.Rotation180:
                    return 180;
                case SurfaceOrientation.Rotation270:
                    return 270;
                default:
                    throw new NotImplementedException("Unknown rotation " + rotation);
            }
        }

        public void OnDisplayAdded(int displayId)
        {
        }

        public void OnDisplayChanged(int displayId)
        {
            viewportChanged = true;
        }

        public void OnDisplayRemoved(int displayId)
        {
        }
    }
}
