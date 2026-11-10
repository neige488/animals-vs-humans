using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>The workshop tools: random draw, glove launch, cushion sweep, disc bounce and blower gusts.</summary>
 public class ToolTests {
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static string Settings(int tool) {
   var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   using(var f=System.IO.File.Create(path))new System.Xml.Serialization.XmlSerializer(typeof(PlaytestValues)).Serialize(f,new PlaytestValues{Tool=tool});
   return path;
  }
  static UnityPlaytestSession World(GameObject root,int tool,int map=GameMaps.Town) {
   var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   game.StartSolo("도구",11,Settings(tool),map);return game;
  }
  static void WalkTo(UnityPlaytestSession game,Vector3 to,int steps=300) {
   for(int i=0;i<steps;i++){var p=V(game.Observe().Players[0].Position);var d=to-p;d.y=0;if(d.magnitude<.1f)break;d.Normalize();game.SubmitInput(0,new PlayerInput{Right=d.x,Forward=d.z});game.Step(.02f);}
   game.SubmitInput(0,new PlayerInput());for(int i=0;i<15;i++)game.Step(.02f);
  }
  static void Use(UnityPlaytestSession game,float yaw,float pitch=0){game.SubmitInput(0,new PlayerInput{Attack=true,Yaw=yaw,Pitch=pitch});game.Step(.02f);game.SubmitInput(0,new PlayerInput{Yaw=yaw});}

  [Test] public void EveryHumanDrawsAToolEachRoundOrTheHostFixesOne() {
   var random=new PlaytestSession(3,Settings(Tools.Random));random.StartSolo("a");
   var drawn=random.Observe().Players.Select(p=>p.Tool).Distinct().Count();
   Assert.GreaterOrEqual(drawn,3,"twelve random draws mix the tools");
   var fixedGlove=new PlaytestSession(3,Settings((int)ToolKind.Glove));fixedGlove.StartSolo("b");
   Assert.IsTrue(fixedGlove.Observe().Players.All(p=>p.Tool==ToolKind.Glove));
   // The glove never needs reloading; its use is slower than a cork shot.
   var r=fixedGlove.Observe().Round;Assert.IsTrue(fixedGlove.TryFire(0,r));Assert.AreEqual(fixedGlove.ObserveSettings().Current.Magazine,fixedGlove.Observe().Players[0].Ammo);
   Assert.Greater(fixedGlove.Observe().Players[0].FireCooldownRemaining,fixedGlove.ObserveSettings().Current.FireInterval*2);
   Assert.IsFalse(fixedGlove.TryReload(0,r));
  }

  [UnityTest] public IEnumerator GloveLaunchesTheNearestFriendFarWithoutStun() {
   var root=new GameObject("glove");
   try {
    var game=World(root,(int)ToolKind.Glove);yield return null;
    // Slot 4 stands at (-4.5,0) with open ground to its west. Step up from the east and punch west.
    var target=V(game.Observe().Players[4].Position);WalkTo(game,target+new Vector3(1.3f,0,0));
    var before=V(game.Observe().Players[4].Position);float highest=before.y;
    Use(game,270);
    FeelEvent shove=null;
    for(int i=0;i<90;i++){game.Step(.02f);highest=Mathf.Max(highest,game.Observe().Players[4].Position.Y);shove=shove??game.Observe().Events.FirstOrDefault(e=>e.Kind==FeelEventKind.Shoved);}
    var after=game.Observe().Players[4];
    Assert.Greater(before.x-after.Position.X,5,"a punch sends a friend flying, not a nudge: "+V(after.Position));
    Assert.Greater(highest-before.y,.6f,"the glove lifts the friend off the ground");
    Assert.AreEqual(Faction.Human,after.Faction);Assert.AreEqual(ActionPhase.None,after.Action);
    Assert.IsNotNull(shove);Assert.AreEqual(0,shove.Actor);Assert.AreEqual(4,shove.Target);
    Assert.Less(Vector3.Distance(V(game.Observe().Players[8].Position),new Vector3(-4.5f,0,3)),.5f,"only the nearest one is punched");
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator CushionSweepsEveryoneInFront() {
   var root=new GameObject("cushion");
   try {
    var game=World(root,(int)ToolKind.Cushion);yield return null;
    // Between slot 4 (-4.5,0) and slot 5 (-1.5,0), facing north: both are inside the wide cone.
    WalkTo(game,new Vector3(-3,0,-1.4f));
    var a=V(game.Observe().Players[4].Position);var b=V(game.Observe().Players[5].Position);
    Use(game,0);for(int i=0;i<40;i++)game.Step(.02f);
    Assert.Greater(Vector3.Distance(V(game.Observe().Players[4].Position),a),1,"left friend pushed");
    Assert.Greater(Vector3.Distance(V(game.Observe().Players[5].Position),b),1,"right friend pushed");
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator DiscBouncesOffAWallAndComesBack() {
   var root=new GameObject("disc");
   try {
    var game=World(root,(int)ToolKind.Disc,GameMaps.Warehouse);yield return null;
    for(int i=0;i<10;i++)game.Step(.02f);
    // Slot 0 stands at z -6, the south wall is at z -14: shoot at it.
    Use(game,180,-3);bool cameBack=false;int id=-1;
    for(int i=0;i<150&&!cameBack;i++){game.Step(.02f);var d=game.ObserveBubbles();if(d.Length==0)continue;Assert.AreEqual(ToolKind.Disc,d[0].Kind);id=d[0].Id;cameBack=d[0].Direction.Z>0;}
    Assert.IsTrue(cameBack,"the disc bounced and flies back north");
    Assert.IsTrue(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Disc"||t.name=="Bubble"));
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator BlowerPushesGentlyAndReportsAtMostOncePerMoment() {
   var root=new GameObject("blower");
   try {
    var game=World(root,(int)ToolKind.Blower);yield return null;
    var before=V(game.Observe().Players[4].Position);int shoves=0,lastEvent=0;
    for(int i=0;i<50;i++){game.SubmitInput(0,new PlayerInput{Attack=true,Yaw=0});game.Step(.02f);
     foreach(var e in game.Observe().Events.Where(e=>e.Id>lastEvent)){if(e.Kind==FeelEventKind.Shoved&&e.Target==4)shoves++;lastEvent=Mathf.Max(lastEvent,e.Id);}}
    game.SubmitInput(0,new PlayerInput());for(int i=0;i<30;i++)game.Step(.02f);
    float moved=V(game.Observe().Players[4].Position).z-before.z;
    Assert.Greater(moved,.4f,"a second of gusts moves a friend");
    Assert.LessOrEqual(shoves,2,"gusts are reported at most once per 0.8 s");Assert.GreaterOrEqual(shoves,1);
    Assert.Less(game.Observe().Players[0].Ammo,game.Session.ObserveSettings().Current.Magazine,"the blower drains its tank");
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
