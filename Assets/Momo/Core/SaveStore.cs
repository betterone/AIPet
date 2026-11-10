using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Momo.Core
{
    public interface IStateCodec { string Encode(WorldState state); WorldState Decode(string json); }
    public sealed class SaveStore
    {
        readonly string directory;
        readonly IStateCodec codec;
        public SaveStore(string directory,IStateCodec codec){this.directory=directory;this.codec=codec;}
        public string DirectoryPath { get { return directory; } }
        public string LastWarning { get; private set; }
        public WorldState Load()
        {
            WorldState best=null;bool any=false,invalid=false;
            for(int slot=0;slot<2;slot++)
            {
                string path=Path.Combine(directory,"world-"+slot+".save");if(!File.Exists(path))continue;any=true;
                try
                {
                    string raw=File.ReadAllText(path);int nl=raw.IndexOf('\n');if(nl!=64)throw new IOException("Missing integrity header");
                    string body=raw.Substring(nl+1);if(Hash(body)!=raw.Substring(0,nl))throw new IOException("Checksum mismatch");
                    var s=codec.Decode(body);Simulation.Validate(s);if(best==null||s.revision>best.revision)best=s;
                }
                catch(Exception ex) when(ex is IOException||ex is ArgumentException||ex is InvalidOperationException||ex is FormatException)
                {invalid=true;}
            }
            if(any&&best==null)throw new IOException("Both save checkpoints are unreadable. Original files have been preserved.");
            LastWarning=invalid?"Recovered the previous valid checkpoint.":null;
            return best;
        }
        public void Save(WorldState state)
        {
            Simulation.Validate(state);Directory.CreateDirectory(directory);
            long previous=state.revision;state.revision++;
            string destination=Path.Combine(directory,"world-"+(state.revision%2)+".save");
            string temp=destination+".pending";
            try
            {
                string body=codec.Encode(state);byte[] bytes=Encoding.UTF8.GetBytes(Hash(body)+"\n"+body);
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);
            }
            catch { state.revision=previous;throw; }
        }
        static string Hash(string text)
        {using(var sha=SHA256.Create()){return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
    }
}
