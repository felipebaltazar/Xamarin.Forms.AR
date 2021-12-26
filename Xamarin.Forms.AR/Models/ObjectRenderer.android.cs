using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Java.Nio;
using JavaGl.Obj;
using Xamarin.Forms.AR.Helpers;
using Matrix = Android.Opengl.Matrix;

namespace Xamarin.Forms.AR.Models
{
    public class ObjectRenderer
    {
        // Shader names.
        private const string VERTEX_SHADER_NAME = "shaders/ar_object.vert";
        private const string FRAGMENT_SHADER_NAME = "shaders/ar_object.frag";

        private const int COORDS_PER_VERTEX = 3;

        private static readonly float[] DEFAULT_COLOR = new float[] { 0f, 0f, 0f, 0f };

        // Note: the last component must be zero to avoid applying the translational part of the matrix.
        private static readonly float[] LIGHT_DIRECTION = new float[] { 0.250f, 0.866f, 0.433f, 0.0f };
        private readonly float[] viewLightDirection = new float[4];

        // Object vertex buffer variables.
        private int vertexBufferId;
        private int verticesBaseAddress;
        private int texCoordsBaseAddress;
        private int normalsBaseAddress;
        private int indexBufferId;
        private int indexCount;

        private int program;
        private readonly int[] textures = new int[1];

        // Shader location: model view projection matrix.
        private int modelViewUniform;
        private int modelViewProjectionUniform;

        // Shader location: object attributes.
        private int positionAttribute;
        private int normalAttribute;
        private int texCoordAttribute;

        // Shader location: texture sampler.
        private int textureUniform;

        // Shader location: environment properties.
        private int lightingParametersUniform;

        // Shader location: material properties.
        private int materialParametersUniform;

        // Shader location: color correction property.
        private int colorCorrectionParameterUniform;

        // Shader location: object color property (to change the primary color of the object).
        private int colorUniform;

        // Shader location: depth texture.
        private int depthTextureUniform;

        // Shader location: transform to depth uvs.
        private int depthUvTransformUniform;

        // Shader location: the aspect ratio of the depth texture.
        private int depthAspectRatioUniform;

        private BlendMode? blendMode = null;

        // Temporary matrices allocated here to reduce number of allocations for each frame.
        private readonly float[] modelMatrix = new float[16];
        private readonly float[] modelViewMatrix = new float[16];
        private readonly float[] modelViewProjectionMatrix = new float[16];

        // Set some default material properties to use for lighting.
        private float ambient = 0.3f;
        private float diffuse = 1.0f;
        private float specular = 1.0f;
        private float specularPower = 6.0f;

        // Depth-for-Occlusion parameters.
        private const string USE_DEPTH_FOR_OCCLUSION_SHADER_FLAG = "USE_DEPTH_FOR_OCCLUSION";
        private bool useDepthForOcclusion = false;
        private float depthAspectRatio = 0.0f;
        private float[] uvTransform = null;
        private int depthTextureId;

        /**
         * Creates and initializes OpenGL resources needed for rendering the model.
         *
         * @param context Context for loading the shader and below-named model and texture assets.
         * @param objAssetName Name of the OBJ file containing the model geometry.
         * @param diffuseTextureAssetName Name of the PNG file containing the diffuse texture map.
         */
        public void CreateOnGlThread(Context context, string objAssetName, string diffuseTextureAssetName)
        {
            // Compiles and loads the shader based on the current configuration.
            CompileAndLoadShaderProgram(context);

            // Read the texture.
            var textureBitmap =
                BitmapFactory.DecodeStream(context.Assets.Open(diffuseTextureAssetName));

            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlGenTextures(textures.Length, textures, 0);
            GLES20.GlBindTexture(GLES20.GlTexture2d, textures[0]);

            GLES20.GlTexParameteri(
                GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlLinearMipmapLinear);
            GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlLinear);
            GLUtils.TexImage2D(GLES20.GlTexture2d, 0, textureBitmap, 0);
            GLES20.GlGenerateMipmap(GLES20.GlTexture2d);
            GLES20.GlBindTexture(GLES20.GlTexture2d, 0);

            textureBitmap.Recycle();

            ShaderHelper.CheckGLError("", "Texture loading");

            // Read the obj file.
            var objInputStream = context.Assets.Open(objAssetName);
            var obj = ObjReader.Read(objInputStream);

