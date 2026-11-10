using System.IO;
using System.Linq;
using UnityEngine;
namespace AvH.Editor {
 // Render the actual owned prefabs for scale/layout inspection. This is not an interactive gameplay gate.
 public static class VisualAudit {
  static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/VisualAudit"));
  public static void GenerateAndCapture() {ConfigureOwnedAssets.Configure();Capture();}
  public static void BuildMac() {GenerateAndCapture();BuildPlaytest.Mac();}
  public static void Capture() {
   Directory.CreateDirectory(Output);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var root=new GameObject("Visual audit");Object.Instantiate(catalog.Village,root.transform);
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.73f,.8f);camera.farClipPlane=200;
   TownLighting.Apply(camera,light);
   foreach(var p in catalog.ShelterPoints)Pose(catalog.Human,root.transform,p);
   Pose(catalog.Animal,root.transform,TownLayout.Roof+new Vector3(1,0,-1));
   Shot(camera,new Vector3(66,59,-72),Vector3.zero,"map");
   Shot(camera,TownLayout.Roof+new Vector3(10,5,-12),TownLayout.Roof+Vector3.up,"shelter");
   Shot(camera,new Vector3(4,2.8f,-12),new Vector3(-3,2,13),"market-street");
   Shot(camera,TownLayout.Workshop+new Vector3(-10,3.2f,12),TownLayout.Workshop+Vector3.up,"workshop");
   Shot(camera,TownLayout.Courtyard+new Vector3(-9,3.2f,-11),TownLayout.Courtyard+Vector3.up,"courtyard");
   Physics.SyncTransforms();
   var surface=Physics.RaycastAll(new Vector3(-7,12,13),Vector3.down,20).OrderBy(h=>h.distance).First();
   Pose(catalog.Human,root.transform,surface.point);
   Shot(camera,new Vector3(0,7,7),surface.point+Vector3.up*.6f,"prop-contact");
   var effects=root.AddComponent<PrimitiveEffects>();effects.Initialize();
   foreach(var p in new[]{new Vector3(-.4f,1.4f,-4),new Vector3(.8f,1.7f,-1.5f),new Vector3(.25f,1.55f,1)}){var bubble=effects.Bubble(root.transform,.35f);bubble.transform.position=p;}
   effects.Emit(new Vector3(0,1.4f,3),new Color(.66f,.92f,1,.9f),12,1.9f);effects.Advance(.12f);
   Shot(camera,new Vector3(2,2.6f,-7),new Vector3(0,1.6f,1),"bubble-effects");root.SetActive(false);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.025f,0);floor.transform.localScale=new Vector3(12,.05f,12);
   foreach(var character in catalog.Humans.Concat(catalog.Animals)) {
    var prefab=character.Prefab;
    var model=Pose(prefab,null,Vector3.zero);
    foreach(var r in model.GetComponentsInChildren<Renderer>())Debug.Log("AUDIT "+prefab.name+" bounds "+r.bounds+" scale "+r.transform.lossyScale);
    if(character.Id.StartsWith("human-")){var gun=effects.Gun(model.transform);gun.GetComponent<BubbleGunPose>().Bind(model.transform);gun.GetComponent<BubbleGunPose>().SetAim(0,-10);gun.GetComponent<BubbleGunPose>().ApplyPose();}
    Shot(camera,new Vector3(3,2,4),Vector3.up*.7f,character.Id);Object.DestroyImmediate(model);
   }
   Object.DestroyImmediate(floor);Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  /// <summary>The sand crossing as the game builds it (dressed when the owned assets are configured), from player-height viewpoints.</summary>
  public static void CaptureCrossing() {
   Directory.CreateDirectory(Output);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var root=new GameObject("Crossing audit");CrossingMap.Build(root.transform);
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.fieldOfView=60;
   TownLighting.Apply(camera,light);
   if(catalog!=null){foreach(var p in CrossingMap.Shelters)Pose(catalog.Human,root.transform,p);Pose(catalog.Animal,root.transform,new Vector3(-46,0,-6));Pose(catalog.Human,root.transform,new Vector3(1,0,-27));}
   Shot(camera,new Vector3(0,62,-78),new Vector3(0,0,4),"crossing-overview");
   Shot(camera,new Vector3(2,2.6f,-31),new Vector3(-2,2,-10),"crossing-plaza");
   Shot(camera,new Vector3(-46,2.6f,-24),new Vector3(-45,2.5f,20),"crossing-long-lane");
   Shot(camera,new Vector3(2,2.6f,-21),new Vector3(4,2,-2),"crossing-alley");
   Shot(camera,new Vector3(40,2.6f,-22),new Vector3(40,2,10),"crossing-passage");
   Shot(camera,new Vector3(0,3,21.5f),new Vector3(-20,3.5f,32),"crossing-courtyard");
   Shot(camera,new Vector3(-38,6.2f,27),new Vector3(10,3.5f,31.5f),"crossing-bridge");
   Shot(camera,new Vector3(-44,2.4f,10),new Vector3(-44,4,26),"crossing-stair-a");
   Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  /// <summary>The planned maps (sky islands, snowy village, forest ruins) as the game builds them.</summary>
  public static void CapturePlanned() {
   Directory.CreateDirectory(Output);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.fieldOfView=60;TownLighting.Apply(camera,light);
   foreach(var (map,name,views) in new[]{
    (GameMaps.Sky,"sky",new[]{(new Vector3(0,45,-75),new Vector3(0,-4,4)),(new Vector3(0,2.6f,-38),new Vector3(0,2,-5)),(new Vector3(-28,2.4f,-2),new Vector3(-42,3,8)),(new Vector3(6,5.8f,-4),new Vector3(-36,0,38))}),
    (GameMaps.Snow,"snow",new[]{(new Vector3(0,34,-62),new Vector3(0,3,8)),(new Vector3(2,2.6f,-31),new Vector3(0,4,-6)),(new Vector3(-12,5.6f,-6),new Vector3(0,9,26)),(new Vector3(0,11.5f,24),new Vector3(0,2,-20))}),
    (GameMaps.Ruins,"ruins",new[]{(new Vector3(0,40,-62),new Vector3(0,0,6)),(new Vector3(2,2.6f,-26),new Vector3(0,1,0)),(new Vector3(-8,2.4f,2),new Vector3(0,4,22)),(new Vector3(-24,3,-14),new Vector3(-24,-3,-5))})}) {
    var root=new GameObject("Planned audit");PlannedMap.Build(root.transform,MapPlans.For(map),catalog!=null?catalog.Dressing(map):null,catalog!=null?catalog.Crate:null);
    if(catalog!=null){foreach(var p in MapPlans.For(map).Shelters)Pose(catalog.Human,root.transform,p);var start=GameMaps.AnimalStart(map,0);Pose(catalog.Animal,root.transform,new Vector3(start.X,0,start.Z));}
    for(int i=0;i<views.Length;i++)Shot(camera,views[i].Item1,views[i].Item2,$"{name}-{i}");
    Object.DestroyImmediate(root);
   }
   Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  /// <summary>The village as the game builds it, from player-height viewpoints.</summary>
  public static void CaptureTown() {
   Directory.CreateDirectory(Output);
   var root=new GameObject("Town audit");PrototypeVillage.Build(root.transform);
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.fieldOfView=60;
   TownLighting.Apply(camera,light);
   Shot(camera,new Vector3(0,48,-62),new Vector3(0,0,2),"town-overview");
   Shot(camera,new Vector3(3,2.6f,-24),new Vector3(0,2,0),"town-market");
   Shot(camera,new Vector3(-30,2.6f,-12),new Vector3(-30,2,20),"town-west");
   Shot(camera,new Vector3(30,2.6f,-26),new Vector3(33,2,20),"town-east");
   Shot(camera,new Vector3(-20,2.6f,14),new Vector3(30,2,14),"town-north");
   Shot(camera,new Vector3(46,22,46),new Vector3(0,0,0),"town-edge");
   Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  /// <summary>The warehouse as the game builds it, from player-height viewpoints.</summary>
  public static void CaptureWarehouse() {
   Directory.CreateDirectory(Output);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var root=new GameObject("Warehouse audit");WarehouseMap.Build(root.transform);
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.fieldOfView=60;
   TownLighting.Apply(camera,light);
   if(catalog!=null){foreach(var p in WarehouseMap.Shelters)Pose(catalog.Human,root.transform,p);Pose(catalog.Animal,root.transform,WarehouseLayout.StairFoot);Pose(catalog.Human,root.transform,new Vector3(-1,0,-6));}
   Shot(camera,new Vector3(0,40,-58),new Vector3(0,0,0),"warehouse-overview");
   Shot(camera,new Vector3(2,2.6f,-11),new Vector3(-4,3,8),"warehouse-hall");
   Shot(camera,new Vector3(-10,2.4f,-6),WarehouseLayout.StairHead,"warehouse-stair");
   Shot(camera,new Vector3(-12,5.6f,9),new Vector3(14,3.5f,7),"warehouse-deck");
   Shot(camera,new Vector3(4,2.6f,-22),new Vector3(-2,3,-14),"warehouse-yard");
   Shot(camera,new Vector3(10,2.4f,-3),WarehouseLayout.RampHead,"warehouse-ramp");
   Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  /// <summary>
  /// Every map as the game shows it at High quality (lighting, sky, post-processing), from one street view and one
  /// wide view each, with a human and an animal in front of the camera. -avhLookPrefix names the files (before/after).
  /// </summary>
  public static void CaptureLooks() {
   Directory.CreateDirectory(Output);
   var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,"-avhLookPrefix");string prefix=at>=0&&at+1<args.Length?args[at+1]:"look";
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   foreach(var (map,name,views) in new[]{
    (GameMaps.Town,"town",new[]{(new Vector3(3,2.6f,-24),new Vector3(0,2,0)),(new Vector3(0,30,-52),new Vector3(0,0,2))}),
    (GameMaps.Warehouse,"warehouse",new[]{(new Vector3(2,2.6f,-11),new Vector3(-4,3,8)),(new Vector3(4,2.6f,-22),new Vector3(-2,3,-14))}),
    (GameMaps.Crossing,"crossing",new[]{(new Vector3(2,2.6f,-31),new Vector3(-2,2,-10)),(new Vector3(-38,6.2f,27),new Vector3(10,3.5f,31.5f))}),
    (GameMaps.Sky,"sky",new[]{(new Vector3(0,2.6f,-38),new Vector3(0,2,-5)),(new Vector3(6,5.8f,-4),new Vector3(-36,0,38))}),
    (GameMaps.Snow,"snow",new[]{(new Vector3(2,2.6f,-31),new Vector3(0,4,-6)),(new Vector3(0,11.5f,24),new Vector3(0,2,-20))}),
    (GameMaps.Ruins,"ruins",new[]{(new Vector3(2,2.6f,-26),new Vector3(0,1,0)),(new Vector3(-8,2.4f,2),new Vector3(0,4,22))})}) {
    var root=new GameObject("Look audit");
    if(map==GameMaps.Town)PrototypeVillage.Build(root.transform);else if(map==GameMaps.Warehouse)WarehouseMap.Build(root.transform);else if(map==GameMaps.Crossing)CrossingMap.Build(root.transform);
    else PlannedMap.Build(root.transform,MapPlans.For(map),catalog!=null?catalog.Dressing(map):null,catalog!=null?catalog.Crate:null);
    var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;
    var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.fieldOfView=60;
    TownLighting.Apply(camera,light,map);DisplayQuality.Apply(GraphicsQuality.High,camera);LowPolyLook.Scenery(root.transform);
    Physics.SyncTransforms();
    if(catalog!=null) {
     // Two bodies a few steps into the street view, on whatever floor is there.
     var forward=views[0].Item2-views[0].Item1;forward.y=0;forward.Normalize();var side=Vector3.Cross(Vector3.up,forward);
     foreach(var (prefab,offset) in new[]{(catalog.Human,forward*5.5f-side*1.1f),(catalog.Animal,forward*7+side*1.4f)}) {
      var above=views[0].Item1+offset+Vector3.up*3;
      if(Physics.Raycast(above,Vector3.down,out var hit,12,Layers.World,QueryTriggerInteraction.Ignore)){var body=Pose(prefab,root.transform,hit.point);LowPolyLook.Character(body.transform);body.transform.rotation=Quaternion.LookRotation(-forward+side*.4f);}
     }
    }
    for(int i=0;i<views.Length;i++){camera.transform.position=views[i].Item1;MapAir.Apply(camera,TownLighting.Current,GraphicsQuality.High);MapAir.Current.Simulate(9,true,true);Shot(camera,views[i].Item1,views[i].Item2,$"{prefix}-{name}-{i}");}
    MapAir.Apply(null,null,GraphicsQuality.Low);
    Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
   }
  }
  static GameObject Pose(GameObject prefab,Transform parent,Vector3 position) {
   var model=Object.Instantiate(prefab,parent);model.transform.position=position;
   foreach(var a in model.GetComponentsInChildren<Animator>()){a.Rebind();a.Update(0);a.Play("Idle",0,0);a.Update(.1f);}return model;
  }
  static void Shot(Camera camera,Vector3 eye,Vector3 target,string name) {
   camera.transform.position=eye;camera.transform.LookAt(target);var rt=new RenderTexture(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
  }
 }
}
