using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Abilities in the real world: physics bodies obey them, the scene shows them, and bots press Q on their own.
 public class AbilityPlayTests {
  static UnityPlaytestSession World(GameObject root,int seed,bool bots,System.Action<PlaytestValues> tune=null) {
   string profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var v=setup.ObserveSettings().Edit;v.PreparationSeconds=1;v.InitialAttackGrace=0;tune?.Invoke(v);setup.UpdateSettingsEdit(0,v);Assert.IsTrue(setup.ApplySettingsNow(0,true));
   var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotsDriveEveryone=bots;game.BotAutomationEnabled=bots;
   game.StartSolo("tester",seed,profile,0);return game;
  }
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static IEnumerator IntoChase(UnityPlaytestSession game){for(int i=0;i<200&&game.Observe().Phase!=RoundPhase.Chase;i++)game.Step(.02f);Assert.AreEqual(RoundPhase.Chase,game.Observe().Phase);yield return null;}

  [UnityTest] public IEnumerator BodiesObeyAbilitiesAndTheSceneShowsThem() {
   GameObject root=null;
   try {
    // The first animals are drawn by the seed: take a world where the three jobs under test are still human.
    UnityPlaytestSession game=null;SessionState state=null;
    for(int seed=5;seed<25;seed++) {
     root=new GameObject("ability world");game=World(root,seed,false);yield return null;yield return IntoChase(game);state=game.Observe();
     if(new[]{AbilityKind.Kickboard,AbilityKind.Umbrella,AbilityKind.Scarecrow}.All(k=>state.Players.Any(p=>p.Faction==Faction.Human&&p.Ability==k)))break;
     Object.Destroy(root);root=null;yield return null;
    }
    Assert.NotNull(root);int round=state.Round;
    // Roster order is pinned for tests: every human slot knows its job.
    int rider=state.Players.First(p=>p.Faction==Faction.Human&&p.Ability==AbilityKind.Kickboard).Slot;
    int jumper=state.Players.First(p=>p.Faction==Faction.Human&&p.Ability==AbilityKind.Umbrella).Slot;
    int farmer=state.Players.First(p=>p.Faction==Faction.Human&&p.Ability==AbilityKind.Scarecrow).Slot;
    int clown=-1;foreach(var p in state.Players)if(p.Faction==Faction.Human&&p.Ability==AbilityKind.Balloon)clown=p.Slot;
    Vector3 start=V(state.Players[rider].Position);float yaw=Mathf.Atan2(-start.x,-start.z)*Mathf.Rad2Deg;
    float ground=state.Players[jumper].Position.Y;
    Assert.IsTrue(game.Session.TryUseAbility(rider,round,yaw,0));Assert.IsTrue(game.Session.TryUseAbility(jumper,round,0,0));Assert.IsTrue(game.Session.TryUseAbility(farmer,round,0,0));
    float top=ground;for(int i=0;i<25;i++){game.Step(.02f);top=Mathf.Max(top,game.Observe().Players[jumper].Position.Y);}
    yield return null;
    var moved=V(game.Observe().Players[rider].Position)-start;moved.y=0;
    Assert.Greater(moved.magnitude,2f,"킥보드는 입력 없이도 곧장 달린다");
    Assert.Greater(top-ground,.9f,"우산은 폴짝 띄운다");
    Assert.AreEqual((int)AbilityKind.Kickboard,game.AbilityPropKey(rider));Assert.AreEqual((int)AbilityKind.Umbrella,game.AbilityPropKey(jumper));
    Assert.AreEqual(1,game.GadgetViewCount,"허수아비가 서 있다");
    // The umbrella caps the fall: slower than a free fall from the same height would end.
    float fastest=0,last=game.Observe().Players[jumper].Position.Y;
    for(int i=0;i<40&&game.Observe().Players[jumper].AbilityActiveRemaining>0;i++){game.Step(.02f);float y=game.Observe().Players[jumper].Position.Y;fastest=Mathf.Max(fastest,(last-y)/.02f);last=y;}
    Assert.Less(fastest,2.6f,"우산을 펴면 천천히 내려온다");
    for(int i=0;i<460;i++)game.Step(.02f);yield return null;
    Assert.AreEqual(0,game.AbilityPropKey(rider),"끝나면 소품도 사라진다");Assert.AreEqual(0,game.GadgetViewCount,"허수아비는 8초 뒤 쓰러진다");
    if(clown>=0&&game.Observe().Players[clown].Faction==Faction.Human) {
     float floor=game.Observe().Players[clown].Position.Y;Assert.IsTrue(game.Session.TryUseAbility(clown,round,0,0));
     for(int i=0;i<110;i++)game.Step(.02f);yield return null;
     if(game.Observe().Players[clown].AbilityActiveRemaining>0){Assert.Greater(game.Observe().Players[clown].Position.Y-floor,2.5f,"풍선은 사람을 들어 올린다");Assert.AreEqual((int)AbilityKind.Balloon,game.AbilityPropKey(clown));}
    }
   } finally {if(root!=null)Object.Destroy(root);}yield return null;
  }
  [UnityTest] public IEnumerator HeldAnimalStaysPutAndWearsTheHold() {
   var root=new GameObject("ability hold world");
   try {
    var game=World(root,5,false);yield return null;yield return IntoChase(game);
    var state=game.Observe();int round=state.Round;
    int cowboy=state.Players.First(p=>p.Faction==Faction.Human&&p.Ability==AbilityKind.Lasso).Slot,animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
    var from=V(state.Players[cowboy].Position);var to=V(state.Players[animal].Position);
    // Rules only: put the animal in front of the cowboy for the throw; the physics body never left its place.
    var sight=game.Session.Sight;game.Session.Sight=null;game.Session.RecordWorldPosition(animal,new WorldPosition(from.x,from.y,from.z+1));
    Assert.IsTrue(game.Session.TryUseAbility(cowboy,round,0,0));game.Session.Sight=sight;
    Assert.Greater(game.Session.Observe().Players[animal].RootRemaining,0,"코앞의 동물은 놓치지 않는다");
    for(int i=0;i<40;i++){game.SubmitInput(animal,new PlayerInput{Forward=1,Jump=true});game.Step(.02f);}
    yield return null;
    var held=V(game.Observe().Players[animal].Position)-to;held.y=0;
    Assert.Less(held.magnitude,.6f,"묶인 동물은 걷지 못한다");Assert.AreEqual(100+(int)AbilityKind.Lasso,game.AbilityPropKey(animal),"올가미가 보인다");
   } finally {Object.Destroy(root);}yield return null;
  }
  [UnityTest,Timeout(300000)] public IEnumerator BotsPressQOnBothSidesWithoutErrors() {
   var root=new GameObject("ability bot world");
   try {
    var game=World(root,11,true,v=>{v.RandomCharacters=true;v.RoundSeconds=90;v.ResultSeconds=1;});yield return null;
    var used=new Dictionary<AbilityKind,int>();int lastId=0,animalUses=0,humanUses=0,hits=0,steps=0,rounds=0,round=game.Observe().Round;
    while(rounds<3&&steps<30000) {
     game.Step(.02f);var state=game.Observe();if(++steps%25==0)yield return null;
     if(state.Round!=round){round=state.Round;rounds++;}
     foreach(var e in state.Events.Where(e=>e.Id>lastId)) {
      lastId=e.Id;
      if(e.Kind==FeelEventKind.AbilityHit)hits++;
      if(e.Kind!=FeelEventKind.Ability)continue;
      var kind=(AbilityKind)(int)e.Strength;used[kind]=used.TryGetValue(kind,out int n)?n+1:1;
      if(state.Players[e.Actor].Faction==Faction.Animal)animalUses++;else humanUses++;
     }
    }
    TestContext.WriteLine($"animal {animalUses} human {humanUses} hits {hits}: "+string.Join(", ",used.OrderBy(p=>p.Key).Select(p=>p.Key+"×"+p.Value)));
    Assert.Greater(animalUses,3,"동물 봇도 능력을 쓴다");Assert.Greater(humanUses,3,"사람 봇도 능력을 쓴다");Assert.GreaterOrEqual(used.Count,8,"여러 능력이 실제로 쓰인다");Assert.Greater(hits,0,"능력이 누군가에게 닿는다");
   } finally {Object.Destroy(root);}yield return null;
  }
 }
}