            // Prepare the Obj so that its structure is suitable for
            // rendering with OpenGL:
            // 1. Triangulate it
            // 2. Make sure that texture coordinates are not ambiguous
            // 3. Make sure that normals are not ambiguous
            // 4. Convert it to single-indexed data
            obj = ObjUtils.ConvertToRenderable(obj);

            // OpenGL does not use Java arrays. ByteBuffers are used instead to provide data in a format
            // that OpenGL understands.

            // Obtain the data from the OBJ, as direct buffers:
            var wideIndices = ObjData.GetFaceVertexIndices(obj, 3);
            var vertices = ObjData.GetVertices(obj);
            var texCoords = ObjData.GetTexCoords(obj, 2);
            var normals = ObjData.GetNormals(obj);

            // Convert int indices to shorts for GL ES 2.0 compatibility
            var indices =
                ByteBuffer.AllocateDirect(2 * wideIndices.Limit())
                    .Order(ByteOrder.NativeOrder())
                    .AsShortBuffer();
            while (wideIndices.HasRemaining)
            {
                indices.Put((short)wideIndices.Get());
            }
            indices.Rewind();

            int[] buffers = new int[2];
            GLES20.GlGenBuffers(2, buffers, 0);
            vertexBufferId = buffers[0];
            indexBufferId = buffers[1];

            // Load vertex buffer
            verticesBaseAddress = 0;
            texCoordsBaseAddress = verticesBaseAddress + 4 * vertices.Limit();
            normalsBaseAddress = texCoordsBaseAddress + 4 * texCoords.Limit();
            var totalBytes = normalsBaseAddress + 4 * normals.Limit();

            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, vertexBufferId);
            GLES20.GlBufferData(GLES20.GlArrayBuffer, totalBytes, null, GLES20.GlStaticDraw);
            GLES20.GlBufferSubData(
                GLES20.GlArrayBuffer, verticesBaseAddress, 4 * vertices.Limit(), vertices);

            GLES20.GlBufferSubData(
                GLES20.GlArrayBuffer, texCoordsBaseAddress, 4 * texCoords.Limit(), texCoords);

            GLES20.GlBufferSubData(
                GLES20.GlArrayBuffer, normalsBaseAddress, 4 * normals.Limit(), normals);

            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

