using Momo.Core;

static class HudTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> assert)
    {
        test("exploration hud hides after six real idle seconds and wakes",()=>{
            var h=new HudVisibility();h.Advance(5.9f,false,false);assert(!h.Quiet,"hid too soon");h.Advance(.11f,false,false);assert(h.Quiet,"idle hud stayed visible");
            assert(h.Wake()&&!h.Quiet,"quiet tap did not wake");assert(!h.Wake(),"awake touch treated as a wake-only tap");
        });
        test("held input and care keep essential controls visible",()=>{
            var h=new HudVisibility();h.Advance(8,false,false);h.Advance(10,true,false);assert(!h.Quiet&&h.IdleSeconds==0,"held input hidden");
            h.Advance(100,false,true);assert(!h.Quiet,"care controls hidden");h.Advance(5,false,false);assert(!h.Quiet,"care exit hid immediately");h.Advance(2,false,false);assert(h.Quiet,"care exit never returned to quiet");
        });
        test("always visible preference suppresses auto hiding",()=>{
            var h=new HudVisibility{AutoHide=false};h.Advance(100,false,false);assert(!h.Quiet,"always visible ignored");
            h.AutoHide=true;h.Wake();h.Advance(7,false,false);assert(h.Quiet,"restoring automatic mode failed");
        });
        test("hud timer rejects invalid input and is partition independent",()=>{
            var a=new HudVisibility();var b=new HudVisibility();a.Advance(7,false,false);for(int i=0;i<70;i++)b.Advance(.1f,false,false);assert(a.Quiet==b.Quiet,"fragmented time changed hiding");
            float before=a.IdleSeconds;foreach(float dt in new[]{float.NaN,float.PositiveInfinity,-1})a.Advance(dt,false,false);assert(a.IdleSeconds==before,"invalid timer corrupted hud");
        });
    }
}
