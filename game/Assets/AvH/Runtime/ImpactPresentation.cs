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
    case FeelEventKind.Stagger:if(target!=null)Effects.Emit(target.position+Vector3.up*1.4f,new Color(1,.9f,.35f,.9f),6,.9f,false);break;
    // The host already raised landing dust from its own physics step.
    case FeelEventKind.Landing:if(IsRemote&&actor!=null)Effects.Emit(actor.position,new Color(.9f,.83f,.64f,.6f),7,1);break;
   }
  }
  void ForgetPresentedEvents(){eventsPrimed=false;lastPresentedEvent=0;}
 }
}
