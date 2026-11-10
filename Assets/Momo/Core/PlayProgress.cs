using System;
using System.Collections.Generic;
using System.Globalization;
namespace Momo.Core
{
    [Serializable] public sealed class CatPlayProgress { public int catId,catches,skillLevel; }
    [Serializable] public sealed class PlayRecord { public int catId,layout,rules,score,bestCombo;public uint seed; }
    [Serializable] public sealed class DailyPlay { public string date;public int layout;public uint seed; }
    [Serializable] public sealed class PlayLog { public string date,kind;public long runId;public int catId,value; }
    [Serializable] public sealed class PlayProgress
    {
        public long nextRunId=1,lastSettledRunId,activeRunId;
        public int activeCatId,activeLayout,activeRules,completedRuns,qualifiedRuns,facilityLevel;
        public uint activeSeed;
        public List<CatPlayProgress> cats=new List<CatPlayProgress>();
        public List<PlayRecord> records=new List<PlayRecord>();
        public List<DailyPlay> daily=new List<DailyPlay>();
        public List<PlayLog> log=new List<PlayLog>();
        public static string DateKey(DateTime local)=>local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        public static string FacilityName(int level)=>level==1?"扑抓垫":level==2?"矮纸箱":level==3?"纸箱隧道":"平地";
        public static string SkillName(int level)=>level==1?"接羽毛":level==2?"折返扑":level==3?"绕障接扑":"认识羽毛";
        public CatPlayProgress Cat(int id)
        {foreach(var c in cats)if(c.catId==id)return c;var cnew=new CatPlayProgress{catId=id};cats.Add(cnew);return cnew;}
        public void Log(string kind,int catId,long runId,int value,DateTime local)
        {log.Add(new PlayLog{date=DateKey(local),kind=kind,catId=catId,runId=runId,value=value});if(log.Count>512)log.RemoveAt(0);}
        public void Visit(DateTime local)
        {
            string key=DateKey(local);bool seen=false;foreach(var e in log)if(e.kind=="visit"&&e.date==key){seen=true;break;}
            if(!seen)Log("visit",0,0,0,local);
            for(int i=6;i>=0;i--)Recommendation(local.Date.AddDays(-i));
        }
        public DailyPlay Recommendation(DateTime local)
        {
            string key=DateKey(local);foreach(var d in daily)if(d.date==key)return d;
            uint seed=2166136261;foreach(char c in key){seed^=c;seed*=16777619;}if(seed==0)seed=1;
            var entry=new DailyPlay{date=key,seed=seed,layout=(int)(seed%(uint)(facilityLevel+1))};daily.Add(entry);
            daily.Sort((a,b)=>string.CompareOrdinal(a.date,b.date));while(daily.Count>7)daily.RemoveAt(0);return entry;
        }
        public WandChallengeSession Begin(int catId,int layout,uint seed,PlayTuning tuning,DateTime local,bool retry=false)
        {
            tuning.Validate();if(catId<1||layout<0||layout>facilityLevel||seed==0||nextRunId==long.MaxValue)throw new ArgumentException("Unavailable challenge");
            activeRunId=nextRunId++;activeCatId=catId;activeLayout=layout;activeSeed=seed;activeRules=tuning.rulesVersion;
            Log(retry?"retry":"start",catId,activeRunId,layout,local);return new WandChallengeSession(activeRunId,catId,layout,seed,tuning);
        }
        public bool Settle(WandChallengeResult result,PlayTuning tuning,DateTime local)
        {
            if(result==null||result.Id!=activeRunId||result.Id<=lastSettledRunId||result.CatId!=activeCatId||result.Layout!=activeLayout||result.Seed!=activeSeed||result.Rules!=activeRules)return false;
            int oldFacility=facilityLevel;var c=Cat(result.CatId);int oldSkill=c.skillLevel;
            completedRuns++;if(result.Catches>=tuning.qualifyingCatches)qualifiedRuns++;
            c.catches+=result.Catches;c.skillLevel=Math.Max(c.skillLevel,PlayTuning.Level(c.catches,tuning.skillThresholds));facilityLevel=Math.Max(facilityLevel,PlayTuning.Level(qualifiedRuns,tuning.facilityThresholds));
            PlayRecord record=null;foreach(var r in records)if(r.catId==result.CatId&&r.layout==result.Layout&&r.rules==result.Rules&&r.seed==result.Seed){record=r;break;}
            if(record==null){record=new PlayRecord{catId=result.CatId,layout=result.Layout,rules=result.Rules,seed=result.Seed};records.Add(record);if(records.Count>256)records.RemoveAt(0);}
            record.score=Math.Max(record.score,result.Score);record.bestCombo=Math.Max(record.bestCombo,result.BestCombo);
            lastSettledRunId=result.Id;activeRunId=0;Log("end",result.CatId,result.Id,result.Score,local);
            for(int i=oldSkill+1;i<=c.skillLevel;i++)Log("skill",result.CatId,result.Id,i,local);
            for(int i=oldFacility+1;i<=facilityLevel;i++)Log("facility",result.CatId,result.Id,i,local);return true;
        }
        public void Abort(DateTime local){if(activeRunId==0)return;Log("abort",activeCatId,activeRunId,0,local);activeRunId=0;}
        public int Best(int catId,int layout,uint seed,int rules)
        {foreach(var r in records)if(r.catId==catId&&r.layout==layout&&r.seed==seed&&r.rules==rules)return r.score;return 0;}
        public string NextGoal(int catId,PlayTuning tuning)
        {
            var c=Cat(catId);
            if(facilityLevel<3)return "再完成 "+Math.Max(0,tuning.facilityThresholds[facilityLevel]-qualifiedRuns)+" 局配合，搭好"+FacilityName(facilityLevel+1)+"。";
            if(c.skillLevel<3)return "再抓到 "+Math.Max(0,tuning.skillThresholds[c.skillLevel]-c.catches)+" 次羽毛，学会"+SkillName(c.skillLevel+1)+"。";
            return "新本领都学会了。试试今天的路线，刷新自己的纪录。";
        }
        public static void Validate(PlayProgress p,HashSet<int> ids)
        {
            if(p==null||p.nextRunId<1||p.lastSettledRunId<0||p.lastSettledRunId>=p.nextRunId||p.activeRunId<0||p.activeRunId>=p.nextRunId||p.activeRunId>0&&(p.activeRunId<=p.lastSettledRunId||!ids.Contains(p.activeCatId)||p.activeLayout<0||p.activeLayout>p.facilityLevel||p.activeSeed==0||p.activeRules<1)||p.completedRuns<0||p.qualifiedRuns<0||p.qualifiedRuns>p.completedRuns||p.facilityLevel<0||p.facilityLevel>3||p.cats==null||p.cats.Count>5||p.records==null||p.records.Count>256||p.daily==null||p.daily.Count>7||p.log==null||p.log.Count>512)throw new ArgumentException("Invalid play progress");
            var seen=new HashSet<int>();foreach(var c in p.cats)if(c==null||!ids.Contains(c.catId)||!seen.Add(c.catId)||c.catches<0||c.skillLevel<0||c.skillLevel>3)throw new ArgumentException("Invalid cat play progress");
            var keys=new HashSet<string>();foreach(var r in p.records)if(r==null||!ids.Contains(r.catId)||r.layout<0||r.layout>3||r.rules<1||r.seed==0||r.score<0||r.bestCombo<0||!keys.Add(r.catId+"/"+r.layout+"/"+r.rules+"/"+r.seed))throw new ArgumentException("Invalid play record");
            var dates=new HashSet<string>();foreach(var d in p.daily)if(d==null||!Date(d.date)||!dates.Add(d.date)||d.layout<0||d.layout>p.facilityLevel||d.seed==0)throw new ArgumentException("Invalid recommendation");
            foreach(var e in p.log)if(e==null||!Date(e.date)||e.runId<0||e.runId>=p.nextRunId||e.catId!=0&&!ids.Contains(e.catId)||e.value<0||(e.kind!="visit"&&e.kind!="start"&&e.kind!="retry"&&e.kind!="end"&&e.kind!="abort"&&e.kind!="skill"&&e.kind!="facility"))throw new ArgumentException("Invalid play log");
        }
        static bool Date(string key)=>DateTime.TryParseExact(key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
    }
}
