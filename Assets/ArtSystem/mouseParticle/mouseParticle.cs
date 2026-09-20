using UnityEngine;
using System.Collections;

public class mouseParticle : MonoBehaviour {
    public Camera camera;
    public ParticleSystem e1;
    public ParticleSystem e2;
    public Transform trans;
    // Use this for initialization
    void Start () {
        camera.depth = 99999;
    }
    float time = 0;
	// Update is called once per frame
	void Update () {
        Vector3 screenPoint = Input.mousePosition;
        screenPoint.z = 10;
        trans.position = camera.ScreenToWorldPoint(screenPoint);

        if (Input.GetMouseButton(0))    
        {
            if (Input.GetMouseButtonDown(0))
            {
                time = 0;
            }
            time += Time.deltaTime;
            if (time > 0.49)
            {
                time = 0.49f;
            }
            // Unity 5.5 removed the legacy EllipsoidParticleEmitter API
            // (maxEmission / minEmission / emit). The modern equivalent is
            // rateOverTime on the particle system's emission module.
            float rate = (0.5f - time) * 60f;
            SetEmission(e1, rate);
            SetEmission(e2, rate / 3f);
            if (e1 != null) e1.Play();
            if (e2 != null) e2.Play();
        }
        else
        {
            if (e1 != null) e1.Stop();
            if (e2 != null) e2.Stop();
        }
    }

    static void SetEmission(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }
}
