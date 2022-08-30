namespace Xamarin.Forms.AR.Platform.iOS
{
    public enum VirtualContentType : int
    {
        Transforms,
        Texture,
        Geometry,
        VideoTexture,
        BlendShape
    }

    public static class VirtualContentTypeExtensions
    {
        public static VirtualContentController MakeController(this VirtualContentType self)
        {
            switch (self)
            {
                case VirtualContentType.Transforms:
                    return new TransformVisualization();
                
                case VirtualContentType.Geometry:
                    return new FaceOcclusionOverlay();
                case VirtualContentType.VideoTexture:
                    return new VideoTexturedFace();
                case VirtualContentType.BlendShape:
                    return new BlendShapeCharacter();
                case VirtualContentType.Texture:
                default:
                    return new TexturedFace();
            }
        }
    }
}
