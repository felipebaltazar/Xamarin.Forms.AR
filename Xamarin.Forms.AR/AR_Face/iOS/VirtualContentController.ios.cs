using ARKit;
using SceneKit;

namespace Xamarin.Forms.AR.Platform.iOS
{
    public class VirtualContentController : ARSCNViewDelegate
    {
        /// The root node for the virtual content.
        public SCNNode ContentNode { get; set; }

        public ARAnchor CurrentAnchor { get; set; }


        public override SCNNode GetNode(ISCNSceneRenderer renderer, ARAnchor anchor)
        {
            ContentNode =  base.GetNode(renderer, anchor);
            CurrentAnchor = anchor;

            return ContentNode;
        }

        public override void DidUpdateNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
        {
            base.DidUpdateNode(renderer, node, anchor);

            ContentNode = node;
            CurrentAnchor = anchor;
        }
    }
}
