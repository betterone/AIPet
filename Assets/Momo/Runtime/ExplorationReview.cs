using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Momo.Core;
namespace Momo
{
    // Exercises the production player paths with an isolated save, then captures phone-sized frames.
    public sealed class ExplorationReview : MonoBehaviour
    {
        void Awake(){Application.runInBackground=true;}
        IEnumerator Start()
        {
            var app=GetComponent<MomoApp>();app.CloseBoutique();app.simulation.State.adopted=true;app.enabled=false;
            var theme=app.room.GetComponent<ToyHomeStyle>();
            if(theme==null||!theme.Applied)throw new Exception("Production room has not applied the toy-home style.");
            foreach(var r in app.room.GetComponentsInChildren<Renderer>())
                if(r.name=="Door partition"&&r.bounds.size.y<3)throw new Exception("Production partition was incorrectly cut away for the visual study.");
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/exploration"));
            Directory.CreateDirectory(directory);string marker=Path.Combine(directory,"PASS.txt");if(File.Exists(marker))File.Delete(marker);
            app.simulation.State.pet=new Point(0,2.2f);app.simulation.State.target=app.simulation.State.pet;
            app.simulation.RestFromCompany();app.cat.transform.position=new Vector3(0,0,2.2f);
            app.window.Approach();for(int i=0;i<120;i++)app.window.Tick(1f/60,false);
            if(app.voice.LoadedClips!=CatAudio.ExpectedClips)throw new Exception("Real cat audio missing");
            yield return CheckCatVoices(app,directory);
            app.company.Start(new Point(0,0));
            var panStart=new Vector2(Screen.width*.8f,Screen.height*.5f);var panEnd=panStart+Vector2.left*80;
            app.HandlePointer(panStart,true,true,false,false);app.HandlePointer(panEnd,false,true,false,false);
            app.SendMessage("UpdateCompany",.1f);
            if(!app.company.Active)throw new Exception("Looking around cancelled the cat's game");
            app.HandlePointer(panEnd,false,false,true,false);app.company.Cancel();
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(320,700)})
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.3f);
                app.window.ReturnToEntry();app.window.Find(app.cat.transform.position);for(int i=0;i<30;i++)app.window.Tick(1f/60,false);
                app.hud.Refresh();yield return Capture(directory,"home-"+size.x);CheckText(app.hud);
                var pad=app.hud.Walk;var stick=WalkPad.InputForOffset(Vector2.right*pad.Radius,pad.Radius);
                pad.Set(11,stick);float x=app.window.ViewerPosition.x;
                for(int i=0;i<30;i++)app.window.Tick(1f/30,false);pad.Release(11);
                if(Vector3.Distance(app.window.ViewerPosition,new Vector3(x,WindowView.Eye.y,0))<.25f||pad.Moving)throw new Exception("Joystick release walking failed");
                var stopped=app.window.ViewerPosition;app.window.Tick(.1f,false);
                if(Vector3.Distance(stopped,app.window.ViewerPosition)>.001f)throw new Exception("Walk kept moving after release");
                pad.Set(11,stick);app.hud.ToggleSettings();app.hud.Refresh();
                if(pad.Moving)throw new Exception("Opening panel left a held direction");
                yield return Capture(directory,"settings-"+size.x);CheckText(app.hud);
                app.hud.SendMessage("SoundCredits");yield return Capture(directory,"sound-credits-"+size.x);CheckText(app.hud);app.hud.ClosePanel();app.hud.Refresh();
                app.window.ReturnToEntry();app.window.MoveBy(new Vector3(7,0,0));
                if(app.window.ViewerPosition.x<6.5f)throw new Exception("Doorway did not connect rooms");
                app.simulation.State.pet=new Point(9.3f,-1.1f);app.simulation.State.target=app.simulation.State.pet;app.simulation.RestFromCompany();
                app.cat.transform.position=new Vector3(9.3f,0,-1.1f);app.window.Find(app.cat.transform.position);
                for(int i=0;i<30;i++){app.window.Tick(1f/60,false);app.cat.Tick(app.simulation.State,1f/60);}
                app.hud.Refresh();yield return Capture(directory,"playroom-"+size.x);CheckText(app.hud);
            }
            app.voice.Locate(app.cat.transform.position,app.window.ViewerPosition,true);app.voice.Play(CatVoice.Invite,true);
            // Walk the actual simulation through a complete autonomous invitation and clue sequence.
            app.company.Start(new Point(7,0));bool hid=false,peeked=false;
            for(int i=0;i<700;i++)
            {
                app.AdvanceWorld(.1f);app.company.Update(.1f,new Point(7,0),false,false,true);
                hid|=app.company.Phase==CompanyPhase.Hidden;peeked|=app.company.Cue==CatVoice.Peek;
                app.cat.Tick(app.simulation.State,.1f);
                if(app.company.Cue!=CatVoice.None)app.voice.Play(app.company.Cue,true);
                if(i%100==0)yield return null;
            }
            if(!hid||!peeked)throw new Exception("Actual simulation failed autonomous hide/peek cycle");
            File.WriteAllText(marker,"Production hold/release controls, panel cancellation, doorway traversal, dynamic Find cat, two phone layouts and thirteen recordings passed. Five adopted cats retain distinct regular meows; startup selection, all three meow cues, purr preservation, fallback cats, mute and overlap/cooldown checks passed. Live simulation hid and offered a clue. Physical ARKit tracking, device touch, listening judgement and child acceptance remain device checks.\n");
            Debug.Log("EXPLORATION REVIEW COMPLETE");Application.Quit();
        }
        static IEnumerator CheckCatVoices(MomoApp app,string directory)
        {
            var audio=app.voice;var source=audio.GetComponent<AudioSource>();
            var names=new[]{"Mikan","Earl","Chai","Oreo","Yuki"};
            string report="";
            for(int i=0;i<names.Length;i++)
            {
                app.AdoptBreed(names[i],i);
                var clip=audio.RegularMeow;
                if(clip==null||clip.name!="Meow"+names[i])throw new Exception("Wrong adopted cat voice: "+names[i]);
                var samples=new float[clip.samples*clip.channels];
                if(!clip.GetData(samples,0)||!Array.Exists(samples,s=>Mathf.Abs(s)>.001f))throw new Exception("Silent/undecodable meow: "+clip.name);
                foreach(var cue in new[]{CatVoice.Invite,CatVoice.Peek,CatVoice.Found})
                {
                    audio.Play(cue,true);
                    if(audio.LastPlayedClip!=clip||!source.isPlaying)throw new Exception("Wrong regular cue: "+cue+" / "+names[i]);
                    audio.Play(CatVoice.Purr,true);
                    if(audio.LastPlayedClip!=clip)throw new Exception("Overlapping audio was allowed");
                    audio.Locate(app.cat.transform.position,app.window.ViewerPosition,false);
                    if(source.isPlaying)throw new Exception("Mute did not stop the sound");
                    audio.Play(cue,false);
                    if(source.isPlaying)throw new Exception("Muted audio played");
                    audio.Locate(app.cat.transform.position,app.window.ViewerPosition,true);
                    audio.Play(cue,true);
                    if(source.isPlaying)throw new Exception("Playback cooldown was bypassed");
                    yield return new WaitForSecondsRealtime(Mathf.Max(clip.length,1f)+.05f);
                }
                report+=names[i]+" -> "+clip.name+" ("+clip.length.ToString("F3")+"s): decoded, all regular cues, mute, overlap and cooldown passed\n";
            }
            audio.Play(CatVoice.Purr,true);
            if(audio.LastPlayedClip==null||!audio.LastPlayedClip.name.StartsWith("Purr"))throw new Exception("Purr was replaced by meow");
            app.AdoptBreed("Shadow",5);
            if(audio.RegularMeow!=null||source.isPlaying)throw new Exception("Fallback selection or voice switch failed");
            audio.Play(CatVoice.Found,true);
            if(!audio.LastPlayedClip.name.StartsWith("Found"))throw new Exception("Fallback cat lost original voice");
            var reload=new GameObject("Voice startup check").AddComponent<CatAudio>();reload.Build(4);
            if(reload.RegularMeow==null||reload.RegularMeow.name!="MeowYuki"||reload.LoadedClips!=CatAudio.ExpectedClips)throw new Exception("Startup voice selection failed");
            Destroy(reload.gameObject);app.AdoptBreed("Mikan",0);
            File.WriteAllText(Path.Combine(directory,"cat-voices.txt"),report+"Original purr and other-cat fallback preserved; switching stops previous audio; startup selection passed.\n");
        }
        static void CheckText(MomoHud hud)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var t in hud.GetComponentsInChildren<TMP_Text>(false))
            {
                t.ForceMeshUpdate();
                if(t.isTextTruncated)throw new Exception("Truncated text: "+t.name+" / "+t.text);
                var corners=new Vector3[4];t.rectTransform.GetWorldCorners(corners);
                if(corners[0].x<-.5f||corners[2].x>Screen.width+.5f||corners[0].y<-.5f||corners[2].y>Screen.height+.5f)
                    throw new Exception("Text outside phone: "+t.name+" / "+t.text+" / "+corners[0]+".."+corners[2]+" / "+Screen.width+"x"+Screen.height);
            }
        }
        static IEnumerator Capture(string folder,string name)
        {yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,name+".png"),frame.EncodeToPNG());Destroy(frame);}
    }
}
