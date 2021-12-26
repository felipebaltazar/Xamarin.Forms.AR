using Android.App;
using Xamarin.Forms;
using Xamarin.Forms.AR;
using Xamarin.Forms.AR.Platform.Android;

[assembly: ExportRenderer(typeof(ARFaceView), typeof(ARFaceViewRenderer))]

[assembly: UsesFeature("android.hardware.camera.ar")]
[assembly: MetaData("com.google.ar.core", Value = "optional")]
[assembly: UsesPermission("android.permission.CAMERA")]