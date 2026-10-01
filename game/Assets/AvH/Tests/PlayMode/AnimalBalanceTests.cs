using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class AnimalBalanceTests {
  string profile;GameObject root;UnityPlaytestSession world;
  void Create() {
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);
   var values=setup.ObserveSettings().Edit;values.PreparationSeconds=1;values.InitialAnimals=6;values.InitialAttackGrace=30;
   setup.UpdateSettingsEdit(0,values);setup.ApplySettingsNow(0,true);
   root=new GameObject("animal balance physics");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;world.StartSolo("host",42,profile);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(100,-.5f,0);floor.transform.localScale=new Vector3(100,1,40);
  }
  void Warp(int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  void Settle(){for(int i=0;i<20;i++)world.Step(.02f);}
  void Arrange(){foreach(var p in world.Observe().Players){world.SubmitInput(p.Slot,new PlayerInput());Warp(p.Slot,new Vector3(70+p.Slot*5,0,-8));}Physics.SyncTransforms();Settle();}
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&File.Exists(profile))File.Delete(profile);}
  [Test] public void AllSpeciesSlidersValidateAtBothEndsAndSteps() {
   var rangeMethod=typeof(SettingsPanel).GetMethod("Range",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);var s=new PlaytestSession(1);s.StartSolo("host");
   for(int species=0;species<AnimalBalance.Count;species++)foreach(string key in AnimalBalance.Keys(species)) {
    var range=(Vector3)rangeMethod.Invoke(null,new object[]{key});var field=typeof(PlaytestValues).GetField(key);
    foreach(float n in new[]{range.x,range.y,range.x+range.z}){s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;field.SetValue(v,n);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0),key);}
   }
  }
  [UnityTest] public IEnumerator SpeciesHaveDifferentMovementAndLiveRetuningKeepsRound() {
   Create();yield return new WaitForFixedUpdate();world.Step(1.01f);Arrange();var state=world.Observe();
   Assert.AreEqual(6,state.Players.Count(p=>p.Faction==Faction.Animal));Assert.AreEqual(6,state.Players.Where(p=>p.Faction==Faction.Animal).Select(p=>p.CharacterId).Distinct().Count());
   foreach(var p in state.Players.Where(p=>p.Faction==Faction.Animal)) {
    float from=world.PlayerTransform(p.Slot).position.x;var rules=world.Session.ObserveSettings().Current;var modifiers=AnimalBalance.For(rules,p);
    for(int n=0;n<10;n++){world.SubmitInput(p.Slot,new PlayerInput{Right=1});world.Step(.02f);}
    Assert.That(world.PlayerTransform(p.Slot).position.x-from,Is.EqualTo(.2f*rules.AnimalSpeed*modifiers.Speed).Within(.08f),p.CharacterName);
    world.SubmitInput(p.Slot,new PlayerInput());
   }
   var animal=state.Players.First(p=>p.CharacterId=="animal-rabbit_brown");world.Session.BeginSettingsEdit(0);var edit=world.Session.ObserveSettings().Edit;edit.RabbitSpeedMultiplier=2;
   world.Session.UpdateSettingsEdit(0,edit);Assert.IsTrue(world.Session.ApplySettingsNow(0));float start=world.PlayerTransform(animal.Slot).position.x;
   for(int n=0;n<10;n++){world.SubmitInput(animal.Slot,new PlayerInput{Right=1});world.Step(.02f);}world.SubmitInput(animal.Slot,new PlayerInput());
   Assert.That(world.PlayerTransform(animal.Slot).position.x-start,Is.EqualTo(.2f*5.6f*2).Within(.08f));Assert.AreEqual(state.Round,world.Observe().Round);
   Settle();var initial=state.Players.Select(p=>world.PlayerTransform(p.Slot).position.y).ToArray();var peak=(float[])initial.Clone();
   foreach(var p in state.Players.Where(p=>p.Faction==Faction.Animal))world.SubmitInput(p.Slot,new PlayerInput{Jump=true});
   for(int n=0;n<65;n++){world.Step(.02f);foreach(var p in state.Players)peak[p.Slot]=Mathf.Max(peak[p.Slot],world.PlayerTransform(p.Slot).position.y);}
   foreach(var p in state.Players.Where(p=>p.Faction==Faction.Animal))Assert.That(peak[p.Slot]-initial[p.Slot],Is.EqualTo(1.5f*AnimalBalance.For(world.Session.ObserveSettings().Current,p).Jump).Within(.22f),p.CharacterName+" jump");
   world.Session.BeginSettingsEdit(0);edit=world.Session.ObserveSettings().Edit;edit.RabbitJumpMultiplier=.8f;world.Session.UpdateSettingsEdit(0,edit);world.Session.ApplySettingsNow(0);
   float floor=world.PlayerTransform(animal.Slot).position.y,high=floor;world.SubmitInput(animal.Slot,new PlayerInput{Jump=true});
   for(int n=0;n<65;n++){world.Step(.02f);high=Mathf.Max(high,world.PlayerTransform(animal.Slot).position.y);}
   Assert.That(high-floor,Is.EqualTo(1.2f).Within(.22f));
   var human=state.Players.First(p=>p.Faction==Faction.Human);float origin=world.PlayerTransform(human.Slot).position.x;
   for(int n=0;n<10;n++){world.SubmitInput(human.Slot,new PlayerInput{Right=1});world.Step(.02f);}Assert.That(world.PlayerTransform(human.Slot).position.x-origin,Is.EqualTo(1).Within(.08f));
   yield return null;
  }
  float Hit(int shooter,int target) {
   Arrange();Warp(shooter,new Vector3(100,0,0));Warp(target,new Vector3(100,0,5));Physics.SyncTransforms();Settle();
   int count=world.ObserveBubbleHits()[target];float from=world.PlayerTransform(target).position.z;
   world.SubmitInput(shooter,new PlayerInput{Attack=true});world.Step(.02f);world.SubmitInput(shooter,new PlayerInput());
   for(int n=0;n<50;n++)world.Step(.02f);
   Assert.AreEqual(count+1,world.ObserveBubbleHits()[target]);return world.PlayerTransform(target).position.z-from;
  }
  [UnityTest] public IEnumerator BubbleImpactUsesVictimSpeciesAndLiveResistance() {
   Create();yield return new WaitForFixedUpdate();world.Step(1.01f);var state=world.Observe();int shooter=state.Players.First(p=>p.Faction==Faction.Human).Slot;
   int fox=state.Players.Single(p=>p.CharacterId=="animal-fox").Slot,bear=state.Players.Single(p=>p.CharacterId=="animal-bear_grizzly").Slot;
   float foxDistance=Hit(shooter,fox),bearDistance=Hit(shooter,bear);Assert.Greater(bearDistance,.01f);Assert.Less(bearDistance,foxDistance*.65f);
   world.Session.BeginSettingsEdit(0);var values=world.Session.ObserveSettings().Edit;values.BearKnockbackMultiplier=1.5f;world.Session.UpdateSettingsEdit(0,values);world.Session.ApplySettingsNow(0);
   Assert.Greater(Hit(shooter,bear),bearDistance*2);Assert.AreEqual(1,world.Observe().Round);yield return null;
  }
 }
}
