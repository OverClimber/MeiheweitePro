using UnityEngine;
using System.Collections;
using System;

public class fusion_tweener : MonoBehaviour {
    ParticleSystem[] systems;
	// Use this for initialization
	void Start () {
        systems = GetComponentsInChildren<ParticleSystem>();
        start_time = Program.TimePassed();
        ScaleSystems(scaleFactor);
        // NOTE: Unity 5.5 removed the legacy ParticleEmitter / ParticleAnimator APIs that
        // this script used to scale; only the ParticleSystem scaling is kept.
	}
    int step = 1;
    float scaleFactor = 0.1f;
    int start_time = 0;
	// Update is called once per frame
    void Update()
    {
        if (Program.TimePassed() - start_time > 0)
        {
            step = 1;
        }
        if (Program.TimePassed() - start_time > 1500)
        {
            step = 2;
        }
        if (Program.TimePassed() - start_time > 3000)
        {
            step = 3;
        }
        if (Program.TimePassed() - start_time > 3500)
        {
            step = 4;
        }

        if (step == 1)
        {
            scaleFactor = 1 + Time.deltaTime*1.5f;
        }
        if (step == 2)
        {
            scaleFactor = 1f;
        }
        if (step == 3)
        {
            scaleFactor = 0.2f;
        }
        if (step == 4)
        {
            Destroy(gameObject);
            return;
        }
        ScaleSystems(scaleFactor);
    }

    void ScaleSystems(float factor)
    {
        if (systems == null) return;
        foreach (ParticleSystem system in systems)
        {
            if (system == null) continue;
            ParticleSystem.MainModule main = system.main;
            main.startSpeedMultiplier *= factor;
            main.startSizeMultiplier *= factor;
            main.gravityModifierMultiplier *= factor;
        }
    }
}
