using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StackIsland.EditorTools
{
    /// <summary>
    /// One-click (or batchmode) setup: art library from the Kenney models, template materials, the Main scene,
    /// build and player settings. Safe to run again.
    /// Batch: Unity.exe -batchmode -projectPath . -executeMethod StackIsland.EditorTools.ProjectSetup.RunBatch -quit
    /// </summary>
    public static class ProjectSetup
    {
        public const string ArtRoot = "Assets/_Game/Art/ThirdParty/Kenney";
        const string ResourcesFolder = "Assets/_Game/Resources";
        const string ArtLibraryPath = ResourcesFolder + "/ArtLibrary.asset";
        const string MatFolder = "Assets/_Game/Art/Materials";
        public const string MainScene = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("Stack Island/Setup Project")]
        public static void RunBatch()
        {
            ImportTmpEssentials();
            ReimportCharacters();
            BuildArtLibrary();
            CreateScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Done.");
        }

        static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            var pkg = Directory.GetDirectories("Library/PackageCache", "com.unity.ugui@*").Select(d => Path.Combine(d, "Package Resources", "TMP Essential Resources.unitypackage")).FirstOrDefault(File.Exists);
            if (pkg != null) AssetDatabase.ImportPackage(pkg, false);
        }

        /// <summary>Import rules changed for the characters (animations on): make Unity re-import them.</summary>
        static void ReimportCharacters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArtRoot + "/mini-characters" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!KenneyImportRules.IsAnimatedCharacter(path)) continue;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi != null && mi.animationType == ModelImporterAnimationType.Legacy && mi.importAnimation) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void BuildArtLibrary()
        {
            EnsureFolder(ResourcesFolder);
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(ArtLibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, ArtLibraryPath);
            }
            lib.entries.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var rel = path.Substring(ArtRoot.Length + 1);
                var kit = rel.Split('/')[0];
                var name = Path.GetFileNameWithoutExtension(path);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) lib.entries.Add(new ArtLibrary.Entry { key = $"{kit}/{name}", prefab = go });
            }
            lib.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            EnsureFolder(MatFolder);
            lib.litOpaque = MakeMat("LitOpaque", "Universal Render Pipeline/Lit", false);
            lib.litTransparent = MakeMat("LitTransparent", "Universal Render Pipeline/Lit", true);
            lib.unlitOpaque = MakeMat("UnlitOpaque", "Universal Render Pipeline/Unlit", false);
            lib.unlitTransparent = MakeMat("UnlitTransparent", "Universal Render Pipeline/Unlit", true);
            lib.ResetCache();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] ArtLibrary: {lib.entries.Count} models.");
        }

        static Material MakeMat(string name, string shader, bool transparent)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            m.SetFloat("_Smoothness", 0.2f);
            if (transparent)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = -1;
            }
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateScene()
        {
            EnsureFolder("Assets/_Game/Scenes");
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<Game>();
            EditorSceneManager.SaveScene(main, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Stack Island";
            PlayerSettings.productName = "Stack Island";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.vSyncCount = 1;
        }
    }

    /// <summary>Desktop builds with matching scenes, settings and archive layout.</summary>
    public static class BuildScript
    {
        [MenuItem("Stack Island/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, false);

        [MenuItem("Stack Island/Build Windows + Zip (to share)")]
        public static void BuildWindowsZip() => Build(BuildTarget.StandaloneWindows64, true);

        [MenuItem("Stack Island/Build Linux")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, false);

        [MenuItem("Stack Island/Build Linux + Zip (to share)")]
        public static void BuildLinuxZip() => Build(BuildTarget.StandaloneLinux64, true);

        static void Build(BuildTarget target, bool zip)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new System.InvalidOperationException($"Install Unity build support for {target}.");

            bool linux = target == BuildTarget.StandaloneLinux64;
            string platform = linux ? "Linux" : "Windows";
            string outDir = $"Builds/{platform}";
            string executable = linux ? "StackIsland.x86_64" : "StackIsland.exe";
            // Recreate the scene and settings on a fresh checkout, without requiring a separate setup pass.
            ProjectSetup.RunBatch();
            if (linux)
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore });
            }
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.MainScene },
                locationPathName = Path.Combine(outDir, executable),
                target = target,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"[Build] Failed: {report.summary.result}");

            foreach (var d in Directory.GetDirectories(outDir, "*BurstDebugInformation_DoNotShip")) Directory.Delete(d, true);
            if (zip)
            {
                string archive = $"Builds/StackIsland-{platform}.zip";
                if (File.Exists(archive)) File.Delete(archive);
                ZipFile.CreateFromDirectory(outDir, archive);
            }
            Debug.Log($"[Build] OK: {report.summary.totalSize / (1024 * 1024)} MB -> {outDir}/{executable}");
        }
    }

    /// <summary>Kenney imports: crisp palette textures, no animations/cameras/lights.</summary>
    public class KenneyImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("ThirdParty/Kenney")) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("ThirdParty/Kenney")) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            if (IsAnimatedCharacter(assetPath))
            {
                // the mini characters ship with walk / idle / attack clips: keep them as simple legacy animations
                mi.importAnimation = true;
                mi.animationType = ModelImporterAnimationType.Legacy;
            }
            else
            {
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
            }
        }

        public static bool IsAnimatedCharacter(string path) => path.Contains("mini-characters/FBX/character-");
    }
}
