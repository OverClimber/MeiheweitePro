"""Run pixel-for-pixel field slicing comparison in real Unity, in an empty scene."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "_takeover_audit/issues_20261011"
PROJECT = OUT / "texture-validation"
RUNTIME = ROOT / "_takeover_audit/implementation_20261011/runtime"

def main():
    (PROJECT / "Assets/Editor").mkdir(parents=True, exist_ok=True)
    (PROJECT / "Packages").mkdir(exist_ok=True)
    (PROJECT / "Packages/manifest.json").write_text('{"dependencies":{}}')
    old = (OUT / "baseline/UIHelper.cs").read_text(encoding="utf-8-sig")
    reference = old[old.index("    internal static Texture2D[] sliceField"):old.index("    static Texture2D ScaleTexture")]
    reference = reference.replace("internal static Texture2D[] sliceField", "static Texture2D[] ReferenceSlice")
    reference = reference.replace("return returnValue;", "UnityEngine.Object.Destroy(textureField); return returnValue;")
    managed = RUNTIME / "MeiheweitePro_Data/Managed"
    dll = ROOT / "_takeover_audit/implementation_20261011/Assembly-CSharp.dll"
    source = r'''
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class FieldTextureValidation {
    static Type helper;
    static FieldTextureValidation() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("maintenance-field", true);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state) {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("maintenance-field", false)) return;
        SessionState.SetBool("maintenance-field", false);
        try {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string path = Path.Combine(@"MANAGED", new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            helper = Assembly.LoadFrom(@"DLL").GetType("UIHelper", true);
            foreach (string name in new[] { "field.png", "newfield.png", "newfield_rd.png" }) {
                var input = new Texture2D(2,2);
                if (!input.LoadImage(File.ReadAllBytes(Path.Combine(@"RUNTIME", "texture/duel", name)))) throw new Exception("image decode failed");
                var expected = ReferenceSlice(input);
                var actual = (Texture2D[])helper.GetMethod("sliceField", BindingFlags.NonPublic|BindingFlags.Static).Invoke(null, new object[] { input });
                for (int slice=0;slice<3;slice++) {
                    var a=actual[slice].GetPixels32(); var b=expected[slice].GetPixels32();
                    if (a.Length != b.Length) throw new Exception("size mismatch");
                    for (int i=0;i<a.Length;i++) if (!a[i].Equals(b[i])) throw new Exception(name+" slice="+slice+" pixel="+i);
                    UnityEngine.Object.Destroy(actual[slice]); UnityEngine.Object.Destroy(expected[slice]);
                }
                UnityEngine.Object.Destroy(input);
                Debug.Log("FIELD_PIXEL_PASS " + name + " 3x1024x819 RGBA equality");
            }
            File.WriteAllText(@"RESULT", "passed");
            EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Texture2D ScaleTexture(Texture2D input,int width,int height) {
        return (Texture2D)helper.GetMethod("ScaleTexture", BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{input,width,height});
    }
REFERENCE
}
'''
    result = OUT / "field-pixel-status.txt"
    result.unlink(missing_ok=True)
    for key, value in {"MANAGED": str(managed), "DLL": str(dll), "RUNTIME": str(RUNTIME), "RESULT": str(result), "REFERENCE": reference}.items():
        source = source.replace(key, value)
    (PROJECT / "Assets/Editor/FieldTextureValidation.cs").write_text(source, encoding="utf-8")
    log = OUT / "field-pixel-unity.log"
    process = subprocess.Popen(["D:/Unity/2021.3.45f1/Editor/Unity.exe", "-batchmode", "-nographics", "-projectPath", str(PROJECT),
        "-logFile", str(log), "-executeMethod", "FieldTextureValidation.Run"], stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    print("Field pixel validation Unity PID=" + str(process.pid), flush=True)
    code = process.wait()
    passed = code == 0 and result.is_file() and result.read_text() == "passed"
    (OUT / "field-pixel-result.json").write_text(json.dumps({"status": "passed" if passed else "failed", "exit_code": code,
        "dll_sha256": hashlib.sha256(dll.read_bytes()).hexdigest(), "coverage": "three real OCG/RD field images, every RGBA pixel in all three slices"}, indent=2))
    return 0 if passed else 1

if __name__ == "__main__": raise SystemExit(main())
