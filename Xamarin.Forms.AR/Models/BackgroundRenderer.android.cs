using Android.Content;
using Android.Opengl;
using Google.AR.Core;
using Java.Lang;
using Java.Nio;
using Xamarin.Forms.AR.Helpers;
using ARFrame = Google.AR.Core.Frame;

namespace Xamarin.Forms.AR.Models
{
    public class BackgroundRenderer
    {
        // Shader names.
        private const string CAMERA_VERTEX_SHADER_NAME = "shaders/screenquad.vert";
        private const string CAMERA_FRAGMENT_SHADER_NAME = "shaders/screenquad.frag";

        private const string DEPTH_VISUALIZER_VERTEX_SHADER_NAME =
            "shaders/background_show_depth_color_visualization.vert";
        private const string DEPTH_VISUALIZER_FRAGMENT_SHADER_NAME =
            "shaders/background_show_depth_color_visualization.frag";

        private const int COORDS_PER_VERTEX = 2;
        private const int TEXCOORDS_PER_VERTEX = 2;
        private const int FLOAT_SIZE = 4;

        private FloatBuffer quadCoords;
        private FloatBuffer quadTexCoords;

        private int cameraProgram;
        private int depthProgram;

        private int cameraPositionAttrib;
        private int cameraTexCoordAttrib;
        private int cameraTextureUniform;
        private int cameraTextureId = -1;
        private bool suppressTimestampZeroRendering = true;

        private int depthPositionAttrib;
        private int depthTexCoordAttrib;
        private int depthTextureUniform;
        private int depthTextureId = -1;

        public int GetTextureId()
        {
            return cameraTextureId;
        }

