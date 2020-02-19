using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

namespace UnityEditor.Rendering.HighDefinition
{
    class FBXMaterialDescriptionPreprocessor : AssetPostprocessor
    {
        static readonly uint k_Version = 1;
        static readonly int k_Order = 2;
        static readonly string k_ShaderPath = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader";
        public override uint GetVersion()
        {
            return k_Version;
        }

        public override int GetPostprocessOrder()
        {
            return k_Order;
        }

        [CollectImportedDependencies(typeof(ModelImporter), 1)]
        public static string[] CollectImportedDependenciesForModelImporter(string assetPath)
        {
            return new[] { k_ShaderPath };
        }

        public void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            var lowerCaseExtension = Path.GetExtension(assetPath).ToLower();
            if (lowerCaseExtension != ".fbx" && lowerCaseExtension != ".obj" && lowerCaseExtension != ".dae" && lowerCaseExtension != ".obj" && lowerCaseExtension != ".blend" && lowerCaseExtension != ".mb" && lowerCaseExtension != ".ma" && lowerCaseExtension != ".max")
                return;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(k_ShaderPath);
            if (shader == null)
                return;

            material.shader = shader;

            material.SetShaderPassEnabled("DistortionVectors", false);
            material.SetShaderPassEnabled("TransparentDepthPrepass", false);
            material.SetShaderPassEnabled("TransparentDepthPostpass", false);
            material.SetShaderPassEnabled("TransparentBackface", false);
            material.SetShaderPassEnabled("MOTIONVECTORS", false);

            Vector4 vectorProperty;
            float floatProperty;
            TexturePropertyDescription textureProperty;

            bool isTransparent = false;

            float opacity;
            float transparencyFactor;
            if (!description.TryGetProperty("Opacity", out opacity))
            {
                if (description.TryGetProperty("TransparencyFactor", out transparencyFactor))
                {
                    opacity = transparencyFactor == 1.0f ? 1.0f : 1.0f - transparencyFactor;
                }
                if (opacity == 1.0f && description.TryGetProperty("TransparentColor", out vectorProperty))
                {
                    opacity = vectorProperty.x == 1.0f ? 1.0f : 1.0f - vectorProperty.x;
                }
            }
            if (opacity < 1.0f || (opacity == 1.0f && description.TryGetProperty("TransparentColor", out textureProperty)))
            {
                isTransparent = true;
            }
            else if (description.HasAnimationCurve("TransparencyFactor") || description.HasAnimationCurve("TransparentColor"))
            {
                isTransparent = true;
            }

            if (isTransparent)
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                material.EnableKeyword("_BLENDMODE_ALPHA");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.renderQueue = -1;
            }

            if (description.TryGetProperty("DiffuseColor", out textureProperty) && textureProperty.texture!=null)
            {
                Color diffuseColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                if (description.TryGetProperty("DiffuseFactor", out floatProperty))
                    diffuseColor *= floatProperty;
                diffuseColor.a = opacity;

                SetMaterialTextureProperty("_BaseColorMap", material, textureProperty);
                material.SetColor("_BaseColor", diffuseColor);
            }
            else if (description.TryGetProperty("DiffuseColor", out vectorProperty))
            {
                Color diffuseColor = vectorProperty;
                if (description.TryGetProperty("DiffuseFactor", out floatProperty))
                    diffuseColor *= floatProperty;
                diffuseColor.a = opacity;
                material.SetColor("_BaseColor", PlayerSettings.colorSpace == ColorSpace.Linear ? diffuseColor.gamma : diffuseColor);
            }

