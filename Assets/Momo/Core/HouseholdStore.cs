using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Momo.Core
{
    public interface IHouseholdCodec
    {
        string EncodeHome(HouseholdState state);
        HouseholdState DecodeHome(string json);
    }
    public sealed class HouseholdStore
    {
        public readonly string DirectoryPath;
        readonly IHouseholdCodec codec;
        readonly IStateCodec legacyCodec;
        readonly System.Collections.Generic.Dictionary<string,string> migrationBackups=new System.Collections.Generic.Dictionary<string,string>();
        public string LastWarning {get;private set;}
        public bool MigratedLegacy {get;private set;}
        public HouseholdStore(string directory,IHouseholdCodec codec,IStateCodec legacyCodec)
        {DirectoryPath=directory;this.codec=codec;this.legacyCodec=legacyCodec;}
        public HouseholdState Load()
        {
            HouseholdState best=null;bool any=false,invalid=false;
            for(int slot=0;slot<2;slot++)
            {
                string file=Path.Combine(DirectoryPath,"household-"+slot+".save");if(!File.Exists(file))continue;any=true;
                try
                {
                    string raw=File.ReadAllText(file);int nl=raw.IndexOf('\n');
                    if(nl!=64||Hash(raw.Substring(nl+1))!=raw.Substring(0,nl))throw new IOException("Checkpoint checksum mismatch");
                    var home=codec.DecodeHome(raw.Substring(nl+1));
                    if(home!=null&&home.schema==2){migrationBackups[file]=raw;home.schema=3;home.play=new PlayProgress();}
                    WorldSimulation.Validate(home);
                    if(best==null||home.revision>best.revision)best=home;
                }
                catch(Exception e) when(e is IOException||e is ArgumentException||e is InvalidOperationException||e is FormatException){invalid=true;}
            }
            if(any&&best==null)throw new IOException("Both household checkpoints are unreadable. Original and legacy files have been preserved.");
            if(best!=null){LastWarning=invalid?"Recovered the previous household checkpoint.":null;return best;}
            var old=new SaveStore(DirectoryPath,legacyCodec);var legacy=old.Load();LastWarning=old.LastWarning;
            if(legacy==null)return null;
            MigratedLegacy=true;return WorldSimulation.Migrate(legacy);
        }
        public void Save(HouseholdState home,int transientCareCatId=0)
        {
            WorldSimulation.Validate(home);Directory.CreateDirectory(DirectoryPath);
            foreach(var entry in migrationBackups)
            {
                string backup=entry.Key+".schema2-"+Hash(entry.Value).Substring(0,12)+".bak";
                if(!File.Exists(backup)){byte[] original=Encoding.UTF8.GetBytes(entry.Value);using(var copy=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)){copy.Write(original,0,original.Length);copy.Flush(true);}}
            }
            migrationBackups.Clear();
            long before=home.revision;home.revision++;
            string destination=Path.Combine(DirectoryPath,"household-"+(home.revision%2)+".save"),pending=destination+".pending";
            try
            {
                string body=codec.EncodeHome(home);
                if(transientCareCatId!=0)
                {
                    // Snapshot only: an autosave during approach must not restore a bath route.
                    var snapshot=codec.DecodeHome(body);
                    foreach(var c in snapshot.cats)if(c.id==transientCareCatId)
                    {c.life.route.Clear();c.life.target=c.life.pet;c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=10;c.waitingFor=Activity.Idle;}
                    body=codec.EncodeHome(snapshot);
                }
                byte[] bytes=Encoding.UTF8.GetBytes(Hash(body)+"\n"+body);
                using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(destination))File.Replace(pending,destination,null);else File.Move(pending,destination);
            }
            catch{home.revision=before;throw;}
        }
        static string Hash(string text)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    }
}
