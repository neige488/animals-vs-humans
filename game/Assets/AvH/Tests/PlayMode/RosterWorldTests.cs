using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class RosterWorldTests {
  [UnityTest] public IEnumerator EveryOwnedCharacterRendersAtGroundAndRemoteUsesHostIdentity() {
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");Assert.IsNotNull(catalog);
   Assert.AreEqual(6,catalog.Humans.Length);Assert.AreEqual(6,catalog.Animals.Length);
   Assert.AreEqual(12,catalog.Humans.Concat(catalog.Animals).Select(c=>c.Id).Distinct().Count());
   var source=new PlaytestSession(42);source.BeginSettingsEdit(0);var rules=source.ObserveSettings().Edit;rules.InitialAnimals=6;source.UpdateSettingsEdit(0,rules);source.ApplySettings(0);
   source.StartSolo("roster",humans:catalog.Humans.Select(c=>c.Definition()).ToArray(),animals:catalog.Animals.Select(c=>c.Definition()).ToArray());
   var root=new GameObject("roster world");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());
   try {
    yield return new WaitForSeconds(.2f);
    for(int slot=0;slot<6;slot++)Check(remote,slot,true);
    source.Advance(20);remote.ApplyRemoteSnapshot(source.Observe(),false);yield return new WaitForSeconds(.2f);
    foreach(var player in source.Observe().Players.Where(p=>p.Faction==Faction.Animal)) {
     Check(remote,player.Slot,false);
     Assert.AreEqual(player.CharacterId,remote.Observe().Players[player.Slot].CharacterId);
     Assert.IsNull(remote.PlayerTransform(player.Slot).Find("Bubble emitter"));
    }
    source.Advance(185);remote.ApplyRemoteSnapshot(source.Observe(),true);yield return new WaitForSeconds(.2f);
    for(int slot=0;slot<6;slot++)Check(remote,slot,true);
   } finally {Object.Destroy(root);}
   yield return null;
  }
  static void Check(UnityPlaytestSession world,int slot,bool human) {
   var body=world.PlayerTransform(slot);var state=world.Observe().Players[slot];
   foreach(var a in body.GetComponentsInChildren<Animator>()){Assert.IsNotNull(a.runtimeAnimatorController,state.CharacterName+" controller");Assert.IsTrue(a.isInitialized,state.CharacterName+" initialized animator "+a.name);}
   var meshes=body.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.IsNotEmpty(meshes,state.CharacterName);
   float bottom=float.MaxValue,top=float.MinValue;
   foreach(var r in meshes){Assert.IsTrue(r.enabled);Assert.Greater(r.sharedMesh.vertexCount,0);var baked=new Mesh();r.BakeMesh(baked,true);foreach(var v in baked.vertices){var y=r.transform.TransformPoint(v).y-body.position.y;bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}Object.Destroy(baked);}
   Assert.Less(Mathf.Abs(bottom),.18f,state.CharacterName+" feet");Assert.Greater(top-bottom,.2f,state.CharacterName+" visible size");Assert.Less(top-bottom,3.5f,state.CharacterName+" authored scale");
   if(human){var animator=body.GetComponentsInChildren<Animator>().First(a=>a.isHuman);var gun=body.Find("Bubble emitter");Assert.IsNotNull(gun);Assert.Greater(gun.position.y-body.position.y,1.05f);Assert.Less(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position,gun.position),.25f);Assert.Less(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,gun.position),.35f);}
  }
 }
}