            if (description.TryGetProperty("Bump", out textureProperty) && textureProperty.texture != null)
            {
                SetMaterialTextureProperty("_BumpMap", material, textureProperty);
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

                if (description.TryGetProperty("BumpFactor", out floatProperty))
                    material.SetFloat("_BumpScale", floatProperty);
            }
            else if (description.TryGetProperty("NormalMap", out textureProperty) && textureProperty.texture != null)
            {
                SetMaterialTextureProperty("_BumpMap", material, textureProperty);
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

                if (description.TryGetProperty("BumpFactor", out floatProperty))
                    material.SetFloat("_BumpScale", floatProperty);
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            if (description.TryGetProperty("EmissiveColor", out textureProperty))
            {
                Color emissiveColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

                material.SetColor("_EmissionColor", emissiveColor);
                material.SetColor("_EmissiveColor", emissiveColor);
                SetMaterialTextureProperty("_EmissionMap", material, textureProperty);

                if (description.TryGetProperty("EmissiveFactor", out floatProperty) && floatProperty > 0.0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
            else if ( description.TryGetProperty("EmissiveColor", out vectorProperty) && vectorProperty.magnitude > vectorProperty.w
                || description.HasAnimationCurve("EmissiveColor.x"))
            {
                if (description.TryGetProperty("EmissiveFactor", out floatProperty))
                    vectorProperty *= floatProperty;

                material.SetColor("_EmissionColor", vectorProperty);
                material.SetColor("_EmissiveColor", vectorProperty);
                if (floatProperty > 0.0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }

            material.SetFloat("_Glossiness", 0.0f);

            if (PlayerSettings.colorSpace == ColorSpace.Linear)
                RemapAndTransformColorCurves(description, clips, "DiffuseColor", "_BaseColor", ConvertFloatLinearToGamma);
            else
                RemapColorCurves(description, clips, "DiffuseColor", "_BaseColor");

            RemapTransparencyCurves(description, clips);

            RemapColorCurves(description, clips, "EmissiveColor", "_EmissionColor");
            RemapColorCurves(description, clips, "EmissiveColor", "_EmissiveColor");
        }

        static void RemapTransparencyCurves(MaterialDescription description, AnimationClip[] clips)
        {
            // For some reason, Opacity is never animated, we have to use TransparencyFactor and TransparentColor
            for (int i = 0; i < clips.Length; i++)
            {
                bool foundTransparencyCurve = false;
                AnimationCurve curve;
                if (description.TryGetAnimationCurve(clips[i].name, "TransparencyFactor", out curve))
                {
                    ConvertKeys(curve, ConvertFloatOneMinus);
                    clips[i].SetCurve("", typeof(Material), "_BaseColor.a", curve);
                    foundTransparencyCurve = true;
                }
                else if (description.TryGetAnimationCurve(clips[i].name, "TransparentColor.x", out curve))
                {
                    ConvertKeys(curve, ConvertFloatOneMinus);
                    clips[i].SetCurve("", typeof(Material), "_BaseColor.a", curve);
                    foundTransparencyCurve = true;
                }

                if (foundTransparencyCurve && !description.HasAnimationCurveInClip(clips[i].name, "DiffuseColor"))
                {
                    Vector4 diffuseColor;
                    description.TryGetProperty("DiffuseColor", out diffuseColor);
                    clips[i].SetCurve("", typeof(Material), "_BaseColor.r", AnimationCurve.Constant(0.0f, 1.0f, diffuseColor.x));
                    clips[i].SetCurve("", typeof(Material), "_BaseColor.g", AnimationCurve.Constant(0.0f, 1.0f, diffuseColor.y));
                    clips[i].SetCurve("", typeof(Material), "_BaseColor.b", AnimationCurve.Constant(0.0f, 1.0f, diffuseColor.z));
                }
            }
        }

        static void RemapColorCurves(MaterialDescription description, AnimationClip[] clips, string originalPropertyName, string newPropertyName)
        {
            AnimationCurve curve;
            for (int i = 0; i < clips.Length; i++)
            {
                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".x", out curve))
                {
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".r", curve);
                }

                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".y", out curve))
                {
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".g", curve);
                }

                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".z", out curve))
                {
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".b", curve);
                }
            }
        }

        static void RemapAndTransformColorCurves(MaterialDescription description, AnimationClip[] clips, string originalPropertyName, string newPropertyName, System.Func<float, float> converter)
        {
            AnimationCurve curve;
            for (int i = 0; i < clips.Length; i++)
            {
                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".x", out curve))
                {
                    ConvertKeys(curve, converter);
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".r", curve);
                }

                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".y", out curve))
                {
                    ConvertKeys(curve, converter);
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".g", curve);
                }

                if (description.TryGetAnimationCurve(clips[i].name, originalPropertyName + ".z", out curve))
                {
                    ConvertKeys(curve, converter);
                    clips[i].SetCurve("", typeof(Material), newPropertyName + ".b", curve);
                }
            }
        }

        static float ConvertFloatLinearToGamma(float value)
        {
            return Mathf.LinearToGammaSpace(value);
        }

        static float ConvertFloatOneMinus(float value)
        {
            return 1.0f - value;
        }

        static void ConvertKeys(AnimationCurve curve, System.Func<float, float> convertionDelegate)
        {
            Keyframe[] keyframes = curve.keys;
            for (int i = 0; i < keyframes.Length; i++)
            {
                keyframes[i].value = convertionDelegate(keyframes[i].value);
            }
            curve.keys = keyframes;
        }

        static void SetMaterialTextureProperty(string propertyName, Material material, TexturePropertyDescription textureProperty)
        {
            material.SetTexture(propertyName, textureProperty.texture);
            material.SetTextureOffset(propertyName, textureProperty.offset);
            material.SetTextureScale(propertyName, textureProperty.scale);
        }
    }
}