            // Load index buffer
            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, indexBufferId);
            indexCount = indices.Limit();
            GLES20.GlBufferData(
                GLES20.GlElementArrayBuffer, 2 * indexCount, indices, GLES20.GlStaticDraw);
            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, 0);

            ShaderHelper.CheckGLError("", "OBJ buffer load");

            Matrix.SetIdentityM(modelMatrix, 0);
        }

        /**
         * Selects the blending mode for rendering.
         *
         * @param blendMode The blending mode. Null indicates no blending (opaque rendering).
         */
        public void SetBlendMode(BlendMode blendMode)
        {
            this.blendMode = blendMode;
        }

        /**
         * Specifies whether to use the depth texture to perform depth-based occlusion of virtual objects
         * from real-world geometry.
         *
         * <p>This function is a no-op if the value provided is the same as what is already set. If the
         * value changes, this function will recompile and reload the shader program to either
         * enable/disable depth-based occlusion. NOTE: recompilation of the shader is inefficient. This
         * code could be optimized to precompile both versions of the shader.
         *
         * @param context Context for loading the shader.
         * @param useDepthForOcclusion Specifies whether to use the depth texture to perform occlusion
         *     during rendering of virtual objects.
         */
        public void SetUseDepthForOcclusion(Context context, bool useDepthForOcclusion)
        {
            if (this.useDepthForOcclusion == useDepthForOcclusion)
            {
                return; // No change, does nothing.
            }

            // Toggles the occlusion rendering mode and recompiles the shader.
            this.useDepthForOcclusion = useDepthForOcclusion;
            CompileAndLoadShaderProgram(context);
        }

        private void CompileAndLoadShaderProgram(Context context)
        {
            // Compiles and loads the shader program based on the selected mode.
            var defineValuesMap = new Dictionary<string, int>();
            defineValuesMap.Add(USE_DEPTH_FOR_OCCLUSION_SHADER_FLAG, useDepthForOcclusion ? 1 : 0);

            var vertexShader =
                ShaderHelper.LoadGLShader(context, GLES20.GlVertexShader, VERTEX_SHADER_NAME);
            var fragmentShader =
                ShaderHelper.LoadGLShader(context, GLES20.GlFragmentShader, FRAGMENT_SHADER_NAME, defineValuesMap);

            program = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(program, vertexShader);
            GLES20.GlAttachShader(program, fragmentShader);
            GLES20.GlLinkProgram(program);
            GLES20.GlUseProgram(program);

            ShaderHelper.CheckGLError("", "Program creation");

            modelViewUniform = GLES20.GlGetUniformLocation(program, "u_ModelView");
            modelViewProjectionUniform = GLES20.GlGetUniformLocation(program, "u_ModelViewProjection");

            positionAttribute = GLES20.GlGetAttribLocation(program, "a_Position");
            normalAttribute = GLES20.GlGetAttribLocation(program, "a_Normal");
            texCoordAttribute = GLES20.GlGetAttribLocation(program, "a_TexCoord");

            textureUniform = GLES20.GlGetUniformLocation(program, "u_Texture");

            lightingParametersUniform = GLES20.GlGetUniformLocation(program, "u_LightingParameters");
            materialParametersUniform = GLES20.GlGetUniformLocation(program, "u_MaterialParameters");
            colorCorrectionParameterUniform =
                GLES20.GlGetUniformLocation(program, "u_ColorCorrectionParameters");
            colorUniform = GLES20.GlGetUniformLocation(program, "u_ObjColor");

            // Occlusion Uniforms.
            if (useDepthForOcclusion)
            {
                depthTextureUniform = GLES20.GlGetUniformLocation(program, "u_DepthTexture");
                depthUvTransformUniform = GLES20.GlGetUniformLocation(program, "u_DepthUvTransform");
                depthAspectRatioUniform = GLES20.GlGetUniformLocation(program, "u_DepthAspectRatio");
            }

            ShaderHelper.CheckGLError("", "Program parameters");
        }

        /**
         * Updates the object model matrix and applies scaling.
         *
         * @param modelMatrix A 4x4 model-to-world transformation matrix, stored in column-major order.
         * @param scaleFactor A separate scaling factor to apply before the {@code modelMatrix}.
         * @see android.opengl.Matrix
         */
        public void UpdateModelMatrix(float[] modelMatrix, float scaleFactor)
        {
            float[] scaleMatrix = new float[16];
            Matrix.SetIdentityM(scaleMatrix, 0);
            scaleMatrix[0] = scaleFactor;
            scaleMatrix[5] = scaleFactor;
            scaleMatrix[10] = scaleFactor;
            Matrix.MultiplyMM(this.modelMatrix, 0, modelMatrix, 0, scaleMatrix, 0);
        }

        /**
         * Sets the surface characteristics of the rendered model.
         *
         * @param ambient Intensity of non-directional surface illumination.
         * @param diffuse Diffuse (matte) surface reflectivity.
         * @param specular Specular (shiny) surface reflectivity.
         * @param specularPower Surface shininess. Larger values result in a smaller, sharper specular
         *     highlight.
         */
        public void SetMaterialProperties(
            float ambient, float diffuse, float specular, float specularPower)
        {
            this.ambient = ambient;
            this.diffuse = diffuse;
            this.specular = specular;
            this.specularPower = specularPower;
        }

        /**
         * Draws the model.
         *
         * @param cameraView A 4x4 view matrix, in column-major order.
         * @param cameraPerspective A 4x4 projection matrix, in column-major order.
         * @param colorCorrectionRgba Illumination intensity. Combined with diffuse and specular material
         *     properties.
         * @see #setBlendMode(BlendMode)
         * @see #updateModelMatrix(float[], float)
         * @see #setMaterialProperties(float, float, float, float)
         * @see android.opengl.Matrix
         */
        public void Draw(float[] cameraView, float[] cameraPerspective, float[] colorCorrectionRgba)
        {
            Draw(cameraView, cameraPerspective, colorCorrectionRgba, DEFAULT_COLOR);
        }

        public void Draw(
            float[] cameraView,
            float[] cameraPerspective,
            float[] colorCorrectionRgba,
            float[] objColor)
        {

            ShaderHelper.CheckGLError("", "Before draw");

            // Build the ModelView and ModelViewProjection matrices
            // for calculating object position and light.
            Matrix.MultiplyMM(modelViewMatrix, 0, cameraView, 0, modelMatrix, 0);
            Matrix.MultiplyMM(modelViewProjectionMatrix, 0, cameraPerspective, 0, modelViewMatrix, 0);

            GLES20.GlUseProgram(program);

            // Set the lighting environment properties.
            Matrix.MultiplyMV(viewLightDirection, 0, modelViewMatrix, 0, LIGHT_DIRECTION, 0);
            NormalizeVec3(viewLightDirection);
            GLES20.GlUniform4f(
                lightingParametersUniform,
                viewLightDirection[0],
                viewLightDirection[1],
                viewLightDirection[2],
                1);

            GLES20.GlUniform4fv(colorCorrectionParameterUniform, 1, colorCorrectionRgba, 0);

            // Set the object color property.
            GLES20.GlUniform4fv(colorUniform, 1, objColor, 0);

            // Set the object material properties.
            GLES20.GlUniform4f(materialParametersUniform, ambient, diffuse, specular, specularPower);

            // Attach the object texture.
            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlBindTexture(GLES20.GlTexture2d, textures[0]);
            GLES20.GlUniform1i(textureUniform, 0);

            // Occlusion parameters.
            if (useDepthForOcclusion)
            {
                // Attach the depth texture.
                GLES20.GlActiveTexture(GLES20.GlTexture1);
                GLES20.GlBindTexture(GLES20.GlTexture2d, depthTextureId);
                GLES20.GlUniform1i(depthTextureUniform, 1);

                // Set the depth texture uv transform.
                GLES20.GlUniformMatrix3fv(depthUvTransformUniform, 1, false, uvTransform, 0);
                GLES20.GlUniform1f(depthAspectRatioUniform, depthAspectRatio);
            }

            // Set the vertex attributes.
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, vertexBufferId);

            GLES20.GlVertexAttribPointer(
                positionAttribute, COORDS_PER_VERTEX, GLES20.GlFloat, false, 0, verticesBaseAddress);
            GLES20.GlVertexAttribPointer(normalAttribute, 3, GLES20.GlFloat, false, 0, normalsBaseAddress);
            GLES20.GlVertexAttribPointer(
                texCoordAttribute, 2, GLES20.GlFloat, false, 0, texCoordsBaseAddress);

            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

            // Set the ModelViewProjection matrix in the shader.
            GLES20.GlUniformMatrix4fv(modelViewUniform, 1, false, modelViewMatrix, 0);
            GLES20.GlUniformMatrix4fv(modelViewProjectionUniform, 1, false, modelViewProjectionMatrix, 0);

            // Enable vertex arrays
            GLES20.GlEnableVertexAttribArray(positionAttribute);
            GLES20.GlEnableVertexAttribArray(normalAttribute);
            GLES20.GlEnableVertexAttribArray(texCoordAttribute);

            if (blendMode != null)
            {
                GLES20.GlEnable(GLES20.GlBlend);
                switch (blendMode)
                {
                    case BlendMode.Shadow:
                        // Multiplicative blending function for Shadow.
                        GLES20.GlDepthMask(false);
                        GLES20.GlBlendFunc(GLES20.GlZero, GLES20.GlOneMinusSrcAlpha);
                        break;
                    case BlendMode.AlphaBlending:
                        // Alpha blending function, with the depth mask enabled.
                        GLES20.GlDepthMask(true);

                        // Textures are loaded with premultiplied alpha
                        // (https://developer.android.com/reference/android/graphics/BitmapFactory.Options#inPremultiplied),
                        // so we use the premultiplied alpha blend factors.
                        GLES20.GlBlendFunc(GLES20.GlNone, GLES20.GlOneMinusSrcAlpha);
                        break;
                }
            }

            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, indexBufferId);
            GLES20.GlDrawElements(GLES20.GlTriangles, indexCount, GLES20.GlUnsignedShort, 0);
            GLES20.GlBindBuffer(GLES20.GlElementArrayBuffer, 0);

            if (blendMode != null)
            {
                GLES20.GlDisable(GLES20.GlBlend);
                GLES20.GlDepthMask(true);
            }

            // Disable vertex arrays
            GLES20.GlDisableVertexAttribArray(positionAttribute);
            GLES20.GlDisableVertexAttribArray(normalAttribute);
            GLES20.GlDisableVertexAttribArray(texCoordAttribute);

            GLES20.GlBindTexture(GLES20.GlTexture2d, 0);

            ShaderHelper.CheckGLError("", "After draw");
        }

        private static void NormalizeVec3(float[] v)
        {
            float reciprocalLength = 1.0f / (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            v[0] *= reciprocalLength;
            v[1] *= reciprocalLength;
            v[2] *= reciprocalLength;
        }

        public void SetUvTransformMatrix(float[] transform)
        {
            uvTransform = transform;
        }

        public void SetDepthTexture(int textureId, int width, int height)
        {
            depthTextureId = textureId;
            depthAspectRatio = (float)width / (float)height;
        }
    }
}
