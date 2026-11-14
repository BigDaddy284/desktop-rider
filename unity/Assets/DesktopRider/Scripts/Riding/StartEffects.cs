using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// What the owner's start-gate video shows as the bike launches: a spray of dirt thrown back by the rear wheel and a
    /// puff of blue two-stroke smoke. Both play each time the bike sets off from a stop (<see cref="FreeRider.SetOff"/>).
    /// While he rides about, a lighter trail of dirt keeps coming off the rear wheel, more of it the faster he goes and
    /// more again, thrown out to the side, when he leans into a turn (Boss, 14 November: dirt the whole time he rides).
    /// The particle systems are made here in code, so there is nothing to set up in the scene but the material.
    /// </summary>
    public sealed class StartEffects : MonoBehaviour
    {
        public FreeRider rider;

        [Tooltip("A transparent particle material (URP Particles/Unlit). Made by the editor set-up.")]
        public Material particleMaterial;

        [Tooltip("The rear wheel's bone; found by name under the rider if left empty.")]
        public Transform rearWheel;

        [Tooltip("The miniature world's gravity, as a share of Unity's (2.6 m/s² of 9.81).")]
        public float gravity = 0.27f;

        public Color dirtColour = new Color(0.24f, 0.16f, 0.10f, 1f);
        public Color smokeColour = new Color(0.58f, 0.66f, 0.86f, 0.55f);

        [Tooltip("Bits of dirt a second from the rear wheel while riding at full speed in a straight line.")]
        public float trailRate = 22f;
        [Tooltip("How many times more dirt comes off in a fully leaned turn.")]
        public float cornerBoost = 2.2f;

        ParticleSystem dirt, smoke, trail;
        Texture2D softDot;
        Material material;

        void Start()
        {
            if (rider == null) rider = GetComponent<FreeRider>();
            if (rearWheel == null && rider != null && rider.modelAnimation != null) rearWheel = Find(rider.modelAnimation.transform, "rear_wheel");
            if (particleMaterial == null || rider == null) { enabled = false; return; }
            softDot = MakeSoftDot(32);
            material = new Material(particleMaterial);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", softDot);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", softDot);
            dirt = MakeDirt();
            smoke = MakeSmoke();
            trail = MakeTrail();
            rider.SetOff += Launch;
        }

        void OnDestroy()
        {
            if (rider != null) rider.SetOff -= Launch;
            if (material != null) Destroy(material);
            if (softDot != null) Destroy(softDot);
        }

        /// <summary>Throws the dirt and puffs the smoke from the rear wheel, backwards from the way the bike faces.</summary>
        public void Launch()
        {
            if (dirt == null || smoke == null) return;
            var back = -rider.transform.forward;
            var wheel = rearWheel != null ? rearWheel.position : rider.transform.position - rider.transform.forward * 0.55f;
            float scale = rider.transform.lossyScale.y;

            var ground = new Vector3(wheel.x, rider.transform.position.y, wheel.z);
            dirt.transform.SetPositionAndRotation(ground + back * 0.08f * scale + Vector3.up * 0.02f * scale,
                Quaternion.LookRotation(back * 0.8f + Vector3.up * 0.6f));
            dirt.Play(true);

            // The exhaust comes out on the bike's right, above and behind the rear wheel.
            var exhaust = wheel + Vector3.up * 0.28f * scale + back * 0.12f * scale + rider.transform.right * 0.12f * scale;
            smoke.transform.SetPositionAndRotation(exhaust, Quaternion.LookRotation(back + Vector3.up * 0.35f));
            smoke.Play(true);
        }

        /// <summary>Keeps the riding trail at the rear wheel and sets how much comes off, from the bike's speed and lean.</summary>
        void LateUpdate()
        {
            if (trail == null) return;
            float speed01 = rider.isActiveAndEnabled && rider.cruiseSpeed > 0 ? Mathf.Clamp01(rider.Speed / rider.cruiseSpeed) : 0f;
            float lean01 = rider.maxLean > 0 ? Mathf.Clamp01(Mathf.Abs(rider.Lean) / rider.maxLean) : 0f;
            var emission = trail.emission;
            emission.rateOverTime = speed01 < 0.15f ? 0f : trailRate * speed01 * (1f + cornerBoost * lean01);
            if (speed01 < 0.15f) return;

            var back = -rider.transform.forward;
            var wheel = rearWheel != null ? rearWheel.position : rider.transform.position - rider.transform.forward * 0.55f;
            float scale = rider.transform.lossyScale.y;
            var ground = new Vector3(wheel.x, rider.transform.position.y, wheel.z);
            // In a turn the back wheel throws the dirt out to the outside of the turn.
            var outside = -rider.transform.right * Mathf.Sign(rider.Lean) * lean01 * 0.9f;   // + lean is a right-hand turn
            trail.transform.SetPositionAndRotation(ground + back * 0.08f * scale + Vector3.up * 0.02f * scale,
                Quaternion.LookRotation(back * 0.8f + outside + Vector3.up * 0.45f));
        }

        ParticleSystem MakeTrail()
        {
            var ps = NewSystem("RideDirt");
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(dirtColour, dirtColour * 0.7f + new Color(0, 0, 0, 0.3f));
            main.gravityModifier = gravity;
            main.maxParticles = 48;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.025f;
            ps.Play(true);
            return ps;
        }

        ParticleSystem MakeDirt()
        {
            var ps = NewSystem("StartDirt");
            var main = ps.main;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(dirtColour, dirtColour * 0.7f + new Color(0, 0, 0, 0.3f));
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.03f;
            return ps;
        }

        ParticleSystem MakeSmoke()
        {
            var ps = NewSystem("StartSmoke");
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startColor = smokeColour;
            main.gravityModifier = -0.02f;                       // drifts up a little
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.015f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 3.2f));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            return ps;
        }

        ParticleSystem NewSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform.parent, false);     // not a child of the rider: the spray stays where it was thrown
            go.layer = gameObject.layer;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static Texture2D MakeSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a * a * (3f - 2f * a)));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
