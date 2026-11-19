using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DesktopRider.Sidecar.EditorTools
{
    /// <summary>
    /// Import rules for the sidecar set (Assets/DesktopRider/Sidecar): old-style (legacy) clips, one per take, looping as
    /// the manifest says, and the baked kit texture giving the colour on its own. Harvey's models are not touched.
    /// </summary>
    public sealed class SidecarImport : AssetPostprocessor
    {
        public const string Folder = "Assets/DesktopRider/Sidecar/";
        public override uint GetVersion() => 2;

        static bool IsSidecarModel(string path) => path.StartsWith(Folder + "Models/") && path.EndsWith(".fbx");
        static bool IsSidecarProp(string path) => path.StartsWith(Folder + "Props/") && path.EndsWith(".fbx");

        void OnPreprocessModel()
        {
            if (!IsSidecarModel(assetPath) && !IsSidecarProp(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (IsSidecarProp(assetPath)) { importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None; return; }
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.importNormals = ModelImporterNormals.Import;
        }

        void OnPreprocessAnimation()
        {
            if (!IsSidecarModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            var loops = SidecarClips.LoopingClips(Path.ChangeExtension(assetPath, null) + "_manifest.json");
            foreach (var take in takes)
            {
                int bar = take.takeName.LastIndexOf('|');
                take.name = bar >= 0 ? take.takeName.Substring(bar + 1) : take.takeName;
                bool loop = Array.IndexOf(loops, take.name) >= 0;
                take.loopTime = loop; take.loop = loop;
                take.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
            importer.clipAnimations = takes;
        }

        void OnPostprocessMaterial(Material material)
        {
            if (!IsSidecarModel(assetPath) && !IsSidecarProp(assetPath)) return;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", IsSidecarProp(assetPath) ? 0.05f : 0.3f);
            foreach (string texture in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(texture) || material.GetTexture(texture) == null) continue;
                foreach (string colour in new[] { "_BaseColor", "_Color" })
                    if (material.HasProperty(colour)) material.SetColor(colour, Color.white);
                return;
            }
        }
    }

    static class SidecarClips
    {
        [Serializable] class Clip { public string name; public bool loop; }
        [Serializable] class Manifest { public Clip[] clips; }

        public static string[] LoopingClips(string manifestPath)
        {
            if (!File.Exists(manifestPath)) return Array.Empty<string>();
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            return m?.clips == null ? Array.Empty<string>() : Array.ConvertAll(Array.FindAll(m.clips, c => c.loop), c => c.name);
        }
    }
}
