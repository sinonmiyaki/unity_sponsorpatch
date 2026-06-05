using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yumesora.MergeSponsor.Editor
{
    public sealed class MergeSponsorWindow : EditorWindow
    {
        private const int PreviewMaxSize = 512;

        [SerializeField] private Texture2D baseTexture;
        [SerializeField] private Texture2D sponsorTexture;
        [SerializeField] private bool autoResizeSponsor = true;
        [SerializeField] private bool configureImporter = true;
        [SerializeField] private bool revealAfterApply = false;
        [SerializeField] private float sponsorOpacity = 1f;

        private Vector2 scrollPosition;
        private Texture2D mergedPreview;
        private Texture2D checkerTexture;
        private bool previewDirty = true;
        private string statusMessage;
        private MessageType statusType = MessageType.Info;

        [MenuItem("YUMESORA/Merge Sponsor")]
        public static void OpenWindow()
        {
            MergeSponsorWindow window = GetWindow<MergeSponsorWindow>(true, "Merge Sponsor", true);
            window.minSize = new Vector2(460f, 580f);
            window.Show();
        }

        private void OnDisable()
        {
            DestroyPreviewTextures();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawTextureSection();
            EditorGUILayout.Space(8f);
            DrawSettingsSection();
            EditorGUILayout.Space(8f);
            DrawPreviewSection();
            EditorGUILayout.Space(8f);
            DrawActionSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("YUMESORA Merge Sponsor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Overwrite the original PNG with a merged sponsor texture, while keeping a _backup PNG for revert.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawTextureSection()
        {
            using (new SectionScope("Textures"))
            {
                EditorGUI.BeginChangeCheck();

                baseTexture = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent("Original PNG", "The texture file that will be overwritten when Apply is pressed."),
                    baseTexture,
                    typeof(Texture2D),
                    false);

                sponsorTexture = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent("Sponsor Kit PNG", "A transparent PNG aligned to the original UV texture."),
                    sponsorTexture,
                    typeof(Texture2D),
                    false);

                if (EditorGUI.EndChangeCheck())
                {
                    previewDirty = true;
                    ClearStatus();
                }

                DrawBackupStatus();
            }
        }

        private void DrawSettingsSection()
        {
            using (new SectionScope("Options"))
            {
                EditorGUI.BeginChangeCheck();

                sponsorOpacity = EditorGUILayout.Slider(
                    new GUIContent("Sponsor Opacity", "Multiplies sponsor alpha before merging."),
                    sponsorOpacity,
                    0f,
                    1f);

                autoResizeSponsor = EditorGUILayout.ToggleLeft(
                    new GUIContent("Auto resize sponsor kit to original size", "Useful when the sponsor kit has the same ratio but a different resolution."),
                    autoResizeSponsor);

                configureImporter = EditorGUILayout.ToggleLeft(
                    new GUIContent("Keep original import size", "Raises the texture max size if needed so 4096 PNGs stay 4096 in Unity."),
                    configureImporter);

                revealAfterApply = EditorGUILayout.ToggleLeft(
                    new GUIContent("Reveal file after apply", "Reveals the overwritten original PNG after Apply or Revert."),
                    revealAfterApply);

                if (EditorGUI.EndChangeCheck())
                {
                    previewDirty = true;
                    ClearStatus();
                }

                DrawSizeWarning();
            }
        }

        private void DrawPreviewSection()
        {
            using (new SectionScope("Preview"))
            {
                using (new EditorGUI.DisabledScope(!CanPreview()))
                {
                    if (GUILayout.Button("Refresh Preview"))
                    {
                        RebuildPreview();
                    }
                }

                if (previewDirty && CanPreview())
                {
                    RebuildPreview();
                }

                Rect previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MinHeight(220f), GUILayout.ExpandWidth(true));
                DrawCheckerboard(previewRect);

                if (mergedPreview != null)
                {
                    EditorGUI.DrawPreviewTexture(previewRect, mergedPreview, null, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Label(previewRect, "Select original and sponsor PNGs.", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawActionSection()
        {
            using (new SectionScope("Apply / Revert"))
            {
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    EditorGUILayout.HelpBox(statusMessage, statusType);
                }

                bool canApply = baseTexture != null && sponsorTexture != null;
                bool canRevert = HasBackup();

                using (new EditorGUI.DisabledScope(!canApply))
                {
                    if (GUILayout.Button("Apply To Original", GUILayout.Height(34f)))
                    {
                        ApplyMerge();
                    }
                }

                using (new EditorGUI.DisabledScope(!canRevert))
                {
                    if (GUILayout.Button("Revert From Backup", GUILayout.Height(28f)))
                    {
                        RevertFromBackup();
                    }
                }
            }
        }

        private void DrawBackupStatus()
        {
            if (baseTexture == null)
            {
                EditorGUILayout.HelpBox("Select an original PNG to see backup status.", MessageType.Info);
                return;
            }

            string basePath = AssetDatabase.GetAssetPath(baseTexture);
            if (string.IsNullOrEmpty(basePath))
            {
                EditorGUILayout.HelpBox("Original texture must be an asset inside this Unity project.", MessageType.Warning);
                return;
            }

            if (!IsPngPath(basePath))
            {
                EditorGUILayout.HelpBox("Original texture must be a PNG file.", MessageType.Error);
                return;
            }

            string backupPath = GetBackupPath(basePath);
            if (File.Exists(GetFullProjectPath(backupPath)))
            {
                EditorGUILayout.HelpBox("Backup ready for Revert: " + backupPath, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Apply will create: " + backupPath, MessageType.None);
            }
        }

        private void DrawSizeWarning()
        {
            if (baseTexture == null || sponsorTexture == null)
            {
                return;
            }

            if (baseTexture.width == sponsorTexture.width && baseTexture.height == sponsorTexture.height)
            {
                return;
            }

            string message = string.Format(
                "Texture sizes differ. Original: {0} x {1}, sponsor: {2} x {3}.",
                baseTexture.width,
                baseTexture.height,
                sponsorTexture.width,
                sponsorTexture.height);

            if (!HasSameAspectRatio(baseTexture.width, baseTexture.height, sponsorTexture.width, sponsorTexture.height))
            {
                EditorGUILayout.HelpBox(message + " The sponsor kit must use the same aspect ratio to avoid distortion.", MessageType.Error);
                return;
            }

            if (autoResizeSponsor)
            {
                EditorGUILayout.HelpBox(message + " The sponsor kit will be resized during Apply.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(message + " Enable auto resize or use a same-size sponsor kit.", MessageType.Error);
            }
        }

        private void ApplyMerge()
        {
            try
            {
                string basePath = ValidateOriginalTexture();
                ValidateSponsorTexture();

                string backupPath = GetBackupPath(basePath);
                bool backupExists = File.Exists(GetFullProjectPath(backupPath));
                string confirmMessage = backupExists
                    ? "This will overwrite the current original PNG. The existing backup will be preserved. If you want to try again from the backed-up original, run Revert first."
                    : "This will overwrite the selected original PNG and create a _backup PNG next to it.";

                if (!EditorUtility.DisplayDialog("Merge Sponsor", confirmMessage, "Apply", "Cancel"))
                {
                    return;
                }

                EditorUtility.DisplayProgressBar("Merge Sponsor", "Merging sponsor texture", 0.35f);
                Texture2D merged = BuildMergedTexture();

                try
                {
                    EditorUtility.DisplayProgressBar("Merge Sponsor", "Writing original PNG", 0.75f);
                    EnsureBackupExists(basePath);
                    WriteTextureToOriginal(merged, basePath);
                }
                finally
                {
                    DestroyImmediateIfNeeded(merged);
                }

                AssetDatabase.Refresh();
                FocusOriginalAsset(basePath);
                previewDirty = true;

                SetStatus("Applied to original: " + basePath + "\nBackup: " + backupPath, MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, MessageType.Error);
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RevertFromBackup()
        {
            try
            {
                string basePath = ValidateOriginalTexture();
                string backupPath = GetBackupPath(basePath);
                string backupFullPath = GetFullProjectPath(backupPath);

                if (!File.Exists(backupFullPath))
                {
                    SetStatus("No backup found for this original PNG.", MessageType.Warning);
                    return;
                }

                if (!EditorUtility.DisplayDialog(
                        "Revert Merge Sponsor",
                        "This will restore the backup over the original PNG, then delete the backup file.",
                        "Revert",
                        "Cancel"))
                {
                    return;
                }

                File.Copy(backupFullPath, GetFullProjectPath(basePath), true);
                AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);
                DeleteBackupAsset(backupPath);
                AssetDatabase.Refresh();
                FocusOriginalAsset(basePath);

                previewDirty = true;
                SetStatus("Reverted original and deleted backup: " + backupPath, MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, MessageType.Error);
                Debug.LogException(exception);
            }
        }

        private Texture2D BuildMergedTexture()
        {
            Texture2D sourceBase = null;
            Texture2D sourceSponsor = null;

            try
            {
                sourceBase = LoadTextureAsset(baseTexture);
                sourceSponsor = LoadTextureAsset(sponsorTexture);
                return MergeTextures(sourceBase, sourceSponsor, autoResizeSponsor, sponsorOpacity);
            }
            finally
            {
                DestroyImmediateIfNeeded(sourceBase);
                DestroyImmediateIfNeeded(sourceSponsor);
            }
        }

        private void WriteTextureToOriginal(Texture2D texture, string basePath)
        {
            byte[] png = ImageConversion.EncodeToPNG(texture);
            if (png == null || png.Length == 0)
            {
                throw new InvalidOperationException("Failed to encode merged texture as PNG.");
            }

            File.WriteAllBytes(GetFullProjectPath(basePath), png);
            AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);

            if (configureImporter)
            {
                ConfigureTextureImporter(basePath, texture.width, texture.height);
            }
        }

        private void EnsureBackupExists(string basePath)
        {
            string backupPath = GetBackupPath(basePath);
            string backupFullPath = GetFullProjectPath(backupPath);

            if (File.Exists(backupFullPath))
            {
                return;
            }

            File.Copy(GetFullProjectPath(basePath), backupFullPath);
            AssetDatabase.ImportAsset(backupPath, ImportAssetOptions.ForceUpdate);
        }

        private void DeleteBackupAsset(string backupPath)
        {
            if (!AssetDatabase.DeleteAsset(backupPath))
            {
                string backupFullPath = GetFullProjectPath(backupPath);
                if (File.Exists(backupFullPath))
                {
                    File.Delete(backupFullPath);
                }

                string metaPath = backupFullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
        }

        private void ConfigureTextureImporter(string texturePath, int width, int height)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 8192);
            importer.SaveAndReimport();
        }

        private void FocusOriginalAsset(string basePath)
        {
            UnityEngine.Object original = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            if (original != null)
            {
                Selection.activeObject = original;
                EditorGUIUtility.PingObject(original);
            }

            if (revealAfterApply)
            {
                EditorUtility.RevealInFinder(GetFullProjectPath(basePath));
            }
        }

        private void RebuildPreview()
        {
            DestroyImmediateIfNeeded(mergedPreview);
            mergedPreview = null;

            if (!CanPreview())
            {
                previewDirty = false;
                return;
            }

            Texture2D sourceBase = null;
            Texture2D sourceSponsor = null;

            try
            {
                sourceBase = LoadTextureAsset(baseTexture);
                sourceSponsor = LoadTextureAsset(sponsorTexture);
                mergedPreview = BuildPreviewTexture(sourceBase, sourceSponsor, autoResizeSponsor, sponsorOpacity);
                mergedPreview.hideFlags = HideFlags.HideAndDontSave;
                previewDirty = false;
            }
            catch (Exception exception)
            {
                previewDirty = false;
                SetStatus("Preview failed: " + exception.Message, MessageType.Warning);
            }
            finally
            {
                DestroyImmediateIfNeeded(sourceBase);
                DestroyImmediateIfNeeded(sourceSponsor);
            }
        }

        private string ValidateOriginalTexture()
        {
            if (baseTexture == null)
            {
                throw new InvalidOperationException("Original PNG is missing.");
            }

            string basePath = AssetDatabase.GetAssetPath(baseTexture);
            if (string.IsNullOrEmpty(basePath) || !basePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Original PNG must be an asset inside the Assets folder.");
            }

            if (!IsPngPath(basePath))
            {
                throw new InvalidOperationException("Original texture must be a PNG file.");
            }

            if (!File.Exists(GetFullProjectPath(basePath)))
            {
                throw new InvalidOperationException("Original PNG file does not exist on disk.");
            }

            return basePath;
        }

        private void ValidateSponsorTexture()
        {
            if (sponsorTexture == null)
            {
                throw new InvalidOperationException("Sponsor Kit PNG is missing.");
            }

            string sponsorPath = AssetDatabase.GetAssetPath(sponsorTexture);
            if (!string.IsNullOrEmpty(sponsorPath) && !IsPngPath(sponsorPath))
            {
                throw new InvalidOperationException("Sponsor kit must be a PNG file.");
            }
        }

        private bool HasBackup()
        {
            if (baseTexture == null)
            {
                return false;
            }

            string basePath = AssetDatabase.GetAssetPath(baseTexture);
            return IsPngPath(basePath) && File.Exists(GetFullProjectPath(GetBackupPath(basePath)));
        }

        private bool CanPreview()
        {
            return baseTexture != null && sponsorTexture != null;
        }

        private static Texture2D BuildPreviewTexture(Texture2D sourceBase, Texture2D sourceSponsor, bool allowResize, float opacity)
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

            int previewWidth;
            int previewHeight;
            CalculatePreviewSize(sourceBase.width, sourceBase.height, out previewWidth, out previewHeight);

            Color32[] basePixels = sourceBase.GetPixels32();
            Color32[] sponsorPixels = sourceSponsor.GetPixels32();
            Color32[] outputPixels = new Color32[previewWidth * previewHeight];

            for (int y = 0; y < previewHeight; y++)
            {
                for (int x = 0; x < previewWidth; x++)
                {
                    Color32 baseColor = SampleNearest(basePixels, sourceBase.width, sourceBase.height, x, y, previewWidth, previewHeight);
                    Color32 sponsorColor = SampleBilinear(sponsorPixels, sourceSponsor.width, sourceSponsor.height, x, y, previewWidth, previewHeight);
                    outputPixels[y * previewWidth + x] = AlphaBlend(baseColor, sponsorColor, opacity);
                }
            }

            Texture2D preview = new Texture2D(previewWidth, previewHeight, TextureFormat.RGBA32, false);
            preview.SetPixels32(outputPixels);
            preview.Apply(false, false);
            return preview;
        }

        private static void CalculatePreviewSize(int width, int height, out int previewWidth, out int previewHeight)
        {
            float scale = Mathf.Min(1f, PreviewMaxSize / (float)Mathf.Max(width, height));
            previewWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            previewHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
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

        private static Color32 SampleNearest(
            Color32[] pixels,
            int sourceWidth,
            int sourceHeight,
            int outputX,
            int outputY,
            int outputWidth,
            int outputHeight)
        {
            int sourceX = Mathf.RoundToInt(NormalizeCoordinate(outputX, outputWidth) * (sourceWidth - 1));
            int sourceY = Mathf.RoundToInt(NormalizeCoordinate(outputY, outputHeight) * (sourceHeight - 1));
            return pixels[sourceY * sourceWidth + sourceX];
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

        private static Texture2D LoadTextureAsset(Texture2D source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string fullPath = GetFullProjectPath(assetPath);
                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(loaded, bytes, false))
                    {
                        loaded.name = source.name + "_Loaded";
                        loaded.hideFlags = HideFlags.HideAndDontSave;
                        return loaded;
                    }

                    DestroyImmediateIfNeeded(loaded);
                }
            }

            return CopyTextureToReadable(source);
        }

        private static Texture2D CopyTextureToReadable(Texture2D source)
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
                readable.name = source.name + "_Readable";
                readable.hideFlags = HideFlags.HideAndDontSave;
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void DrawCheckerboard(Rect rect)
        {
            if (checkerTexture == null)
            {
                checkerTexture = CreateCheckerTexture();
            }

            GUI.DrawTextureWithTexCoords(
                rect,
                checkerTexture,
                new Rect(0f, 0f, rect.width / checkerTexture.width, rect.height / checkerTexture.height));
        }

        private static Texture2D CreateCheckerTexture()
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color32 light = new Color32(190, 190, 190, 255);
            Color32 dark = new Color32(130, 130, 130, 255);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool lightSquare = ((x / 8) + (y / 8)) % 2 == 0;
                    pixels[y * size + x] = lightSquare ? light : dark;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static string GetBackupPath(string basePath)
        {
            string directory = Path.GetDirectoryName(basePath);
            string fileName = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            string backupName = fileName + "_backup" + extension;

            return string.IsNullOrEmpty(directory)
                ? backupName
                : (directory + "/" + backupName).Replace("\\", "/");
        }

        private static string GetFullProjectPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static bool IsPngPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSameAspectRatio(int firstWidth, int firstHeight, int secondWidth, int secondHeight)
        {
            return (long)firstWidth * secondHeight == (long)secondWidth * firstHeight;
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }

        private void ClearStatus()
        {
            statusMessage = null;
        }

        private void DestroyPreviewTextures()
        {
            DestroyImmediateIfNeeded(mergedPreview);
            DestroyImmediateIfNeeded(checkerTexture);
            mergedPreview = null;
            checkerTexture = null;
        }

        private static void DestroyImmediateIfNeeded(UnityEngine.Object target)
        {
            if (target != null)
            {
                DestroyImmediate(target);
            }
        }

        private sealed class SectionScope : IDisposable
        {
            public SectionScope(string title)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }
    }
}
