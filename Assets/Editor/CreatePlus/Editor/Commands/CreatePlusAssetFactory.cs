using System.IO;
using System.Text;
using CreatePlus.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreatePlus.Commands {
    /// <summary>
    /// Concrete asset creation routines used by the built-in commands. Each routine creates an asset
    /// in the context's target folder, gives it a unique name, then selects and pings it. Kept
    /// separate from command definitions so the same helpers can back future commands.
    /// </summary>
    public static class CreatePlusAssetFactory {
        /// <summary>Resolves a safe project-relative target folder from the context.</summary>
        static string TargetFolder(CreatePlusContext context) {
            string folder = context != null ? context.TargetFolderAssetPath : null;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) {
                return "Assets";
            }

            return folder;
        }

        // ---- Folder ----------------------------------------------------------------------------

        public static void CreateFolder(CreatePlusContext context) {
            string parent = TargetFolder(context);
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(parent + "/New Folder");
            string newName = Path.GetFileName(uniquePath);
            string guid = AssetDatabase.CreateFolder(parent, newName);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SelectAndPing(path);
        }

        // ---- C# Script -------------------------------------------------------------------------

        public static void CreateCSharpScript(CreatePlusContext context) {
            string folder = TargetFolder(context);
            // Use an identifier-safe unique name so the class name always matches the file name
            // (Unity's default " 1" suffix would introduce a space and break that requirement).
            string path = GenerateUniqueIdentifierPath(folder, "NewBehaviourScript", ".cs");
            string className = Path.GetFileNameWithoutExtension(path);
            string contents = BuildScriptTemplate(className);
            WriteTextFile(path, contents);
            SelectAndPing(path);
        }

        // ---- Material --------------------------------------------------------------------------

        public static void CreateMaterial(CreatePlusContext context) {
            string folder = TargetFolder(context);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Material.mat");
            var material = new Material(DefaultMaterialShader());
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            SelectAndPing(path);
        }

        static Shader DefaultMaterialShader() {
            // Pick a sensible default that exists in this project's render pipeline.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) {
                shader = Shader.Find("Standard");
            }

            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            return shader;
        }

        // ---- Scene -----------------------------------------------------------------------------

        public static void CreateScene(CreatePlusContext context) {
            string folder = TargetFolder(context);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Scene.unity");

            // Create the scene additively and immediately close it so the user's open scene(s) are
            // not disturbed. SaveScene writes the asset to disk.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            SelectAndPing(path);
        }

        // ---- Text ------------------------------------------------------------------------------

        public static void CreateTextFile(CreatePlusContext context) {
            string folder = TargetFolder(context);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Text.txt");
            WriteTextFile(path, string.Empty);
            SelectAndPing(path);
        }

        // ---- Assembly Definition ---------------------------------------------------------------

        public static void CreateAssemblyDefinition(CreatePlusContext context) {
            string folder = TargetFolder(context);
            string path = GenerateUniqueIdentifierPath(folder, "NewAssembly", ".asmdef");
            string asmName = Path.GetFileNameWithoutExtension(path);
            string contents = "{\n    \"name\": \"" + asmName + "\"\n}\n";
            WriteTextFile(path, contents);
            SelectAndPing(path);
        }

        public static void CreateAssemblyDefinitionReference(CreatePlusContext context) {
            string folder = TargetFolder(context);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/NewAssemblyReference.asmref");
            const string contents = "{\n    \"reference\": \"\"\n}\n";
            WriteTextFile(path, contents);
            SelectAndPing(path);
        }

        // ---- Helpers ---------------------------------------------------------------------------

        /// <summary>
        /// Returns a unique asset path whose file name stays a valid identifier on collision (appends
        /// "1", "2", ... instead of " 1"), so generated class/assembly names match their file names.
        /// </summary>
        static string GenerateUniqueIdentifierPath(string folder, string baseName, string extension) {
            string safeBase = ToValidIdentifier(baseName);
            string candidate = folder + "/" + safeBase + extension;
            int suffix = 1;
            while (File.Exists(candidate)) {
                candidate = folder + "/" + safeBase + suffix + extension;
                suffix++;
            }

            return candidate;
        }

        static void WriteTextFile(string assetPath, string contents) {
            // Unity's working directory is the project root, so the project-relative path resolves
            // correctly for File IO. ImportAsset brings the new file into the database.
            File.WriteAllText(assetPath, contents, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath);
        }

        static void SelectAndPing(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) {
                return;
            }

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>Converts an arbitrary name to a valid C# identifier (used for class/assembly names).</summary>
        static string ToValidIdentifier(string raw) {
            if (string.IsNullOrEmpty(raw)) {
                return "NewScript";
            }

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw) {
                if (char.IsLetterOrDigit(c) || c == '_') {
                    sb.Append(c);
                }
            }

            if (sb.Length == 0 || char.IsDigit(sb[0])) {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }

        static string BuildScriptTemplate(string className) {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("public class " + className + " : MonoBehaviour {");
            sb.AppendLine("    void Start() {");
            sb.AppendLine();
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    void Update() {");
            sb.AppendLine();
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
