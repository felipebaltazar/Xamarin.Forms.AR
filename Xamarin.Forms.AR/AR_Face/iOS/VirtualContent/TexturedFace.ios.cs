using ARKit;
using Foundation;
using SceneKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class TexturedFace : VirtualContentController
    {
        public override SCNNode GetNode(ISCNSceneRenderer renderer, ARAnchor anchor)
        {
            if (renderer is ARSCNView sceneView && anchor is ARFaceAnchor)
            {
                var frame = sceneView.Session.CurrentFrame;

                // Show video texture as the diffuse material and disable lighting.
                var faceGeometry = ARSCNFaceGeometry.Create(sceneView.Device, true);

                var material = faceGeometry.FirstMaterial;
                material.Diffuse.Contents = sceneView.Scene.Background.Contents;
                //material.LightingModelName =

                var shaderURL = NSBundle.MainBundle.GetUrlForResource("VideoTexturedFace", "shader");

                try
                {
                    var modifier = new NSString(shaderURL);
                    faceGeometry.ShaderModifiers = new SCNShaderModifiers() { EntryPointGeometry = shaderURL.ToString() } [ .geometry: modifier]

                    // Pass view-appropriate image transform to the shader modifier so
                    // that the mapped video lines up correctly with the background video.
                    var affineTransform = frame.displayTransform(for: .portrait, viewportSize: sceneView.bounds.size)
                        var transform = SCNMatrix4(affineTransform)
                    faceGeometry.setValue(SCNMatrix4Invert(transform), forKey: "displayTransform")

                    ContentNode = SCNNode(geometry: faceGeometry)
                }
                catch (Exception)
                {
                    fatalError("Can't load shader modifier from bundle.")
                }


                return ContentNode;
            }


            return base.GetNode(renderer, anchor);
        }
    }
}
