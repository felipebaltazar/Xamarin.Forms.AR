using System;
using System.Collections.Generic;
using System.Diagnostics;
using Android.Content;
using Android.Opengl;
using Java.IO;
using Java.Lang;

namespace Xamarin.Forms.AR.Helpers
{
    public static class ShaderHelper
    {
        /**
         * Converts a raw text file, saved as a resource, into an OpenGL ES shader.
         *
         * @param type The type of shader we will be creating.
         * @param filename The filename of the asset file about to be turned into a shader.
         * @param defineValuesMap The #define values to add to the top of the shader source code.
         * @return The shader object handler.
         */
        public static int LoadGLShader(Context context, int type, string filename, Dictionary<string, int> defineValuesMap)
        {
            // Load shader source code.
            var code = ReadShaderFileFromAssets(context, filename);

            // Prepend any #define values specified during this run.
            var defines = "";
            foreach (var entry in defineValuesMap)
            {
                defines += "#define " + entry.Key + " " + entry.Value + "\n";
            }

            code = defines + code;

            // Compiles shader code.
            int shader = GLES20.GlCreateShader(type);
            GLES20.GlShaderSource(shader, code);
            GLES20.GlCompileShader(shader);

            // Get the compilation status.
            var compileStatus = new int[1];
            GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, compileStatus, 0);

            // If the compilation failed, delete the shader.
            if (compileStatus[0] == 0)
            {
                Debug.Write("Error compiling shader: " + GLES20.GlGetShaderInfoLog(shader));
                GLES20.GlDeleteShader(shader);
                shader = 0;
            }

            if (shader == 0)
                throw new RuntimeException("Error creating shader.");

            return shader;
        }

        /** Overload of loadGLShader that assumes no additional #define values to add. */
        public static int LoadGLShader(Context context, int type, string filename)
        {
            var emptyDefineValuesMap = new Dictionary<string, int>();
            return LoadGLShader(context, type, filename, emptyDefineValuesMap);
        }

        /**
         * Checks if we've had an error inside of OpenGL ES, and if so what that error is.
         *
         * @param label Label to report in case of error.
         * @throws RuntimeException If an OpenGL error is detected.
         */
        public static void CheckGLError(string tag, string label)
        {
            var lastError = GLES20.GlNoError;
            // Drain the queue of all errors.
            int error;
            while ((error = GLES20.GlGetError()) != GLES20.GlNoError)
            {
                Debug.Write(label + ": glError " + error);
                lastError = error;
            }

            if (lastError != GLES20.GlNoError)
            {
                throw new RuntimeException(label + ": glError " + lastError);
            }
        }

        /**
         * Converts a raw shader file into a string.
         *
         * @param filename The filename of the shader file about to be turned into a shader.
         * @return The context of the text file, or null in case of error.
         */
        private static string ReadShaderFileFromAssets(Context context, string filename)
        {
            try
            {
                using (var inputStream = context.Assets.Open(filename))
                {
                    using (var reader = new BufferedReader(new InputStreamReader(inputStream)))
                    {
                        var sb = new StringBuilder();
                        var line = string.Empty;
                        while ((line = reader.ReadLine()) != null)
                        {
                            var tokens = line.Split(" ", -1);
                            if (tokens[0].Equals("#include"))
                            {
                                var includeFilename = tokens[1];
                                includeFilename = includeFilename.Replace("\"", "");

                                if (includeFilename.Equals(filename))
                                    throw new IOException("Do not include the calling file.");

                                sb.Append(ReadShaderFileFromAssets(context, includeFilename));
                            }
                            else
                            {
                                sb.Append(line).Append("\n");
                            }
                        }
                        return sb.ToString();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Write(ex.Message);
            }

            return string.Empty;
        }
    }
}
