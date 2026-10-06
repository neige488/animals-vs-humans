using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // A remote view plays host frames back through the same animator path the host uses.
 public class RemotePlaybackTests {
  GameObject root;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);}
  const float Dt=1/60f,Send=.05f;
  // What the host showed for each slot at host time t: slot 3 runs, jumps, then stands aiming down; the first animal tells and swings.
  static WorldPosition Start(SessionState s,int slot)=>s.Players[slot].Position;
  static float RunX(float t)=>4*Mathf.Min(t,1.5f);
  static LocomotionState Runner(float t){
   bool air=t>=1&&t<1.5f;
   return new LocomotionState{VelocityX=t<1.5f?4:0,VerticalSpeed=air?6-24*(t-1):0,Grounded=!air,TopSpeed=5};
  }
  static LocomotionState Attacker(float t){
   if(t>=.5f&&t<.65f)return new LocomotionState{Grounded=true,Action=ActionPhase.Windup,ActionProgress=(t-.5f)/.15f};
   if(t>=.65f&&t<.85f)return new LocomotionState{Grounded=true,Action=ActionPhase.Swing,ActionProgress=(t-.65f)/.2f};
   return new LocomotionState{Grounded=true};
  }
  [UnityTest] public IEnumerator JitteredFramesPlayTheHostsRunJumpAttackAndAimWithoutTeleportOrFlicker() {
   var source=new PlaytestSession(123);source.StartSolo("source");source.Advance(20.01);var state=source.Observe();
   int animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int runner=state.Players.First(p=>p.Faction==Faction.Human&&p.Slot!=0).Slot;
   root=new GameObject("remote playback");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(state);remote.AutomaticRemotePlayback=false;yield return null;
   var start=Start(state,runner);
   NetworkMotionFrame Frame(float t)=>RemoteFrames.Of(state,t,
    yaw:s=>s==runner?90:0,pitch:s=>s==runner&&t>=1.5f?-20:0,
    position:s=>s==runner?new WorldPosition(start.X+RunX(t),start.Y+(t>=1&&t<1.5f?6*(t-1)-12*(t-1)*(t-1):0),start.Z):state.Players[s].Position,
    motion:s=>s==runner?Runner(t):s==animal?Attacker(t):new LocomotionState{Grounded=true});
   // 20 Hz host frames that arrive bunched and stalled (TCP keeps order).
   var random=new System.Random(5);var arrivals=new List<(float at,float host)>();float last=0;
   for(float h=0;h<2.5f;h+=Send){last=Mathf.Max(last,h+.02f+(float)random.NextDouble()*.045f);arrivals.Add((last,h));}
   int next=0;var gaits=new List<LocomotionGait>();var actions=new HashSet<ActionPhase>();float maxStep=0,previous=float.NaN;bool airborne=false;
   for(float now=0;now<2.6f;now+=Dt) {
    while(next<arrivals.Count&&arrivals[next].at<=now){remote.ApplyRemoteVisuals(new NetworkVisualState{Motion=Frame(arrivals[next].host)});next++;}
    remote.Step(Dt);
    float x=remote.PlayerTransform(runner).position.x;if(!float.IsNaN(previous))maxStep=Mathf.Max(maxStep,Mathf.Abs(x-previous));previous=x;
    var view=remote.ObserveAnimation(runner);if(now>.3f&&now<.95f)gaits.Add(view.Gait);airborne|=view.Gait==LocomotionGait.Airborne;
    actions.Add(remote.ObserveAnimation(animal).Action);
    if(now>.6f&&now<.9f)Assert.Less(Mathf.Abs(x-(start.X+RunX(now))),1,"Runner plays slightly behind the host, not far behind");
    if(Mathf.Abs(now-.8f)<Dt/2)Assert.Less(x,start.X+RunX(now)-.2f,"Playback keeps a small jitter buffer behind the newest frame");
   }
   Assert.LessOrEqual(maxStep,4*Dt*1.3f+.01f,"No teleport or catch-up jump under jitter");
   CollectionAssert.DoesNotContain(gaits,LocomotionGait.Idle,"A steady remote runner never flickers to idle");
   Assert.IsTrue(airborne,"The host's jump shows as airborne");
   Assert.IsTrue(actions.Contains(ActionPhase.Windup)&&actions.Contains(ActionPhase.Swing),"The host's attack tell and swing reach the remote animator");
   Assert.AreEqual(-20,remote.PlayerTransform(runner).GetComponentInChildren<BubbleGunPose>().AimPitch,.05,"Aim pitch follows the host, not only shots");
   Assert.AreEqual(LocomotionGait.Idle,remote.ObserveAnimation(runner).Gait);
  }
  [UnityTest] public IEnumerator StalledStreamStandsStillAndTheViewersOwnBodySkipsTheBuffer() {
   var source=new PlaytestSession(123);source.StartSolo("source");var state=source.Observe();
   root=new GameObject("remote stall");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(state);remote.AutomaticRemotePlayback=false;remote.LocalViewerSlot=5;yield return null;
   var a=Start(state,3);var b=Start(state,5);
   NetworkMotionFrame Frame(float t)=>RemoteFrames.Of(state,t,position:s=>s==3?new WorldPosition(a.X+4*t,a.Y,a.Z):s==5?new WorldPosition(b.X+4*t,b.Y,b.Z):state.Players[s].Position,
    motion:s=>s==3||s==5?new LocomotionState{VelocityX=4,Grounded=true,TopSpeed=5}:new LocomotionState{Grounded=true});
   float host=0;
   float behind=0,own=0;int samples=0;
   for(float now=0;now<1.5f;now+=Dt){
    while(host<=now-.02f){remote.ApplyRemoteVisuals(new NetworkVisualState{Motion=Frame(host)});host+=Send;}remote.Step(Dt);
    if(now>1){behind+=(a.X+4*(host-Send))-remote.PlayerTransform(3).position.x;own+=(b.X+4*(host-Send))-remote.PlayerTransform(5).position.x;samples++;}
   }
   behind/=samples;own/=samples;
   Assert.Greater(behind,.2f,"Other characters play from the jitter buffer");Assert.Less(own,behind-.1f,"The viewer's own body follows the newest frame");
   Assert.AreNotEqual(LocomotionGait.Idle,remote.ObserveAnimation(3).Gait);
   for(int i=0;i<40;i++)remote.Step(Dt);
   Assert.AreEqual(LocomotionGait.Idle,remote.ObserveAnimation(3).Gait,"A stalled stream holds the last pose and stops running in place");
   Assert.AreEqual(a.X+4*(host-Send),remote.PlayerTransform(3).position.x,.02,"and holds the newest host position");
  }
 }
}
