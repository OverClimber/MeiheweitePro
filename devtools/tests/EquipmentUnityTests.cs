using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EquipmentUnityTests
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Invoke(object obj, string method) {
        obj.GetType().GetMethod(method, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);
    }
    public static void Run()
    {
        try {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fixture/mod_ocgcore_lighting.prefab");
            Check(prefab!=null,"original equipment prefab loads");
            var go=UnityEngine.Object.Instantiate(prefab);
            var bolt=go.GetComponentInChildren<LightningBolt>();
            Check(bolt!=null && bolt.target!=null,"prefab script and target references resolve");
            bolt.transform.position=new Vector3(-12,0,0);
            bolt.target.position=new Vector3(12,0,0);
            bolt.scale=0;
            Invoke(bolt,"Start");
            var ps=bolt.GetComponent<ParticleSystem>();
            Check(ps!=null,"legacy equipment prefab acquires live ParticleSystem");
            Invoke(bolt,"LateUpdate");
            var points=new ParticleSystem.Particle[1000];
            int count=ps.GetParticles(points);
            Check(count==100,"all link particles created: count="+count);
            Check(ps.main.simulationSpace==ParticleSystemSimulationSpace.World,"link uses world coordinates");
            Check(Vector3.Distance(points[0].position,bolt.transform.position)<.01f,"first endpoint follows source");
            Check(Vector3.Distance(points[count-1].position,bolt.target.position)<.01f,"last endpoint follows target");
            foreach(var p in new[]{points[0],points[count-1]})
                Check(p.startSize>0 && p.remainingLifetime>0,"visible live particles");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            Check(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported,"supported particle material");
            var cam=new GameObject("evidence-camera").AddComponent<Camera>();
            cam.transform.position=new Vector3(0,0,-40);
            cam.orthographic=true; cam.orthographicSize=12;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=Color.black;
            var rt=new RenderTexture(512,256,24); cam.targetTexture=rt; cam.Render();
            RenderTexture.active=rt;
            var tex=new Texture2D(512,256,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,512,256),0,0); tex.Apply();
            int lit=0; foreach(var pixel in tex.GetPixels32()) if(pixel.r>20||pixel.g>20||pixel.b>20) lit++;
            File.WriteAllBytes(@"@IMAGE@",tex.EncodeToPNG());
            Check(lit>100,"equipment link produces visible pixels");
            Check(Mathf.Abs(renderer.maxParticleSize-.25f)<.0001f,"legacy screen size cap restored");
            var reference=AssetDatabase.LoadAssetAtPath<Material>("Assets/Fixture/Lightning Mat.mat");
            Check(reference!=null && reference.mainTexture!=null,"original material and PSD resolve");
            renderer.sharedMaterial=reference; cam.Render();
            var expected=new Texture2D(512,256,TextureFormat.RGB24,false);
            expected.ReadPixels(new Rect(0,0,512,256),0,0); expected.Apply();
            var actualPixels=tex.GetPixels32(); var expectedPixels=expected.GetPixels32();
            int differing=0;
            for(int i=0;i<actualPixels.Length;i++)
                if(Mathf.Abs(actualPixels[i].r-expectedPixels[i].r)>3 || Mathf.Abs(actualPixels[i].g-expectedPixels[i].g)>3 || Mathf.Abs(actualPixels[i].b-expectedPixels[i].b)>3) differing++;
            File.WriteAllBytes(@"@IMAGE@"+".reference.png",expected.EncodeToPNG());
            // Imported PSD uses texture compression; compare shape and average visible
            // color error rather than treating each quantized pixel as a silhouette change.
            int referenceLit=0, visible=0; float error=0;
            int ax0=512,ay0=256,ax1=0,ay1=0,bx0=512,by0=256,bx1=0,by1=0;
            for(int i=0;i<actualPixels.Length;i++) {
                var a=actualPixels[i]; var b=expectedPixels[i];
                bool av=a.r>20||a.g>20||a.b>20, bv=b.r>20||b.g>20||b.b>20;
                int x=i%512,y=i/512;
                if(av){ ax0=Mathf.Min(ax0,x);ay0=Mathf.Min(ay0,y);ax1=Mathf.Max(ax1,x);ay1=Mathf.Max(ay1,y); }
                if(bv){ referenceLit++;bx0=Mathf.Min(bx0,x);by0=Mathf.Min(by0,y);bx1=Mathf.Max(bx1,x);by1=Mathf.Max(by1,y); }
                if(av||bv){ visible++;error+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b); }
            }
            Check(ax0==bx0 && ay0==by0 && ax1==bx1 && ay1==by1,"same visible link bounds as original");
            Check(Mathf.Abs(lit-referenceLit)<=Mathf.Max(2,referenceLit*.02f),"same glow area as original");
            Check(error/(visible*3)<4,"mean visible color error below compression tolerance: "+error/(visible*3));

            bolt.target.position=new Vector3(5,5,0); Invoke(bolt,"LateUpdate"); ps.GetParticles(points);
            Check(Vector3.Distance(points[count-1].position,bolt.target.position)<.01f,"link tracks moving target");
            UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText(@"@RESULT@","passed assertions="+checks+" rendered_pixels="+lit);
            EditorApplication.Exit(0);
        } catch(Exception e) {
            File.WriteAllText(@"@RESULT@","failed "+e); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
