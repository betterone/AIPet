using UnityEngine;
using Momo.Core;
namespace Momo
{
    public sealed class CatAudio : MonoBehaviour
    {
        AudioSource source;AudioLowPassFilter filter;AudioClip[][] banks;
        int[] last={-1,-1,-1,-1};float nextAt;
        AudioClip[] regularMeows;
        int catIndex=-1;
        public const int ExpectedClips=13;
        public int LoadedClips {get;private set;}
        public AudioClip RegularMeow {get;private set;}
        public AudioClip LastPlayedClip {get;private set;}
        public void Build(int breedIndex=0)
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;
            source.minDistance=1.5f;source.maxDistance=22;source.rolloffMode=AudioRolloffMode.Linear;source.volume=.65f;
            filter=gameObject.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=16000;
            banks=new AudioClip[4][];
            var names=new[]{"Invite","Peek","Found","Purr"};
            for(int b=0;b<4;b++){banks[b]=new AudioClip[2];for(int i=0;i<2;i++){banks[b][i]=Resources.Load<AudioClip>("CatAudio/"+names[b]+(i+1));if(banks[b][i]!=null)LoadedClips++;}}
            var cats=new[]{"Mikan","Earl","Chai","Oreo","Yuki"};
            regularMeows=new AudioClip[cats.Length];
            for(int i=0;i<cats.Length;i++)
            {
                regularMeows[i]=Resources.Load<AudioClip>("CatAudio/Meow"+cats[i]);
                if(regularMeows[i]!=null)LoadedClips++;
            }
            if(LoadedClips!=ExpectedClips)Debug.LogError("Cat voice recordings missing: "+LoadedClips+"/"+ExpectedClips);
            SelectCat(breedIndex);
        }
        public void SelectCat(int breedIndex)
        {
            if(catIndex==breedIndex)return;
            catIndex=breedIndex;
            RegularMeow=regularMeows!=null&&breedIndex>=0&&breedIndex<regularMeows.Length?regularMeows[breedIndex]:null;
            if(source!=null)source.Stop();
            nextAt=0;
        }
        public void Locate(Vector3 pet,Vector3 viewer,bool enabled)
        {
            transform.position=pet+Vector3.up*.5f;
            bool blocked=!Room.Clear(new Point(pet.x,pet.z),new Point(viewer.x,viewer.z));
            filter.cutoffFrequency=blocked?2500:16000;source.volume=enabled?(blocked?.48f:.65f):0;
            if(!enabled&&source.isPlaying)source.Stop();
        }
        public void Play(CatVoice voice,bool enabled)
        {
            if(!enabled||voice==CatVoice.None||Time.unscaledTime<nextAt||banks==null||source.isPlaying)return;
            int b=voice==CatVoice.Invite?0:voice==CatVoice.Peek?1:voice==CatVoice.Found?2:3;
            int index=last[b]<0?Random.Range(0,2):1-last[b];var clip=banks[b][index];
            if(voice!=CatVoice.Purr&&RegularMeow!=null)clip=RegularMeow;
            if(clip==null)return;last[b]=index;nextAt=Time.unscaledTime+Mathf.Max(clip.length,voice==CatVoice.Purr?2.5f:1f);
            LastPlayedClip=clip;source.PlayOneShot(clip);
        }
    }
}
