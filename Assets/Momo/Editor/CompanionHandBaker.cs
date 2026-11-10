using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Momo.Editor
{
    public static class CompanionHandBaker
    {
        const string Folder="Assets/Momo/Resources/CompanionHand/";
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Momo/Models/CompanionHand/HandSource.fbx");
            if(source==null)throw new Exception("Full companion arm source missing");
            Bake(source,false);Bake(source,true);AssetDatabase.SaveAssets();
        }
        static void Bake(GameObject asset,bool left)
        {
            var source=UnityEngine.Object.Instantiate(asset);source.transform.position=Vector3.zero;
            var skin=source.GetComponentInChildren<SkinnedMeshRenderer>();
            var all=source.GetComponentsInChildren<Transform>();
            Transform Find(string n)=>all.First(t=>t.name==n);
            var rotation=Matrix4x4.Rotate(Quaternion.Euler(180,0,0));
            Vector3 P(Transform t)=>rotation.MultiplyPoint3x4(t.position);
            // Explicit anatomical side: right thumb is medial (-X) with fingers +Z,
            // palm +Y. Reflection is baked into skin/normals/triangle winding, never bones.
            float thumbSide=P(Find("thumb.02.R")).x-P(Find("f_middle.01.R")).x;
            bool reflect=left?thumbSide<0:thumbSide>0;
            var map=Matrix4x4.Scale(new Vector3(reflect?-1:1,1,1))*rotation;
            Vector3 Pos(Transform t)=>map.MultiplyPoint3x4(t.position);
            string name=left?"Left":"Right";
            var root=new GameObject(name+" companion arm");
            var bones=new Dictionary<Transform,Transform>();
            var required=new HashSet<Transform>(skin.bones);
            foreach(var t in all)if(t.name=="ElbowEnd"||t.name=="ShoulderEnd")required.Add(t);
            foreach(var t in required)
            {
                var b=new GameObject(t.name).transform;b.SetParent(root.transform,false);b.position=Pos(t);
                Transform next=null;
                string special=t.name=="forearm.R"?"ElbowEnd":t.name=="upper_arm.R"?"ShoulderEnd":null;
                if(special!=null)next=Find(special);
                else foreach(Transform child in t)if(required.Contains(child)){next=child;break;}
                Vector3 direction=next!=null?Pos(next)-Pos(t):t.parent!=null?Pos(t)-Pos(t.parent):Vector3.forward;
                if(direction.sqrMagnitude<.000001f)direction=Vector3.forward;
                var up=Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.95f?Vector3.forward:Vector3.up;
                b.rotation=Quaternion.LookRotation(direction,up);bones.Add(t,b);
            }
            foreach(var pair in bones)if(pair.Key.parent!=null&&bones.TryGetValue(pair.Key.parent,out var parent))pair.Value.SetParent(parent,true);
            // Rebuild a forward shoulder -> elbow -> wrist chain. The old reverse
            // wrist-rooted chain forced wrist direction to follow an arbitrary elbow.
            var forearm=bones[Find("forearm.R")];var upper=bones[Find("upper_arm.R")];
            var handRoot=bones[Find("HandRoot")];
            Vector3 wristPosition=forearm.position,elbowPosition=bones[Find("ElbowEnd")].position,shoulderPosition=bones[Find("ShoulderEnd")].position;
            var body=new GameObject("ShoulderRoot").transform;body.SetParent(root.transform,false);body.position=shoulderPosition;
            upper.SetParent(body,true);upper.position=shoulderPosition;upper.rotation=Quaternion.LookRotation(elbowPosition-shoulderPosition,Vector3.up);
            forearm.SetParent(upper,true);forearm.position=elbowPosition;forearm.rotation=Quaternion.LookRotation(wristPosition-elbowPosition,Vector3.up);
            Transform Twist(string label,float t,Transform parent)
            {var b=new GameObject(label).transform;b.SetParent(parent,false);b.position=Vector3.Lerp(elbowPosition,wristPosition,t);b.rotation=forearm.rotation;return b;}
            var twistMid=Twist("ForearmTwistMid",.35f,forearm);var twistWrist=Twist("ForearmTwistWrist",.8f,twistMid);
            handRoot.SetParent(twistWrist,true);handRoot.position=wristPosition;
            bones[Find("ElbowEnd")].SetParent(upper,true);bones[Find("ElbowEnd")].position=elbowPosition;
            bones[Find("ShoulderEnd")].SetParent(body,true);bones[Find("ShoulderEnd")].position=shoulderPosition;
            var wristAnchor=new GameObject("WristAnchor").transform;wristAnchor.SetParent(forearm,false);wristAnchor.position=wristPosition;
            var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh);mesh.name=name+" continuous arm";
            var localToCanonical=map*skin.transform.localToWorldMatrix;
            mesh.vertices=mesh.vertices.Select(v=>localToCanonical.MultiplyPoint3x4(v)).ToArray();
            mesh.normals=mesh.normals.Select(n=>localToCanonical.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            if(localToCanonical.determinant<0)
                for(int sub=0;sub<mesh.subMeshCount;sub++){var tris=mesh.GetTriangles(sub);for(int i=0;i<tris.Length;i+=3){int tmp=tris[i];tris[i]=tris[i+1];tris[i+1]=tmp;}mesh.SetTriangles(tris,sub);}
            var ordered=skin.bones.Select(b=>bones[b]).Concat(new[]{twistMid,twistWrist}).ToArray();
            int foreIndex=Array.IndexOf(skin.bones,Find("forearm.R")),midIndex=ordered.Length-2,distalIndex=ordered.Length-1;
            var weights=mesh.boneWeights;var vertices=mesh.vertices;var arm=wristPosition-elbowPosition;
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];var collected=new Dictionary<int,float>();
                void Add(int bone,float amount){if(amount<=0)return;collected[bone]=collected.TryGetValue(bone,out var old)?old+amount:amount;}
                float t=Mathf.Clamp01(Vector3.Dot(vertices[i]-elbowPosition,arm)/arm.sqrMagnitude);
                void Part(int bone,float amount)
                {
                    if(bone!=foreIndex){Add(bone,amount);return;}
                    if(t<.35f){float a=t/.35f;Add(foreIndex,amount*(1-a));Add(midIndex,amount*a);}
                    else{float a=Mathf.Clamp01((t-.35f)/.45f);Add(midIndex,amount*(1-a));Add(distalIndex,amount*a);}
                }
                Part(w.boneIndex0,w.weight0);Part(w.boneIndex1,w.weight1);Part(w.boneIndex2,w.weight2);Part(w.boneIndex3,w.weight3);
                var top=collected.OrderByDescending(k=>k.Value).Take(4).ToArray();float sum=top.Sum(k=>k.Value);var next=new BoneWeight();
                if(top.Length>0){next.boneIndex0=top[0].Key;next.weight0=top[0].Value/sum;}
                if(top.Length>1){next.boneIndex1=top[1].Key;next.weight1=top[1].Value/sum;}
                if(top.Length>2){next.boneIndex2=top[2].Key;next.weight2=top[2].Value/sum;}
                if(top.Length>3){next.boneIndex3=top[3].Key;next.weight3=top[3].Value/sum;}
                weights[i]=next;
            }
            mesh.boneWeights=weights;mesh.bindposes=ordered.Select(b=>b.worldToLocalMatrix).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Folder+name+"Skin.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
            var render=root.AddComponent<SkinnedMeshRenderer>();render.sharedMesh=saved;render.bones=ordered;
            render.rootBone=body;render.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
            foreach(var t in root.GetComponentsInChildren<Transform>())if((t.localScale-Vector3.one).sqrMagnitude>.00001f)throw new Exception("Scaled baked hand bone: "+t.name);
            var thumb=bones[Find("thumb.02.R")].position;var index=bones[Find("f_index.01.R")].position;
            if(left?thumb.x<=index.x:thumb.x>=index.x)throw new Exception("Baked thumb side is wrong: "+name);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");
            Debug.Log("HAND BAKED "+name+" thumb="+thumb+" index="+index+" positive scale, fixed arm lengths");
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
