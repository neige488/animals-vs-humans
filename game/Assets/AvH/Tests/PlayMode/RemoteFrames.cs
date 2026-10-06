using System.Linq;
namespace AvH.Tests {
 // Builds host presentation frames the way a host would publish them, for remote-view tests.
 static class RemoteFrames {
  public static NetworkMotionFrame Of(SessionState state,double hostTime,System.Func<int,float> yaw=null,System.Func<int,LocomotionState> motion=null,System.Func<int,WorldPosition> position=null,System.Func<int,float> pitch=null)=>
   new NetworkMotionFrame{HostTime=hostTime,Round=state.Round,Bodies=state.Players.Select(p=>NetworkBodyMotion.Encode(position==null?p.Position:position(p.Slot),yaw==null?0:yaw(p.Slot),pitch==null?0:pitch(p.Slot),motion==null?new LocomotionState{Grounded=true}:motion(p.Slot))).ToArray()};
  public static NetworkVisualState Facing(SessionState state,double hostTime,int slot,float yaw)=>new NetworkVisualState{Motion=Of(state,hostTime,s=>s==slot?yaw:0)};
 }
}
