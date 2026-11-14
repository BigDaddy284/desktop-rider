using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// Some of the rider's clips come from separate models with the same bones (the tricks, the riding style). Their
    /// takes are added to the rider's own clips, and those models are only stores of clips that are never shown.
    /// </summary>
    public static class ClipLibrary
    {
        /// <summary>
        /// Adds every clip of <paramref name="from"/> to <paramref name="to"/>. With <paramref name="replace"/>, a clip
        /// of the same name is replaced (the riding style replaces the model's own resting and riding clips); without
        /// it, existing clips are kept. Returns how many clips were added or replaced.
        /// </summary>
        public static int Copy(UnityEngine.Animation from, UnityEngine.Animation to, bool replace)
        {
            if (from == null || to == null || from == to) return 0;
            int added = 0;
            foreach (AnimationState state in from)
            {
                if (state.clip == null) continue;
                if (to[state.name] != null)
                {
                    if (!replace) continue;
                    to.RemoveClip(state.name);
                }
                to.AddClip(state.clip, state.name);
                to[state.name].wrapMode = state.clip.wrapMode;
                added++;
            }
            from.gameObject.SetActive(false);
            return added;
        }
    }
}
