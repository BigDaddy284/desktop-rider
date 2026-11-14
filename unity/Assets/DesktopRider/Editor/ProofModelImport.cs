using UnityEditor;
using UnityEngine;

namespace DesktopRider.EditorTools
{
    /// <summary>
    /// Import rules for the rider models: old-style (legacy) animation clips.
    /// The rough proof model has one long take, cut at the ranges recorded in the Blender file.
    /// The textured DesktopRider.fbx already has one take per clip (bike and rider rigs keyed together), so only the looping is set.
    /// </summary>
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

        /// <summary>Raise this when the rules change so Unity imports the models again.</summary>
        public override uint GetVersion() => 4;

        void OnPreprocessAnimation()
        {
            if (!IsRiderModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            if (!assetPath.Contains("/Models/Proof/"))
            {
                foreach (var take in takes)
                {
                    take.name = ClipName(take.takeName);
                    bool loop = IsLooping(take.name);
                    take.loopTime = loop; take.loop = loop;
                    take.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
                }
                importer.clipAnimations = takes;
                return;
            }
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

        /// <summary>The proof model and the textured rider model; props and anything else keep Unity's defaults.</summary>
        public static bool IsRiderModel(string path) =>
            path.Contains("/Models/Proof/") || path.EndsWith("/Models/DesktopRider.fbx") || path.EndsWith("/Models/DesktopRider_Tricks.fbx");

        /// <summary>The ramps, landings and whoops built by Tools/Blender/build_unity_props.py.</summary>
        public static bool IsPropModel(string path) => path.Contains("/Models/Props/") && path.EndsWith(".fbx");

        /// <summary>Blender can prefix a take with its armature ("rider_rig|Idle"); the clip is called by the part after the bar.</summary>
        public static string ClipName(string takeName)
        {
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }

        public static bool IsLooping(string clip)
        {
            foreach (var c in Clips) if (c.name == clip) return c.loop;
            return false;   // tricks and anything new play once
        }

        /// <summary>
        /// Blender writes each material's old flat colour into the FBX as well as the kit texture, and Unity multiplies
        /// the two, which turns the light blue and pink kit dark red and blue. Where a texture is present it alone gives the colour.
        /// </summary>
        void OnPostprocessMaterial(Material material)
        {
            bool prop = IsPropModel(assetPath);
            if (!IsRiderModel(assetPath) && !prop) return;
            // Dirt is matt; Unity's default smoothness makes it look like plastic.
            if (prop && material.name.StartsWith("DR_Dirt") && material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            foreach (string texture in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(texture) || material.GetTexture(texture) == null) continue;
                foreach (string colour in new[] { "_BaseColor", "_Color" })
                    if (material.HasProperty(colour)) material.SetColor(colour, Color.white);
                return;
            }
        }

        void OnPreprocessModel()
        {
            if (!IsRiderModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
