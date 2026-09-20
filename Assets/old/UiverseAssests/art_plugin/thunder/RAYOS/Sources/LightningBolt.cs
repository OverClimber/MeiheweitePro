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

	void Start()
	{
		oneOverZigs = 1f / (float)zigs;
		// Unity 5.5 removed the legacy ParticleEmitter API; this now uses ParticleSystem.
		ps = GetComponent<ParticleSystem>();
		if (ps != null)
		{
			ParticleSystem.EmissionModule emission = ps.emission;
			emission.enabled = false;
			ps.Emit(zigs);
		}
		particles = new ParticleSystem.Particle[Mathf.Max(1, zigs)];
		if (ps != null)
			ps.GetParticles(particles);
	}

	void Update ()
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
