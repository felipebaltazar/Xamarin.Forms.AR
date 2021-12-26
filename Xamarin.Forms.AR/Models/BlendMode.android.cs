using System;
namespace Xamarin.Forms.AR.Models
{
    /**
         * Blend mode.
         *
         * @see #setBlendMode(BlendMode)
         */
    public enum BlendMode
    {
        /** Multiplies the destination color by the source alpha, without z-buffer writing. */
        Shadow,
        /** Normal alpha blending with z-buffer writing. */
        AlphaBlending
    }
}
