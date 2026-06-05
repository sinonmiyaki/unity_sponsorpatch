using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yumesora.MergeSponsor
{
    [DisallowMultipleComponent]
    public sealed class YumesoraSponsorTool : MonoBehaviour
    {
        private const string DefaultTextureProperty = "_MainTex";
        [SerializeField] private string toolName = "Sponsor";
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Texture2D originalTexture;
        [SerializeField] private string textureProperty = DefaultTextureProperty;
        [SerializeField] private bool autoApplyOnPlay = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private bool autoResizeSponsor = true;
        [SerializeField] private List<SponsorKit> sponsorKits = new List<SponsorKit>();

        private readonly List<RendererState> rendererStates = new List<RendererState>();
        private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();
        private bool isApplied;

        public string ToolName
        {
            get { return toolName; }
        }

        public Transform TargetRoot
        {
            get { return ResolveTargetRoot(); }
        }

        public Texture2D OriginalTexture
        {
            get { return originalTexture; }
        }

        public string TextureProperty
        {
            get { return string.IsNullOrWhiteSpace(textureProperty) ? DefaultTextureProperty : textureProperty; }
        }

        public bool IsApplied
        {
            get { return isApplied; }
        }

        public IReadOnlyList<SponsorKit> SponsorKits
        {
            get { return sponsorKits; }
        }

        private void Reset()
        {
            targetRoot = transform.parent != null ? transform.parent : transform;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && autoApplyOnPlay)
            {
                ApplyTemporary();
            }
        }

        private void OnDisable()
        {
            RestoreTemporary();
        }

        private void OnDestroy()
        {
            RestoreTemporary();
        }

        public void SetTargetRoot(Transform root)
        {
            targetRoot = root;
        }

        public bool ApplyTemporary()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("YUMESORA Sponsor Tool can apply temporary textures only while Unity is in Play Mode.", this);
                return false;
            }

            if (isApplied)
            {
                return true;
            }

            string error;
            Texture2D mergedTexture = BuildMergedTexture(out error);
            if (mergedTexture == null)
            {
                Debug.LogWarning(error, this);
                return false;
            }

            int changedSlots = ApplyTextureToMaterials(mergedTexture);
            if (changedSlots == 0)
            {
                DestroyObject(mergedTexture);
                Debug.LogWarning("YUMESORA Sponsor Tool could not find a material slot using the original texture.", this);
                return false;
            }

            temporaryObjects.Add(mergedTexture);
            isApplied = true;
            return true;
        }

        public void RestoreTemporary()
        {
            if (!isApplied && rendererStates.Count == 0 && temporaryObjects.Count == 0)
            {
                return;
            }

            for (int i = rendererStates.Count - 1; i >= 0; i--)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }
            }

            rendererStates.Clear();

            for (int i = temporaryObjects.Count - 1; i >= 0; i--)
            {
                DestroyObject(temporaryObjects[i]);
            }

            temporaryObjects.Clear();
            isApplied = false;
        }

        public int CountMatchingMaterialSlots()
        {
            if (originalTexture == null)
            {
                return 0;
            }

            int count = 0;
            Renderer[] renderers = ResolveTargetRoot().GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            string propertyName = TextureProperty;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (MaterialUsesOriginalTexture(material, propertyName))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public bool CanBuildMergedTexture(out string error)
        {
            error = null;

            if (originalTexture == null)
            {
                error = "Original texture is missing.";
                return false;
            }

            List<SponsorKit> activeKits = GetActiveSponsorKits();
            if (activeKits.Count == 0)
            {
                error = "At least one enabled sponsor kit texture is required.";
                return false;
            }

            for (int i = 0; i < activeKits.Count; i++)
            {
                Texture2D sponsor = activeKits[i].Texture;
                bool sameSize = originalTexture.width == sponsor.width && originalTexture.height == sponsor.height;

                if (!sameSize && !autoResizeSponsor)
                {
                    error = "Sponsor kit sizes must match the original texture, or Auto Resize Sponsor Kits must be enabled.";
                    return false;
                }

                if (!sameSize && !HasSameAspectRatio(originalTexture.width, originalTexture.height, sponsor.width, sponsor.height))
                {
                    error = "Original and sponsor kit textures must use the same aspect ratio.";
                    return false;
                }
            }

            return true;
        }

        private Texture2D BuildMergedTexture(out string error)
        {
            error = null;

            if (originalTexture == null)
            {
                error = "Original texture is missing.";
                return null;
            }

            List<SponsorKit> activeKits = GetActiveSponsorKits();
            if (activeKits.Count == 0)
            {
                error = "At least one enabled sponsor kit texture is required.";
                return null;
            }

            Texture2D current = null;

            try
            {
                current = CopyTextureToReadable(originalTexture, originalTexture.name + "_YUMESORA_Base");

                for (int i = 0; i < activeKits.Count; i++)
                {
                    SponsorKit kit = activeKits[i];
                    Texture2D sponsor = null;
                    Texture2D merged = null;

                    try
                    {
                        sponsor = CopyTextureToReadable(kit.Texture, kit.Texture.name + "_YUMESORA_Sponsor");
                        merged = MergeTextures(current, sponsor, autoResizeSponsor, kit.Opacity);
                    }
                    finally
                    {
                        DestroyObject(sponsor);
                    }

                    DestroyObject(current);
                    current = merged;
                    current.name = string.IsNullOrWhiteSpace(toolName)
                        ? "YUMESORA_MergedSponsor"
                        : "YUMESORA_" + toolName + "_MergedSponsor";
                }

                Texture2D result = current;
                current = null;
                return result;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }
            finally
            {
                DestroyObject(current);
            }
        }

        private List<SponsorKit> GetActiveSponsorKits()
        {
            List<SponsorKit> activeKits = new List<SponsorKit>();

            for (int i = 0; i < sponsorKits.Count; i++)
            {
                SponsorKit kit = sponsorKits[i];
                if (kit != null && kit.IsActive && kit.Texture != null)
                {
                    activeKits.Add(kit);
                }
            }

            return activeKits;
        }

        private int ApplyTextureToMaterials(Texture2D mergedTexture)
        {
            int changedSlots = 0;
            Renderer[] renderers = ResolveTargetRoot().GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            string propertyName = TextureProperty;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = renderers[rendererIndex];
                Material[] originalMaterials = targetRenderer.sharedMaterials;
                Material[] modifiedMaterials = null;

                for (int materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
                {
                    Material material = originalMaterials[materialIndex];
                    if (!MaterialUsesOriginalTexture(material, propertyName))
                    {
                        continue;
                    }

                    if (modifiedMaterials == null)
                    {
                        modifiedMaterials = (Material[])originalMaterials.Clone();
                    }

                    Material temporaryMaterial = new Material(material);
                    temporaryMaterial.name = material.name + " (YUMESORA Sponsor Temp)";
                    temporaryMaterial.SetTexture(propertyName, mergedTexture);

                    temporaryObjects.Add(temporaryMaterial);
                    modifiedMaterials[materialIndex] = temporaryMaterial;
                    changedSlots++;
                }

                if (modifiedMaterials != null)
                {
                    rendererStates.Add(new RendererState(targetRenderer, originalMaterials));
                    targetRenderer.sharedMaterials = modifiedMaterials;
                }
            }

            return changedSlots;
        }

        private bool MaterialUsesOriginalTexture(Material material, string propertyName)
        {
            return material != null
                && material.HasProperty(propertyName)
                && material.GetTexture(propertyName) == originalTexture;
        }

        private Transform ResolveTargetRoot()
        {
            if (targetRoot != null)
            {
                return targetRoot;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        private static Texture2D CopyTextureToReadable(Texture2D source, string textureName)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply(false, false);
                readable.name = textureName;
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Texture2D MergeTextures(Texture2D sourceBase, Texture2D sourceSponsor, bool allowResize, float opacity)
        {
            bool sameSize = sourceBase.width == sourceSponsor.width && sourceBase.height == sourceSponsor.height;
            if (!sameSize && !allowResize)
            {
                throw new InvalidOperationException(
                    "Original and sponsor textures must have the same pixel size, or auto resize must be enabled.");
            }

            if (!sameSize && !HasSameAspectRatio(sourceBase.width, sourceBase.height, sourceSponsor.width, sourceSponsor.height))
            {
                throw new InvalidOperationException(
                    "Original and sponsor textures must use the same aspect ratio.");
            }

            Color32[] basePixels = sourceBase.GetPixels32();
            Color32[] sponsorPixels = sourceSponsor.GetPixels32();
            Color32[] outputPixels = new Color32[sourceBase.width * sourceBase.height];

            for (int y = 0; y < sourceBase.height; y++)
            {
                int rowOffset = y * sourceBase.width;
                for (int x = 0; x < sourceBase.width; x++)
                {
                    Color32 baseColor = basePixels[rowOffset + x];
                    Color32 sponsorColor = sameSize
                        ? sponsorPixels[rowOffset + x]
                        : SampleBilinear(sponsorPixels, sourceSponsor.width, sourceSponsor.height, x, y, sourceBase.width, sourceBase.height);

                    outputPixels[rowOffset + x] = AlphaBlend(baseColor, sponsorColor, opacity);
                }
            }

            Texture2D output = new Texture2D(sourceBase.width, sourceBase.height, TextureFormat.RGBA32, false);
            output.SetPixels32(outputPixels);
            output.Apply(false, false);
            return output;
        }

        private static Color32 SampleBilinear(
            Color32[] pixels,
            int sourceWidth,
            int sourceHeight,
            int outputX,
            int outputY,
            int outputWidth,
            int outputHeight)
        {
            float sourceX = NormalizeCoordinate(outputX, outputWidth) * (sourceWidth - 1);
            float sourceY = NormalizeCoordinate(outputY, outputHeight) * (sourceHeight - 1);

            int x0 = Mathf.FloorToInt(sourceX);
            int y0 = Mathf.FloorToInt(sourceY);
            int x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
            int y1 = Mathf.Min(y0 + 1, sourceHeight - 1);

            float tx = sourceX - x0;
            float ty = sourceY - y0;

            Color32 c00 = pixels[y0 * sourceWidth + x0];
            Color32 c10 = pixels[y0 * sourceWidth + x1];
            Color32 c01 = pixels[y1 * sourceWidth + x0];
            Color32 c11 = pixels[y1 * sourceWidth + x1];

            float w00 = (1f - tx) * (1f - ty);
            float w10 = tx * (1f - ty);
            float w01 = (1f - tx) * ty;
            float w11 = tx * ty;

            return WeightedColor(c00, w00, c10, w10, c01, w01, c11, w11);
        }

        private static float NormalizeCoordinate(int value, int size)
        {
            return size <= 1 ? 0f : value / (float)(size - 1);
        }

        private static Color32 WeightedColor(
            Color32 c00,
            float w00,
            Color32 c10,
            float w10,
            Color32 c01,
            float w01,
            Color32 c11,
            float w11)
        {
            float alpha = 0f;
            float red = 0f;
            float green = 0f;
            float blue = 0f;

            AddPremultiplied(c00, w00, ref red, ref green, ref blue, ref alpha);
            AddPremultiplied(c10, w10, ref red, ref green, ref blue, ref alpha);
            AddPremultiplied(c01, w01, ref red, ref green, ref blue, ref alpha);
            AddPremultiplied(c11, w11, ref red, ref green, ref blue, ref alpha);

            if (alpha <= 0f)
            {
                return new Color32(0, 0, 0, 0);
            }

            return new Color32(
                FloatToByte(red / alpha),
                FloatToByte(green / alpha),
                FloatToByte(blue / alpha),
                FloatToByte(alpha));
        }

        private static void AddPremultiplied(
            Color32 color,
            float weight,
            ref float red,
            ref float green,
            ref float blue,
            ref float alpha)
        {
            float weightedAlpha = (color.a / 255f) * weight;
            alpha += weightedAlpha;
            red += (color.r / 255f) * weightedAlpha;
            green += (color.g / 255f) * weightedAlpha;
            blue += (color.b / 255f) * weightedAlpha;
        }

        private static Color32 AlphaBlend(Color32 destination, Color32 source, float opacity)
        {
            float sourceAlpha = (source.a / 255f) * Mathf.Clamp01(opacity);
            float destinationAlpha = destination.a / 255f;
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);

            if (outputAlpha <= 0f)
            {
                return new Color32(0, 0, 0, 0);
            }

            float sourceRed = source.r / 255f;
            float sourceGreen = source.g / 255f;
            float sourceBlue = source.b / 255f;
            float destinationRed = destination.r / 255f;
            float destinationGreen = destination.g / 255f;
            float destinationBlue = destination.b / 255f;

            float red = (sourceRed * sourceAlpha + destinationRed * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;
            float green = (sourceGreen * sourceAlpha + destinationGreen * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;
            float blue = (sourceBlue * sourceAlpha + destinationBlue * destinationAlpha * (1f - sourceAlpha)) / outputAlpha;

            return new Color32(
                FloatToByte(red),
                FloatToByte(green),
                FloatToByte(blue),
                FloatToByte(outputAlpha));
        }

        private static byte FloatToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static bool HasSameAspectRatio(int firstWidth, int firstHeight, int secondWidth, int secondHeight)
        {
            return (long)firstWidth * secondHeight == (long)secondWidth * firstHeight;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        [Serializable]
        public sealed class SponsorKit
        {
            [SerializeField] private bool isActive = true;
            [SerializeField] private string displayName = "Sponsor";
            [SerializeField] private Texture2D texture;
            [SerializeField, Range(0f, 1f)] private float opacity = 1f;

            public bool IsActive
            {
                get { return isActive; }
            }

            public string DisplayName
            {
                get { return displayName; }
            }

            public Texture2D Texture
            {
                get { return texture; }
            }

            public float Opacity
            {
                get { return Mathf.Clamp01(opacity); }
            }
        }

        private sealed class RendererState
        {
            public readonly Renderer Renderer;
            public readonly Material[] OriginalMaterials;

            public RendererState(Renderer renderer, Material[] originalMaterials)
            {
                Renderer = renderer;
                OriginalMaterials = originalMaterials;
            }
        }
    }
}
