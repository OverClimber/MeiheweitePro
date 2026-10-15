/*
	This script is placed in public domain. The author takes no responsibility for any possible harm.
	Contributed by Jonathan Czeck
*/
using UnityEngine;
using System.Collections;

public class LightningBolt : MonoBehaviour
{
	public Transform target;
	public int zigs = 100;
	public float speed = 1f;
	public float scale = 1f;
	public Light startLight;
	public Light endLight;

	Perlin noise;
	float oneOverZigs;

	private ParticleSystem.Particle[] particles;
	private ParticleSystem ps;
	private Material ownedMaterial;
	private Texture2D ownedTexture;

	private const string LegacyLightningPng = "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";

	void Start()
	{
		int count = Mathf.Clamp(zigs, 2, 1000);
		oneOverZigs = 1f / (count - 1);
		// Unity 5.5 removed the legacy ParticleEmitter API; this now uses ParticleSystem.
		ps = GetComponent<ParticleSystem>();
		if (ps == null)
		{
			// Older shipped prefabs contain removed EllipsoidParticleEmitter /
			// ParticleRenderer components. Repair them without changing serialized fields.
			ps = gameObject.AddComponent<ParticleSystem>();
			var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive")
				?? Shader.Find("Sprites/Default");
			if (shader == null) throw new System.InvalidOperationException("Equipment link particle shader missing");
			ownedMaterial = new Material(shader);
			if (ownedMaterial.HasProperty("_TintColor")) ownedMaterial.SetColor("_TintColor", new Color(.45241475f,.7278706f,1f,.31f));
			// Exact PNG conversion of Artwork/Lightning.psd. Embed the existing
			// texture because older players lost its removed ParticleRenderer reference.
			ownedTexture = new Texture2D(2,2,TextureFormat.RGB24,false);
			if (!ownedTexture.LoadImage(System.Convert.FromBase64String(LegacyLightningPng), true))
				throw new System.InvalidOperationException("Equipment link texture could not be decoded");
			ownedTexture.wrapMode = TextureWrapMode.Repeat;
			ownedTexture.filterMode = FilterMode.Bilinear;
			ownedMaterial.mainTexture = ownedTexture;
			var renderer = ps.GetComponent<ParticleSystemRenderer>();
			renderer.sharedMaterial = ownedMaterial;
			renderer.maxParticleSize = .25f; // Legacy prefab m_MaxParticleSize.
		}
		ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		var main = ps.main;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = count;
		main.startSpeed = 0f;
		main.playOnAwake = false;
		var emission = ps.emission; emission.enabled = false;
		ps.Play();
		particles = new ParticleSystem.Particle[count];
		for (int i = 0; i < count; i++)
		{
			particles[i].startSize = Mathf.Lerp(.75f, 1.7f, (float)i/(count-1));
			particles[i].startLifetime = 1f;
		}
	}

	void OnDestroy()
	{
		if (ownedMaterial != null) Destroy(ownedMaterial);
		if (ownedTexture != null) Destroy(ownedTexture);
	}

	void LateUpdate ()
	{
		if (ps == null || target == null || particles == null)
			return;

		if (noise == null)
			noise = new Perlin();

		float timex = Time.time * speed * 0.1365143f;
		float timey = Time.time * speed * 1.21688f;
		float timez = Time.time * speed * 2.5564f;

		for (int i=0; i < particles.Length; i++)
		{
			Vector3 position = Vector3.Lerp(transform.position, target.position, oneOverZigs * (float)i);
			Vector3 offset = new Vector3(noise.Noise(timex + position.x, timex + position.y, timex + position.z),
										noise.Noise(timey + position.x, timey + position.y, timey + position.z),
										noise.Noise(timez + position.x, timez + position.y, timez + position.z));
			position += (offset * scale * ((float)i * oneOverZigs));

			particles[i].position = position;
			particles[i].startColor = Color.white;
			particles[i].remainingLifetime = 1f;
		}

		ps.SetParticles(particles, particles.Length);

		if (particles.Length >= 2)
		{
			if (startLight)
				startLight.transform.position = particles[0].position;
			if (endLight)
				endLight.transform.position = particles[particles.Length - 1].position;
		}
	}
}
