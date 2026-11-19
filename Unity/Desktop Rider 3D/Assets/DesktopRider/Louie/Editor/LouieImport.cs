using UnityEditor;
using UnityEngine;

namespace LouieTools
{
    /// <summary>
    /// Import rules for Louie (Assets/DesktopRider/Louie/Models): old-style (legacy) clips, one per take, the same looping
    /// as Harvey's, and the kit texture giving the colour on its own. Harvey's and the sidecar's models are not touched.
    /// </summary>
    public sealed class LouieImport : AssetPostprocessor
    {
        public const string Folder = "Assets/DesktopRider/Louie/";
        public override uint GetVersion() => 1;

        static readonly string[] Loops = { "Idle", "Revving", "Talking", "Sleeping", "RidingLoop", "CornerLeft", "CornerRight" };

        public static bool IsLouieModel(string path) => path.StartsWith(Folder + "Models/") && path.EndsWith(".fbx");

        public static string ClipName(string takeName)
        {
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }

        public static bool IsLooping(string clip) => System.Array.IndexOf(Loops, clip) >= 0;

        void OnPreprocessModel()
        {
            if (!IsLouieModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        void OnPreprocessAnimation()
        {
            if (!IsLouieModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            foreach (var take in takes)
            {
                take.name = ClipName(take.takeName);
                bool loop = IsLooping(take.name);
                take.loopTime = loop; take.loop = loop;
                take.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
            importer.clipAnimations = takes;
        }

        // Blender also writes each material's flat colour; where the kit texture is present it alone gives the colour.
        void OnPostprocessMaterial(Material material)
        {
            if (!IsLouieModel(assetPath)) return;
            foreach (string texture in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(texture) || material.GetTexture(texture) == null) continue;
                foreach (string colour in new[] { "_BaseColor", "_Color" })
                    if (material.HasProperty(colour)) material.SetColor(colour, Color.white);
                return;
            }
        }
    }
}
