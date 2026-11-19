using System;
using System.Collections.Generic;
using System.Linq;
using DesktopRider.Core.Configuration;
using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// The characters that can ride about (the "Who rides" setting): Harvey on his bike, and the sidecar crew (Sidecar 41,
    /// approved in the Unity sidecar viewer on 17 November 2026). Both models sit under the rider's lean pivot; only the
    /// chosen one is shown. Changing over swaps the model the rider animates, its tricks and props, how it rides (a
    /// sidecar does not lean like a solo bike) and how big it is drawn, while the rider keeps his place on the desktop.
    /// </summary>
    public sealed class RiderCharacters : MonoBehaviour
    {
        [Serializable]
        public sealed class Character
        {
            public RiderCharacter id;
            public GameObject model;
            [Tooltip("Tricks: props_layout.json for this character, and its ramp and landing models.")]
            public TextAsset trickLayout;
            public GameObject[] props = Array.Empty<GameObject>();
            public float propYaw = 90f;
            [Tooltip("Tricks squeezed to this much of their length along the way they travel, ramps and landings too (1 = as made).")]
            public float trickLength = 1f;
            [Tooltip("True: the rules use the model's own clips (the sidecar). False: Harvey's clip list.")]
            public bool ownClips;
            [Header("Riding")]
            public float cruiseSpeed = 2.6f;
            public float acceleration = 2.5f;
            public float braking = 4f;
            public float maxTurnRate = 110f;
            public float maxLean = 38f;
            [Tooltip("Drawn this much of the normal size (the sidecar is drawn so its crew are the size of Harvey and Louie).")]
            public float drawScale = 1f;
            [Tooltip("Blue exhaust smoke when setting off (Harvey's two-stroke). No reference photo for the sidecar's exhaust, so off for it.")]
            public bool exhaustSmoke = true;
            [Header("Click area and shadow (ground footprint, metres)")]
            public Vector3 colliderCentre = new Vector3(0f, 0.65f, 0f);
            public Vector3 colliderSize = new Vector3(0.6f, 1.4f, 1.9f);
            public Vector3 shadowCentre = new Vector3(0f, 0.01f, 0f);
            public Vector2 shadowSize = new Vector2(1.3f, 2.5f);
        }

        public FreeRider rider;
        public TrickRunner tricks;
        public StartEffects effects;
        public BoxCollider clickArea;
        public Transform shadow;
        public Character[] characters = Array.Empty<Character>();

        /// <summary>The character riding now.</summary>
        public RiderCharacter Current { get; private set; } = RiderCharacter.Harvey;

        bool applied;

        /// <summary>Shows only the first character until the settings say otherwise.</summary>
        void Awake()
        {
            foreach (var c in characters)
                if (c.model != null) c.model.SetActive(c == characters[0]);
        }

        /// <summary>Makes <paramref name="id"/> the one riding. Does nothing if it already is (or it is not in the scene).</summary>
        public void Use(RiderCharacter id)
        {
            if (applied && id == Current) return;
            var chosen = characters.FirstOrDefault(c => c.id == id && c.model != null);
            if (chosen == null || rider == null)
            {
                if (!applied) Debug.LogWarning("Desktop Rider has no " + id + " in this build; Harvey rides instead.");
                return;
            }
            if (tricks != null) tricks.Stop();
            foreach (var c in characters)
                if (c.model != null) c.model.SetActive(c == chosen);

            var animation = chosen.model.GetComponent<UnityEngine.Animation>();
            rider.cruiseSpeed = chosen.cruiseSpeed;
            rider.acceleration = chosen.acceleration;
            rider.braking = chosen.braking;
            rider.maxTurnRate = chosen.maxTurnRate;
            rider.maxLean = chosen.maxLean;
            rider.UseModel(animation, chosen.ownClips ? ClipsOf(animation) : null, chosen.drawScale);
            if (tricks != null) tricks.Use(chosen.trickLayout, chosen.props, chosen.propYaw, chosen.trickLength);
            if (effects != null) effects.UseModel(animation, chosen.exhaustSmoke);
            if (clickArea != null) { clickArea.center = chosen.colliderCentre; clickArea.size = chosen.colliderSize; }
            if (shadow != null)
            {
                shadow.localPosition = chosen.shadowCentre;
                shadow.localScale = new Vector3(chosen.shadowSize.x, chosen.shadowSize.y, 1f);
            }
            Current = id;
            applied = true;
            Debug.Log("Riding now: " + id + ".");
        }

        /// <summary>
        /// The model's everyday and riding clips for the rules (name, frames at 24 a second, looping). Tricks are left out:
        /// they are ridden by the trick runner, not picked by the rules.
        /// </summary>
        public static IEnumerable<(string Name, int Frames, bool Loop)> ClipsOf(UnityEngine.Animation animation, ICollection<string> tricks = null)
        {
            var list = new List<(string, int, bool)>();
            if (animation == null) return list;
            foreach (AnimationState state in animation)
            {
                if (state.clip == null || (tricks != null && tricks.Contains(state.name))) continue;
                int frames = Mathf.Max(1, Mathf.RoundToInt(state.clip.length * 24f));
                bool loop = state.clip.wrapMode == WrapMode.Loop || state.wrapMode == WrapMode.Loop || state.clip.isLooping;
                list.Add((state.name, frames, loop));
            }
            return list;
        }
    }
}
