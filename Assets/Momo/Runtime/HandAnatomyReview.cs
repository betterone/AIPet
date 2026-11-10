using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Momo
{
    // Neutral anatomical views of the SAME prefabs/pose driver as production.
    public sealed class HandAnatomyReview : MonoBehaviour
    {
        IEnumerator Start()
        {
            var app=GetComponent<MomoApp>();app.enabled=false;app.CloseBoutique();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/hand-anatomy/calibration"));Directory.CreateDirectory(folder);
            var run=Run(app,folder);while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(folder,"FAIL.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        IEnumerator Run(MomoApp app,string folder)
        {
            File.Delete(Path.Combine(folder,"PASS.txt"));File.Delete(Path.Combine(folder,"FAIL.txt"));
            PlayerReviewCapture.Resize(app,1024,768);yield return null;
            app.room.gameObject.SetActive(false);app.hud.gameObject.SetActive(false);app.hands.gameObject.SetActive(false);
            foreach(var cat in app.cats.Values)cat.gameObject.SetActive(false);
            var material=Art.Mat("Hand inspection skin","#FFFFFF",.12f);material.mainTexture=Resources.Load<Texture2D>("CompanionHand/Skin");material.SetFloat("_SpecularHighlights",0);
            var views=new CompanionHandView[2];
            for(int i=0;i<2;i++){var g=new GameObject(i==0?"LEFT":"RIGHT");g.transform.position=new Vector3(i==0?-.26f:.26f,0,0);views[i]=g.AddComponent<CompanionHandView>();views[i].Build(material,i==0);}
            if(views[0].ModelKey!="Right"||views[1].ModelKey!="Left")throw new Exception("Requested hand model swap was not applied");
            var camera=app.room.cameraView;camera.enabled=true;camera.orthographic=true;camera.orthographicSize=.40f;camera.backgroundColor=GameHudStyle.Paper;camera.transform.position=new Vector3(0,2,0);camera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            foreach(var curl in new[]{0f,28f,70f})
            {
                foreach(var v in views)v.Pose(curl,camera,true);yield return null;
                PlayerReviewCapture.Capture(app,Path.Combine(folder,"palms-"+curl+".png"));
                foreach(var v in views)
                {if(!v.AnatomyValid||v.MaximumLengthError>.0001f||v.MaximumWristAngle>19)throw new Exception("Hand anatomy/length invariant failed");foreach(var t in v.GetComponentsInChildren<Transform>())if((t.localScale-Vector3.one).sqrMagnitude>.00001f)throw new Exception("Scaled hand bone "+t.name);}
            }
            camera.transform.position=new Vector3(0,-2,0);camera.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
            foreach(var v in views)v.Pose(0,camera,true);yield return null;PlayerReviewCapture.Capture(app,Path.Combine(folder,"dorsal.png"));
            File.WriteAllText(Path.Combine(folder,"PASS.txt"),"Neutral LEFT/RIGHT palms, cupped and grasp poses, plus dorsal view captured from production prefabs. User-requested binding verified: LEFT action uses Right skin; RIGHT action uses Left skin. All joints use positive unit scale; upper/forearm length error <0.0001; wrist bend <=19 degrees. Desktop calibration views, not naturalness/device acceptance.\n");
            Debug.Log("HAND ANATOMY REVIEW COMPLETE");Application.Quit();
        }
    }
}
