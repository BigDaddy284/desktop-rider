using UnityEditor;
using UnityEngine;

namespace DesktopRider.EditorTools
{
    /// <summary>Import rules for the rough proof model: old-style animation clips, cut at the ranges recorded in the Blender file.</summary>
    public sealed class ProofModelImport : AssetPostprocessor
    {
        // name, first frame, last frame, loops: from the DR_animations.json text block in desktop_rider_animated.blend (24 frames a second)
        static readonly (string name, int first, int last, bool loop)[] Clips =
        {
            ("Idle", 1, 49, true), ("IdleVariation", 54, 150, false), ("LookLeft", 155, 187, false), ("LookRight", 192, 224, false),
            ("Revving", 229, 253, true), ("Thinking", 258, 330, false), ("Talking", 335, 359, true), ("Wheelie", 364, 424, false),
            ("Celebration", 429, 477, false), ("SleepStart", 482, 506, false), ("Sleeping", 511, 607, true), ("SleepEnd", 612, 636, false),
            ("RideOff", 641, 671, false), ("RidingLoop", 676, 700, true), ("RideOn", 705, 735, false),
        };

        void OnPreprocessAnimation()
        {
            if (!assetPath.Contains("/Models/Proof/")) return;
            var importer = (ModelImporter)assetImporter;
            var takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            var result = new ModelImporterClipAnimation[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                result[i] = new ModelImporterClipAnimation
                {
                    name = Clips[i].name, takeName = takes[0].takeName,
                    firstFrame = Clips[i].first, lastFrame = Clips[i].last,
                    loopTime = Clips[i].loop, loop = Clips[i].loop,
                    wrapMode = Clips[i].loop ? WrapMode.Loop : WrapMode.ClampForever,
                };
            }
            importer.clipAnimations = result;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Models/Proof/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
