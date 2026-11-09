using UnityEngine;
namespace AvH {
 // World effects for public impact events, shared by the host and remote views. Presentation only.
 public sealed partial class UnityPlaytestSession {
  int lastPresentedEvent;bool eventsPrimed;
  /// <summary>Shows each public event once (by host id). The first observation of a session only primes, so joins never replay.</summary>
  void PresentEvents(SessionState state) {
   if(state?.Events==null)return;
   int newest=0;foreach(var e in state.Events)newest=Mathf.Max(newest,e.Id);
   if(!eventsPrimed||newest<lastPresentedEvent){eventsPrimed=true;lastPresentedEvent=newest;return;}
   foreach(var e in state.Events)if(e.Id>lastPresentedEvent)Present(e);
   lastPresentedEvent=Mathf.Max(lastPresentedEvent,newest);
  }
  void Present(FeelEvent e) {
   Transform Body(int slot)=>slot>=0&&slot<bodies.Count?bodies[slot].transform:null;
   var actor=Body(e.Actor);var target=Body(e.Target);
   PresentEventAudio(e,actor,target);
   switch(e.Kind) {
    case FeelEventKind.AttackWindup:if(actor!=null)Effects.Emit(actor.position+Vector3.up*1.2f+actor.forward*.3f,new Color(1,.55f,.2f,.85f),5,.5f,false);break; // the readable tell
    case FeelEventKind.AttackMiss:if(actor!=null)Effects.Emit(actor.position+Vector3.up*.8f+actor.forward*.9f,new Color(1,1,1,.7f),6,1.2f,false);break;
    case FeelEventKind.AttackHit:if(target!=null)Effects.Emit(target.position+Vector3.up*.9f,new Color(1,.95f,.7f,.95f),14,2.2f);break;
    case FeelEventKind.Stagger:if(target!=null)Effects.Emit(target.position+Vector3.up*1.4f,new Color(1,.9f,.35f,.9f),6,.9f,false);ToolHit(e,target);break;
    // A friend shoved by a shot: a yellow ring at the feet and sparks over the head, no stun.
    case FeelEventKind.Shoved:ToolHit(e,target);if(target!=null){Effects.Emit(target.position+Vector3.up*.05f,new Color(1,.83f,.25f,.9f),0,0,true);Effects.Emit(target.position+Vector3.up*1.9f,new Color(1,.83f,.25f,.9f),5,.8f,false);}break;
    // The host already raised landing dust from its own physics step.
    case FeelEventKind.Landing:if(IsRemote&&actor!=null)Effects.Emit(actor.position,new Color(.9f,.83f,.64f,.6f),7,1);break;
    case FeelEventKind.PropPushed:Effects.Emit(ToVector(e.Position)+Vector3.up*.1f,new Color(.9f,.83f,.64f,.5f),4,.7f,false);break;
   }
  }
  // The hit of a close-range tool: a big POW for the glove, a pink puff for the cushion. Shots show their own impact.
  void ToolHit(FeelEvent e,Transform target) {
   var state=Observe();if(target==null||e.Actor<0||e.Actor>=state.Players.Length)return;var tool=state.Players[e.Actor].Tool;var at=target.position+Vector3.up*1.1f;
   if(tool==ToolKind.Glove){Effects.Emit(at,new Color(1,.92f,.3f,1),16,3.2f);Effects.Emit(at,Color.white,8,2.2f,false);Audio.Play("punch",at);}
   else if(tool==ToolKind.Cushion){Effects.Emit(at,new Color(.98f,.6f,.75f,.9f),10,2f);Audio.Play("cushion",at,1,.85f);}
  }
  readonly double[] seenToolUse=new double[12];
  /// <summary>
  /// Tool uses read from the public tool timer, every frame, on the host and on guests alike:
  /// the glove fist and the cushion fly out and back, the blower throws paper and dust, and each new use is heard.
  /// </summary>
  void PresentTools(SessionState state) {
   if(state?.Players==null)return;
   for(int i=0;i<state.Players.Length&&i<guns.Length;i++) {
    var p=state.Players[i];var gun=guns[i];if(p.Faction!=Faction.Human||gun==null){seenToolUse[i]=0;continue;}
    double left=p.ToolUseRemaining,total=Tools.ActionSeconds(p.Tool);bool fresh=left>seenToolUse[i]+.005;seenToolUse[i]=left;
    var anim=gun.GetComponent<ToolAnimator>();
    if(anim!=null)anim.SetExtension(left>0&&(p.Tool==ToolKind.Glove||p.Tool==ToolKind.Cushion)?Mathf.Sin(Mathf.PI*(float)(1-left/total)):0);
    if(!fresh)continue;
    var forward=Quaternion.Euler(0,PresentationYaw(i),0)*Vector3.forward;var muzzle=gun.position+forward*.6f;
    switch(p.Tool) {
     case ToolKind.Glove:Audio.Play("punch-whiff",muzzle);break;
     case ToolKind.Cushion:Audio.Play("cushion",muzzle);break;
     case ToolKind.Blower:
      Effects.Spray(muzzle,forward+Vector3.up*.05f,new Color(.96f,.95f,.9f,.95f),5,Tools.BlowerReach*1.6f,.32f,.6f);   // paper scraps
      Effects.Spray(muzzle,forward,new Color(.72f,.64f,.5f,.55f),4,Tools.BlowerReach*1.2f,.45f,.1f);                  // dust
      Audio.Play("gust",muzzle);break;
    }
   }
  }
  void ForgetPresentedEvents(){eventsPrimed=false;lastPresentedEvent=0;}
 }
}
