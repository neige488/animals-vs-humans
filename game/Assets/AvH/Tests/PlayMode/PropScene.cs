using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace AvH.Tests {
 // Shared staging for prop scenarios on the owned village.
 static class PropScene {
  public static void Warp(UnityPlaytestSession world,int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  public static float YawOf(Vector3 direction)=>Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
  /// <summary>Everyone but slot 0 waits far from the market so only the tested body touches a prop.</summary>
  public static void Park(UnityPlaytestSession world){foreach(var p in world.Observe().Players.Where(p=>p.Slot!=0))Warp(world,p.Slot,new Vector3(-36+p.Slot*2.5f,0,-40.5f));Physics.SyncTransforms();}
  /// <summary>A prop with a free straight run through it: from Home+away*before, walking -away passes over the prop's home.</summary>
  public static (PropView prop,Vector3 away) Open(UnityPlaytestSession world,float before,float after,float minHeight=0) {
   var all=world.ObserveProps();
   foreach(var prop in all.Where(p=>p.Height>=minHeight).OrderBy(p=>p.Home.sqrMagnitude))
   for(int k=0;k<8;k++) {
    var away=Quaternion.Euler(0,k*45,0)*Vector3.forward;var start=prop.Home+away*before;
    if(all.Any(o=>o.Index!=prop.Index&&Vector3.Distance(o.Home,prop.Home)<before+after))continue;
    var bottom=start+Vector3.up*.45f;var top=start+Vector3.up*1.45f;
    if(Physics.OverlapCapsule(bottom,top,.42f).Any(c=>!(c is CharacterController)))continue;
    if(Physics.CapsuleCastAll(bottom,top,.42f,-away,before+after).Any(h=>!(h.collider is CharacterController)))continue;
    if(!Physics.Raycast(start+Vector3.up,Vector3.down,1.3f))continue;
    return (prop,away);
   }
   Assert.Fail("No market prop has an open approach");return default;
  }
 }
}
