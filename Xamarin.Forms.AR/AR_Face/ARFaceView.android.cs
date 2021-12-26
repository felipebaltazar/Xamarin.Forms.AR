using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using System;
using System.ComponentModel;
using Xamarin.Forms.Platform.Android;
using Xamarin.Forms.Platform.Android.FastRenderers;
using AView = Android.Views.View;

namespace Xamarin.Forms.AR.Platform.Android
{
    public class ARFaceViewRenderer : FrameLayout, IViewRenderer, IVisualElementRenderer
    {
        private bool disposed;
        private int? defaultLabelFor;
        private ARFaceView element;
        private VisualElementTracker visualElementTracker;
        private VisualElementRenderer visualElementRenderer;
        private FragmentManager fragmentManager;
        private ARFragment aRFragment;

        FragmentManager FragmentManager => fragmentManager ??= Context.GetFragmentManager();

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

        protected virtual void OnElementChanged(ElementChangedEventArgs<ARFaceView> e)
        {
            ARFragment newfragment = null;

            if (e.OldElement != null)
            {
                e.OldElement.PropertyChanged -= OnElementPropertyChanged;
                aRFragment?.Dispose();
                aRFragment = null;
            }

            if (e.NewElement != null)
            {
                this.EnsureId();

                e.NewElement.PropertyChanged += OnElementPropertyChanged;

                ElevationHelper.SetElevation(this, e.NewElement);
                newfragment = new ARFragment() { Element = element };
            }

            FragmentManager.BeginTransaction()
                .Replace(Id, aRFragment = newfragment, "arcorefragment")
                .Commit();

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

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            aRFragment?.Dispose();
            aRFragment = null;

            disposed = true;

            if (disposing)
            {
                SetOnClickListener(null);
                SetOnTouchListener(null);

                if (visualElementTracker != null)
                {
                    visualElementTracker.Dispose();
                    visualElementTracker = null;
                }

                if (visualElementRenderer != null)
                {
                    visualElementRenderer.Dispose();
                    visualElementRenderer = null;
                }

                if (Element != null)
                {
                    Element.PropertyChanged -= OnElementPropertyChanged;

                    if (Xamarin.Forms.Platform.Android.Platform.GetRenderer(Element) == this)
                        Xamarin.Forms.Platform.Android.Platform.SetRenderer(Element, null);
                }
            }

            base.Dispose(disposing);
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
