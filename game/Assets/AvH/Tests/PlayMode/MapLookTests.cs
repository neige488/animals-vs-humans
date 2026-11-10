using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>Each map has its own time of day, scenery and bodies wear the low-poly shader, and the air carries the map's specks.</summary>
 public class MapLookTests {
  [TearDown] public void Cleanup(){MapAir.Apply(null,null,GraphicsQuality.Low);DisplayQuality.Apply(GraphicsQuality.High);}

  [UnityTest] public IEnumerator EveryMapHasItsOwnSunSkyAndGrade() {
   var root=new GameObject("look");var camera=root.AddComponent<Camera>();var sun=new GameObject("sun").AddComponent<Light>();sun.transform.SetParent(root.transform);
   try {
    var looks=Enumerable.Range(0,GameMaps.Count).Select(MapLook.For).ToArray();
    for(int a=0;a<looks.Length;a++)for(int b=a+1;b<looks.Length;b++) {
     Assert.Greater(Vector3.Angle(looks[a].ToSun,looks[b].ToSun),4,$"maps {a} and {b} have the sun in different places");
     Assert.AreNotEqual(looks[a].Zenith,looks[b].Zenith,$"maps {a} and {b} have different skies");
    }
    foreach(var look in looks){Assert.Greater(look.ToSun.y,.2f,"the sun stays above the rooftops");Assert.Greater(look.AmbientEquator.grayscale,.5f,"shade stays readable");}
    TownLighting.Apply(camera,sun,GameMaps.Ruins);yield return null;
    var ruins=MapLook.For(GameMaps.Ruins);
    Assert.AreEqual(GameMaps.Ruins,TownLighting.CurrentMap);Assert.AreEqual(ruins.Sun,sun.color);Assert.AreEqual(ruins.Horizon,RenderSettings.fogColor);
    Assert.Less(Vector3.Angle(-sun.transform.forward,ruins.ToSun),.1f,"the light shines from where the sky shows the sun");
    Assert.Less(Vector3.Angle((Vector3)RenderSettings.skybox.GetVector("_SunDir"),ruins.ToSun),.1f);
    Assert.IsTrue(RenderSettings.skybox.shader.isSupported,"the sky shader compiles for this target");
    TownLighting.Apply(camera,sun);Assert.AreEqual(GameMaps.Town,TownLighting.CurrentMap);
   } finally {Object.Destroy(root);}
  }

  [UnityTest] public IEnumerator SceneryAndBodiesWearTheLowPolyShader() {
   var root=new GameObject("low poly");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
    game.StartSolo("look",5,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"),GameMaps.Crossing);yield return null;
    var shader=Resources.Load<Shader>("LowPoly");Assert.IsNotNull(shader);Assert.IsTrue(shader.isSupported,"the low-poly shader compiles for this target");
    var materials=root.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
    Assert.IsFalse(materials.Any(m=>m.shader.name.StartsWith("Standard")&&m.renderQueue<2450),"no opaque Standard material is left: "+string.Join(", ",materials.Where(m=>m.shader.name.StartsWith("Standard")&&m.renderQueue<2450).Select(m=>m.name).Take(5)));
    Assert.Greater(materials.Count(m=>m.shader==shader),3);
    var worn=game.PlayerTransform(0).Find("Visual").GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).ToArray();
    var body=worn.FirstOrDefault(m=>m.shader==shader);Assert.IsNotNull(body,"the body wears the low-poly shader, not "+string.Join(", ",worn.Select(m=>m.shader.name)));
    Assert.AreEqual(LowPolyLook.CharacterRim,body.GetFloat("_Rim"),1e-4f,"bodies get the strong rim so they read against walls");
    // One twin per source material, shared by everything that uses it; see-through materials are left alone.
    var plain=new Material(Shader.Find("Standard"));var glass=new Material(Shader.Find("Standard")){renderQueue=3000};
    Assert.AreSame(LowPolyLook.Of(plain,false),LowPolyLook.Of(plain,false));Assert.AreNotSame(LowPolyLook.Of(plain,false),LowPolyLook.Of(plain,true));Assert.AreSame(glass,LowPolyLook.Of(glass,false));
    Object.Destroy(plain);Object.Destroy(glass);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator TheAirFollowsTheMapAndTurnsOffAtLowQuality() {
   var root=new GameObject("air");var camera=root.AddComponent<Camera>();
   try {
    MapAir.Apply(camera,MapLook.For(GameMaps.Snow),GraphicsQuality.High);yield return null;
    Assert.IsNotNull(MapAir.Current);float snow=MapAir.Current.emission.rateOverTime.constant;Assert.Less(MapAir.Current.velocityOverLifetime.y.constantMax,0,"snow falls");
    MapAir.Apply(camera,MapLook.For(GameMaps.Town),GraphicsQuality.High);yield return null;
    Assert.Less(MapAir.Current.emission.rateOverTime.constant,snow,"dust is sparser than snowfall");
    Assert.AreEqual(1,camera.GetComponentsInChildren<ParticleSystem>().Length,"only one air system at a time");
    MapAir.Apply(camera,MapLook.For(GameMaps.Town),GraphicsQuality.Low);yield return null;Assert.IsTrue(MapAir.Current==null,"no air specks at low quality");
   } finally {Object.Destroy(root);}
  }
 }
}