        /**
         * Allocates and initializes OpenGL resources needed by the background renderer. Must be called on
         * the OpenGL thread, typically in {@link GLSurfaceView.Renderer#onSurfaceCreated(GL10,
         * EGLConfig)}.
         *
         * @param context Needed to access shader source.
         */
        public void CreateOnGlThread(Context context, int depthTextureId)
        {
            // Generate the background texture.
            var textures = new int[1];
            GLES20.GlGenTextures(1, textures, 0);
            cameraTextureId = textures[0];
            var textureTarget = GLES11Ext.GlTextureExternalOes;
            GLES20.GlBindTexture(textureTarget, cameraTextureId);

            GLES20.GlTexParameteri(textureTarget, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(textureTarget, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(textureTarget, GLES20.GlTextureMinFilter, GLES20.GlLinear);
            GLES20.GlTexParameteri(textureTarget, GLES20.GlTextureMagFilter, GLES20.GlLinear);

            int numVertices = 4;
            if (numVertices != QUAD_COORDS.Length / COORDS_PER_VERTEX)
            {
                throw new RuntimeException("Unexpected number of vertices in BackgroundRenderer.");
            }

            ByteBuffer bbCoords = ByteBuffer.AllocateDirect(QUAD_COORDS.Length * FLOAT_SIZE);
            bbCoords.Order(ByteOrder.NativeOrder());
            quadCoords = bbCoords.AsFloatBuffer();
            quadCoords.Put(QUAD_COORDS);
            quadCoords.Position(0);

            ByteBuffer bbTexCoordsTransformed =
                ByteBuffer.AllocateDirect(numVertices * TEXCOORDS_PER_VERTEX * FLOAT_SIZE);
            bbTexCoordsTransformed.Order(ByteOrder.NativeOrder());
            quadTexCoords = bbTexCoordsTransformed.AsFloatBuffer();

            // Load render camera feed shader.
            var vertexShader =
                ShaderHelper.LoadGLShader(context, GLES20.GlVertexShader, CAMERA_VERTEX_SHADER_NAME);
            var fragmentShader =
                ShaderHelper.LoadGLShader(
                    context, GLES20.GlFragmentShader, CAMERA_FRAGMENT_SHADER_NAME);

            cameraProgram = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(cameraProgram, vertexShader);
            GLES20.GlAttachShader(cameraProgram, fragmentShader);
            GLES20.GlLinkProgram(cameraProgram);
            GLES20.GlUseProgram(cameraProgram);
            cameraPositionAttrib = GLES20.GlGetAttribLocation(cameraProgram, "a_Position");
            cameraTexCoordAttrib = GLES20.GlGetAttribLocation(cameraProgram, "a_TexCoord");
            ShaderHelper.CheckGLError("", "Program creation");

            cameraTextureUniform = GLES20.GlGetUniformLocation(cameraProgram, "sTexture");
            ShaderHelper.CheckGLError("", "Program parameters");

            // Load render depth map shader.
            vertexShader =
                ShaderHelper.LoadGLShader(
                    context, GLES20.GlVertexShader, DEPTH_VISUALIZER_VERTEX_SHADER_NAME);
            fragmentShader =
                ShaderHelper.LoadGLShader(
                    context, GLES20.GlFragmentShader, DEPTH_VISUALIZER_FRAGMENT_SHADER_NAME);

            depthProgram = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(depthProgram, vertexShader);
            GLES20.GlAttachShader(depthProgram, fragmentShader);
            GLES20.GlLinkProgram(depthProgram);
            GLES20.GlUseProgram(depthProgram);
            depthPositionAttrib = GLES20.GlGetAttribLocation(depthProgram, "a_Position");
            depthTexCoordAttrib = GLES20.GlGetAttribLocation(depthProgram, "a_TexCoord");
            ShaderHelper.CheckGLError("", "Program creation");

            depthTextureUniform = GLES20.GlGetUniformLocation(depthProgram, "u_DepthTexture");
            ShaderHelper.CheckGLError("", "Program parameters");

            this.depthTextureId = depthTextureId;
        }

        public void CreateOnGlThread(Context context)
        {
            CreateOnGlThread(context, -1);
        }

        public void SuppressTimestampZeroRendering(bool suppressTimestampZeroRendering)
        {
            this.suppressTimestampZeroRendering = suppressTimestampZeroRendering;
        }

        /**
         * Draws the AR background image. The image will be drawn such that virtual content rendered with
         * the matrices provided by {@link com.google.ar.core.Camera#getViewMatrix(float[], int)} and
         * {@link com.google.ar.core.Camera#getProjectionMatrix(float[], int, float, float)} will
         * accurately follow static physical objects. This must be called <b>before</b> drawing virtual
         * content.
         *
         * @param frame The current {@code Frame} as returned by {@link Session#update()}.
         * @param debugShowDepthMap Toggles whether to show the live camera feed or latest depth image.
         */
        public void Draw(ARFrame frame, bool debugShowDepthMap)
        {
            // If display rotation changed (also includes view size change), we need to re-query the uv
            // coordinates for the screen rect, as they may have changed as well.
            if (frame.HasDisplayGeometryChanged)
            {
                frame.TransformCoordinates2d(
                    Coordinates2d.OpenglNormalizedDeviceCoordinates,
                    quadCoords,
                    Coordinates2d.TextureNormalized,
                    quadTexCoords);
            }

            if (frame.Timestamp == 0 && suppressTimestampZeroRendering)
            {
                // Suppress rendering if the camera did not produce the first frame yet. This is to avoid
                // drawing possible leftover data from previous sessions if the texture is reused.
                return;
            }

            Draw(debugShowDepthMap);
        }

        public void Draw(ARFrame frame)
        {
            Draw(frame, false);
        }

        /**
         * Draws the camera image using the currently configured {@link BackgroundRenderer#quadTexCoords}
         * image texture coordinates.
         *
         * <p>The image will be center cropped if the camera sensor aspect ratio does not match the screen
         * aspect ratio, which matches the cropping behavior of {@link
         * Frame#transformCoordinates2d(Coordinates2d, float[], Coordinates2d, float[])}.
         */
        public void Draw(
            int imageWidth, int imageHeight, float screenAspectRatio, int cameraToDisplayRotation)
        {
            // Crop the camera image to fit the screen aspect ratio.
            float imageAspectRatio = (float)imageWidth / imageHeight;
            float croppedWidth;
            float croppedHeight;
            if (screenAspectRatio < imageAspectRatio)
            {
                croppedWidth = imageHeight * screenAspectRatio;
                croppedHeight = imageHeight;
            }
            else
            {
                croppedWidth = imageWidth;
                croppedHeight = imageWidth / screenAspectRatio;
            }

            float u = (imageWidth - croppedWidth) / imageWidth * 0.5f;
            float v = (imageHeight - croppedHeight) / imageHeight * 0.5f;

            float[] texCoordTransformed;
            switch (cameraToDisplayRotation)
            {
                case 90:
                    texCoordTransformed = new float[] { 1 - u, 1 - v, 1 - u, v, u, 1 - v, u, v };
                    break;
                case 180:
                    texCoordTransformed = new float[] { 1 - u, v, u, v, 1 - u, 1 - v, u, 1 - v };
                    break;
                case 270:
                    texCoordTransformed = new float[] { u, v, u, 1 - v, 1 - u, v, 1 - u, 1 - v };
                    break;
                case 0:
                    texCoordTransformed = new float[] { u, 1 - v, 1 - u, 1 - v, u, v, 1 - u, v };
                    break;
                default:
                    throw new IllegalArgumentException("Unhandled rotation: " + cameraToDisplayRotation);
            }

            // Write image texture coordinates.
            quadTexCoords.Position(0);
            quadTexCoords.Put(texCoordTransformed);

            Draw(false);
        }

        /**
         * Draws the camera background image using the currently configured {@link
         * BackgroundRenderer#quadTexCoords} image texture coordinates.
         */
        private void Draw(bool debugShowDepthMap)
        {
            // Ensure position is rewound before use.
            quadTexCoords.Position(0);

            // No need to test or write depth, the screen quad has arbitrary depth, and is expected
            // to be drawn first.
            GLES20.GlDisable(GLES20.GlDepthTest);
            GLES20.GlDepthMask(false);

            GLES20.GlActiveTexture(GLES20.GlTexture0);

            if (debugShowDepthMap)
            {
                GLES20.GlBindTexture(GLES20.GlTexture2d, depthTextureId);
                GLES20.GlUseProgram(depthProgram);
                GLES20.GlUniform1i(depthTextureUniform, 0);

                // Set the vertex positions and texture coordinates.
                GLES20.GlVertexAttribPointer(
                    depthPositionAttrib, COORDS_PER_VERTEX, GLES20.GlFloat, false, 0, quadCoords);
                GLES20.GlVertexAttribPointer(
                    depthTexCoordAttrib, TEXCOORDS_PER_VERTEX, GLES20.GlFloat, false, 0, quadTexCoords);
                GLES20.GlEnableVertexAttribArray(depthPositionAttrib);
                GLES20.GlEnableVertexAttribArray(depthTexCoordAttrib);
            }
            else
            {
                GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, cameraTextureId);
                GLES20.GlUseProgram(cameraProgram);
                GLES20.GlUniform1i(cameraTextureUniform, 0);

                // Set the vertex positions and texture coordinates.
                GLES20.GlVertexAttribPointer(
                    cameraPositionAttrib, COORDS_PER_VERTEX, GLES20.GlFloat, false, 0, quadCoords);
                GLES20.GlVertexAttribPointer(
                    cameraTexCoordAttrib, TEXCOORDS_PER_VERTEX, GLES20.GlFloat, false, 0, quadTexCoords);
                GLES20.GlEnableVertexAttribArray(cameraPositionAttrib);
                GLES20.GlEnableVertexAttribArray(cameraTexCoordAttrib);
            }

            GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

            // Disable vertex arrays
            if (debugShowDepthMap)
            {
                GLES20.GlDisableVertexAttribArray(depthPositionAttrib);
                GLES20.GlDisableVertexAttribArray(depthTexCoordAttrib);
            }
            else
            {
                GLES20.GlDisableVertexAttribArray(cameraPositionAttrib);
                GLES20.GlDisableVertexAttribArray(cameraTexCoordAttrib);
            }

            // Restore the depth state for further drawing.
            GLES20.GlDepthMask(true);
            GLES20.GlEnable(GLES20.GlDepthTest);

            ShaderHelper.CheckGLError("", "BackgroundRendererDraw");
        }

        /**
         * (-1, 1) ------- (1, 1)
         *   |    \           |
         *   |       \        |
         *   |          \     |
         *   |             \  |
         * (-1, -1) ------ (1, -1)
         * Ensure triangles are front-facing, to support glCullFace().
         * This quad will be drawn using GL_TRIANGLE_STRIP which draws two
         * triangles: v0->v1->v2, then v2->v1->v3.
         */
        private static readonly float[] QUAD_COORDS =
            new float[] {
        -1.0f, -1.0f, +1.0f, -1.0f, -1.0f, +1.0f, +1.0f, +1.0f,
            };
    }
}
