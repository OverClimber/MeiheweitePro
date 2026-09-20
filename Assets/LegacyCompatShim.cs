// ---------------------------------------------------------------------------
// LegacyCompatShim.cs
//
// Purpose: let assets inherited from the Unity 5.6 project keep compiling on
// Unity 2021 after Unity removed two families of engine API:
//
//   * the legacy on-screen text / texture components  (GUIText, GUITexture)
//     -> removed in Unity 5.5, still present in 2021 as [Obsolete(error: true)]
//   * the legacy particle system (ParticleEmitter, ParticleAnimator,
//     EllipsoidParticleEmitter, Particle) -> removed in Unity 5.5
//
// Types declared in the *global* namespace take precedence over types imported
// with `using UnityEngine;`, so unqualified references inside the game's scripts
// resolve to the stand-ins below instead of to UnityEngine's obsolete/removed
// versions.
//
// These stand-ins are inert: no component of these types can exist at runtime in
// a Unity 2021 project, so every code path guarded by `GetComponent<ParticleEmitter>()`
// or `GetComponent(typeof(GUITexture))` evaluates to false and becomes a no-op --
// which is the behaviour you get on Unity 2021 regardless.
// ---------------------------------------------------------------------------
using UnityEngine;

public class GUITexture : MonoBehaviour
{
    public Color color = Color.white;
    public Texture texture;
    public Rect pixelInset;
}

public class GUIText : MonoBehaviour
{
    public string text = "";
    public Material material;
    public Color color = Color.white;
    public int fontSize = 0;
}

public class ParticleAnimator : MonoBehaviour
{
    public Vector3 force;
    public Vector3 rndForce;
    public bool autodestruct;
    public bool doesAnimateColor;
    public Color[] colorAnimation;
}

public class ParticleEmitter : MonoBehaviour
{
    public bool emit;
    public bool enabled_ = true;
    public float minSize;
    public float maxSize;
    public float minEmission;
    public float maxEmission;
    public float minEnergy = 1f;
    public float maxEnergy = 1f;
    public Vector3 worldVelocity;
    public Vector3 localVelocity;
    public Vector3 rndVelocity;
    public Vector3 tangentVelocity;
    public bool useWorldSpace;
    public Particle[] particles = new Particle[0];

    public int particleCount { get { return particles.Length; } }

    public void Emit() { }
    public void Emit(int count) { }
    public void Emit(Vector3 pos, Vector3 vel, float size, float energy, Color color) { }
    public void ClearParticles() { }
}

public class EllipsoidParticleEmitter : ParticleEmitter
{
}

public class Particle
{
    public Vector3 position;
    public Vector3 velocity;
    public float size = 1f;
    public float energy = 1f;
    public Color color = Color.white;
    public float rotation;
    public float angularVelocity;
}
