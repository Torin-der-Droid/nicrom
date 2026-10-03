using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NicromFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
[InitializeOnLoad]
public static class HandGestureValidation
{
    const string Key = "NicromHandGestureValidationPending";
    static HandGestureValidation() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        Validate();
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string message) { if(!value)throw new Exception(message); }
    static void Validate()
    {
        var lines = new List<string>();
        try
        {
            Directory.CreateDirectory("Logs/HandGesture");
            Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var pad=InputSystem.AddDevice<Gamepad>();
            using(var action=new InputAction("Thrust",InputActionType.Button,"<Gamepad>/leftShoulder"))
            {
                action.Enable();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));InputSystem.Update();
                Require(action.WasPressedThisFrame(),"L1 triggers action");
                InputSystem.Update();Require(!action.WasPressedThisFrame(),"Holding L1 does not retrigger");
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
            }
            InputSystem.RemoveDevice(pad);
            lines.Add("PASS: Input System L1 press, hold and release.");
            GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
            var camera=new GameObject("Gesture camera").AddComponent<Camera>();
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=70;
            camera.backgroundColor=new Color(.10f,.13f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var sun=new GameObject("Key light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;
            sun.transform.rotation=Quaternion.Euler(25,-35,0);
            RenderSettings.ambientLight=Color.gray;
            foreach(string name in new[]{"Assault Rifle_1","Pistol_1"})
            {
                string asset="Assets/HandGestureChecks/"+name+" Animated.fbx";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                var root=Object.Instantiate(prefab);
                foreach(var animator in root.GetComponentsInChildren<Animator>())animator.enabled=false;
                var idle=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>c.name.Contains("Idle")&&!c.name.StartsWith("__preview"));
                idle.SampleAnimation(root,0);
                var bones=root.GetComponentsInChildren<Transform>();
                Transform upper=bones.First(t=>t.name=="upperarm_L.L"), lower=bones.First(t=>t.name=="lowerarm_L.L"), hand=bones.First(t=>t.name=="hand_L.L");
                float length=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
                root.transform.localScale*=.62f/length;
                root.transform.position+=new Vector3(-.24f,-.35f,-.15f)-upper.position;
                var materials=new List<Material>();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var mat=new Material(Shader.Find("Standard"));mat.color=renderer is SkinnedMeshRenderer?new Color(.63f,.48f,.34f):new Color(.18f,.2f,.23f);
                    materials.Add(mat);renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();
                    if(renderer is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen=true;lines.Add(name+" renderer="+skin.name+" mesh readable="+skin.sharedMesh.isReadable+" bones="+skin.bones.Length); }
                }
                Require(LeftHandGesturePose.TryCreate(root.transform,out var pose),"Actual rig and runtime bind pose accessible: "+name);
                var right=bones.First(t=>t.name=="hand_L.R");
                Vector3 rightPosition=right.position;Quaternion rightRotation=right.rotation;
                var rotations=bones.Select(t=>t.localRotation).ToArray();
                Save(camera,name+"-grip");
                pose.Apply(camera.transform,1);
                Require(Vector3.Dot(pose.Palm.forward,camera.transform.forward)>.999f,"Palm faces forward");
                Require(Vector3.Dot(pose.Palm.up,camera.transform.up)>.99f,"Open fingers point up");
                Require(Vector3.Distance(right.position,rightPosition)<.0001f && Quaternion.Angle(right.rotation,rightRotation)<.05f,"Right hand unchanged");
                Require(Vector3.Distance(hand.position,new Vector3(-.20f,-.13f,.48f))<.13f,"Left hand reaches thrust target");
                foreach (string finger in new[]{"index","middle","ring","pink"})
                {
                    var first=bones.First(t=>t.name==finger+"_L.L");
                    var end=first.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith("_end"));
                    Require(Vector3.Dot((end.position-first.position).normalized,camera.transform.up)>.8f,"Extended finger "+finger);
                }
                Save(camera,name+"-extended");
                pose.Restore();
                for(int i=0;i<bones.Length;i++)Require(Quaternion.Angle(bones[i].localRotation,rotations[i])<.1f,"Restores animation bone "+bones[i].name);
                for(int frame=0;frame<=72;frame++) { pose.Apply(camera.transform,HandGestureTiming.WeightAt(frame/144f));pose.Restore(); }
                for(int i=0;i<bones.Length;i++)Require(Quaternion.Angle(bones[i].localRotation,rotations[i])<.1f,"No accumulated bone drift");
                lines.Add("PASS "+name+": open bind fingers, forward palm, right hand unchanged, target reach, restore and repeated blend.");
                pose.Dispose();Object.DestroyImmediate(root);
                foreach(var mat in materials)Object.DestroyImmediate(mat);
            }
            File.WriteAllLines("Logs/HandGesture/results.txt",lines);
            Debug.Log("Hand gesture runtime validation passed.");EditorApplication.Exit(0);
        }
        catch(Exception ex)
        {
            lines.Add("FAIL: "+ex);File.WriteAllLines("Logs/HandGesture/results.txt",lines);Debug.LogException(ex);EditorApplication.Exit(1);
        }
    }
    static void Save(Camera camera,string name)
    {
        // Explicitly bake here because several poses are inspected in the same player frame.
        var snapshots=new List<GameObject>();var meshes=new List<Mesh>();var skins=Object.FindObjectsByType<SkinnedMeshRenderer>();
        foreach(var skin in skins)
        {
            if(!skin.enabled)continue;
            var mesh=new Mesh();skin.BakeMesh(mesh, false);meshes.Add(mesh);
            var snapshot=new GameObject("Baked preview");snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
            snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;snapshot.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            snapshots.Add(snapshot);
            File.AppendAllText("Logs/HandGesture/bounds.txt",name+" "+skin.name+" local="+mesh.bounds+" world="+snapshot.GetComponent<Renderer>().bounds+" scale="+skin.transform.lossyScale+"\n");
            skin.enabled=false;
        }
        var rt=new RenderTexture(960,640,24);camera.targetTexture=rt;camera.Render();
        RenderTexture.active=rt;
        var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
        File.WriteAllBytes("Logs/HandGesture/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
        foreach(var snapshot in snapshots)Object.DestroyImmediate(snapshot);
        foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
        foreach(var skin in skins)skin.enabled=true;
    }
}
