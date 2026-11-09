using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace AvH.Editor {
 /// <summary>
 /// The sand crossing dressed with the owned Synty POLYGON Adventure kit: a stone fortress town. Visual only: every
 /// collider stays with <see cref="CrossingMap"/>, and the dressing follows <see cref="CrossingLayout"/> so walls,
 /// parapets, stairs and the bridge sit exactly on them. Static parts are merged per material into a few chunk
 /// meshes; trees, bushes, banners and lanterns stay separate so <see cref="TownDressing"/> can sway and light them.
 /// </summary>
 public static partial class ConfigureOwnedAssets {
  const string Generic="Synty/PolygonGeneric/Prefabs/";
  static readonly Color RoofClay=new Color(.62f,.33f,.22f),Sandstone=new Color(.78f,.67f,.5f),Mortar=new Color(.56f,.5f,.4f);
  /// <summary>
  /// Synty materials without an albedo map (glass, distant mountains) whose shader graph is not imported render magenta;
  /// they become a plain Standard colour. Glass turns an opaque dark tint so windows read as windows.
  /// </summary>
  static void ConvertFlat(string file,Material mat,string text) {
   if(mat.shader!=null&&mat.shader.isSupported&&mat.shader.name!="Hidden/InternalErrorShader")return;
   var match=System.Text.RegularExpressions.Regex.Match(text,@"- _Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)");if(!match.Success)return;
   var color=new Color(float.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),float.Parse(match.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture),float.Parse(match.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture));
   if(file.Contains("Glass"))color=Color.Lerp(color,new Color(.12f,.17f,.2f),.7f);
   var path=Generated+System.IO.Path.GetFileNameWithoutExtension(file)+"_Builtin.mat";var converted=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(converted==null){converted=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(converted,path);}
   converted.color=color;converted.SetFloat("_Glossiness",file.Contains("Glass")?.75f:.05f);EditorUtility.SetDirty(converted);Converted[mat]=converted;
  }
  // Wall styles: four plaster tints for the town blocks, brick for the town wall and the sites.
  static readonly Color[] PlasterTints={Color.white,new Color(1,.87f,.68f),new Color(1,.83f,.78f),new Color(.9f,.9f,.86f)};
  const int Brick=4,Timber=5;
  static Material Styled(Material source,int style) {
   if(style==Brick){var brick=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Generic_Brick_Builtin.mat");return brick??source;}
   if(style==Timber){var wood=AssetDatabase.LoadAssetAtPath<Material>(Generated+"Generic_Wood_Builtin.mat");return wood??source;}
   if(style==0)return source;
   var path=Generated+$"Crossing_Plaster_{style}.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(source);AssetDatabase.CreateAsset(mat,path);}
   mat.CopyPropertiesFromMaterial(source);mat.color=PlasterTints[style];EditorUtility.SetDirty(mat);return mat;
  }
  static void Restyle(GameObject go,int style) {
   foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&m.name=="Generic_Plaster_Builtin"?Styled(m,style):m).ToArray();
  }
  public static bool CrossingNeedsRebuild() {
   var catalog=AssetDatabase.LoadAssetAtPath<OwnedAssetCatalog>(Root+"Resources/OwnedAssetCatalog.asset");
   return catalog!=null&&(catalog.Crossing==null||catalog.Crate==null||catalog.Warehouse==null||catalog.Sky==null||catalog.Snow==null||catalog.Ruins==null);
  }
  /// <summary>Builds and saves the crossing dressing and the pushable crate model; returns (dressing, crate).</summary>
  static (GameObject dressing,GameObject crate) BuildCrossing() {
   var map=new GameObject("Crossing dressing");
   Ground(map);Facades(map);Perimeter(map);Sites(map);Climbs(map);BridgeDressing(map);Parapets(map);PassageRoof(map);Cover(map);Market(map);Greenery(map);Horizon(map);
   MergeStatic(map,"Crossing");
   var dressing=PrefabUtility.SaveAsPrefabAsset(map,Generated+"Crossing.prefab");Object.DestroyImmediate(map);
   var holder=new GameObject("Crate");var crate=Put(holder,Adventure+"Props/SM_Prop_Crate_01.prefab",Vector3.zero,0,1/.77f);crate.name="Crate model";
   var crateAsset=PrefabUtility.SaveAsPrefabAsset(holder,Generated+"CrossingCrate.prefab");Object.DestroyImmediate(holder);
   return (dressing,crateAsset);
  }

  // ---------- placement ----------
  static GameObject Spawn(GameObject parent,string path) {
   var go=(GameObject)PrefabUtility.InstantiatePrefab(Load(path));PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   go.transform.SetParent(parent.transform,false);Clean(go);foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);return go;
  }
  /// <summary>Uniform scale, bottom centre on <paramref name="floor"/>.</summary>
  static GameObject Put(GameObject parent,string path,Vector3 floor,float yaw,float scale) {
   var go=Spawn(parent,path);go.transform.localScale=Vector3.one*scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.position=Vector3.zero;
   var b=Bounds(go);go.transform.position=floor-new Vector3(b.center.x,b.min.y,b.center.z);return go;
  }
  /// <summary>
  /// Stretches a piece so its unrotated bounds fill <paramref name="size"/> (a flat axis keeps scale 1), then turns it
  /// (yaw, then a tilt about its own x) and centres it on <paramref name="center"/>. The POLYGON palette textures are
  /// flat colour swatches, so stretching does not smear detail.
  /// </summary>
  static GameObject Fit(GameObject parent,string path,Vector3 center,Vector3 size,float yaw,float tilt=0) {
   var go=Spawn(parent,path);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;
   var b=Bounds(go);
   var scale=new Vector3(b.size.x>.01f?size.x/b.size.x:1,b.size.y>.01f?size.y/b.size.y:1,b.size.z>.01f?size.z/b.size.z:1);
   var rotation=Quaternion.Euler(0,yaw,0)*Quaternion.Euler(tilt,0,0);
   go.transform.localScale=scale;go.transform.rotation=rotation;
   go.transform.position=center-rotation*Vector3.Scale(b.center,scale);
   return go;
  }
  static string Base(string piece)=>Generic+"Base/SM_Bld_Base_"+piece+".prefab";
  static string Env(string piece)=>Adventure+"Environments/SM_Env_"+piece+".prefab";
  static string Prop(string piece)=>Adventure+"Props/SM_Prop_"+piece+".prefab";
  static Vector3 V(Vector2 p,float y)=>new Vector3(p.x,y,p.y);

  // ---------- ground ----------
  static void Ground(GameObject map) {
   var root=Group(map,"Ground");
   Surface(root,"Packed sand",new Vector3(0,.004f,0),new Vector3(CrossingLayout.HalfX*2,.008f,CrossingLayout.HalfZ*2),new Color(.71f,.62f,.45f));
   // Laid stone slabs in the plaza and the alley bend; a grassy garden courtyard behind the lanes.
   Paving(root,Rect.MinMaxRect(-CrossingLayout.HalfX,-CrossingLayout.HalfZ,CrossingLayout.HalfX,-20),0,"Plaza paving");
   Paving(root,Rect.MinMaxRect(0,-4,16,4),0,"Bend paving");
   int n=0;
   foreach(var r in new[]{Rect.MinMaxRect(-CrossingLayout.HalfX,20,CrossingLayout.HalfX,CrossingLayout.HalfZ)})for(float x=r.xMin;x<r.xMax-.1f;x+=4)for(float z=r.yMin;z<r.yMax-.1f;z+=4) {
    var c=new Vector2(x+2,z+2);if(Covered(c))continue;
    Fit(root,Env("FloorTile_0"+(n++%3+1)),new Vector3(c.x,.015f,c.y),new Vector3(4,0,4),(n*90)%360);
   }
   // Dirt roads down the lanes.
   foreach(var lane in new[]{CrossingLayout.LongLane,CrossingLayout.Passage,Rect.MinMaxRect(0,-20,4,-4),Rect.MinMaxRect(12,4,16,20)})
    for(float z=lane.yMin;z<lane.yMax-.1f;z+=6)Fit(root,Env("Road_Straight_01"),new Vector3(lane.center.x,.03f,Mathf.Min(z+3,lane.yMax-3)),new Vector3(lane.width-.6f,.04f,6),0);
  }
  // Running-bond stone slabs in two shades over a darker grout bed; one mesh per shade.
  static void Paving(GameObject root,Rect r,float y,string name)=>Paving(root,r,y,name,1.1f,.8f,.07f,new Color(.42f,.38f,.3f),new Color(.74f,.68f,.55f),new Color(.66f,.6f,.48f));
  /// <summary>Running-bond pieces W x D with a gap over a grout bed (slabs, or long floor boards).</summary>
  static void Paving(GameObject root,Rect r,float y,string name,float W,float D,float Gap,Color grout,params Color[] shades) {
   CrossSurface(root,name+" grout",new Vector3(r.center.x,y+.006f,r.center.y),new Vector3(r.width,.01f,r.height),grout);
   for(int shade=0;shade<2;shade++) {
    var v=new List<Vector3>();var t=new List<int>();int row=0;
    for(float z=r.yMin;z<r.yMax-.05f;z+=D,row++)for(float x=r.xMin-(row%2)*W*.5f;x<r.xMax-.05f;x+=W) {
     int k=Mathf.FloorToInt(x*3.1f+z*7.3f);if(((k%3)==0?1:0)!=shade)continue;
     float x0=Mathf.Max(x,r.xMin)+Gap/2,x1=Mathf.Min(x+W,r.xMax)-Gap/2,z0=z+Gap/2,z1=Mathf.Min(z+D,r.yMax)-Gap/2;if(x1-x0<.1f||z1-z0<.1f)continue;
     float top=y+.02f+(k&1)*.004f;int n=v.Count;v.AddRange(new[]{new Vector3(x0,top,z0),new Vector3(x0,top,z1),new Vector3(x1,top,z1),new Vector3(x1,top,z0)});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
    var mesh=new Mesh{name=name+" "+shade,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
    var go=new GameObject(mesh.name);go.transform.SetParent(root.transform);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=VillageMaterial("Slab_"+ColorUtility.ToHtmlStringRGB(shades[shade]),shades[shade]);
   }
  }
  static bool Covered(Vector2 c) {
   foreach(var b in CrossingLayout.Blocks)if(b.Contains(c))return true;
   return CrossingLayout.SiteA.Contains(c)||CrossingLayout.SiteB.Contains(c)||CrossingLayout.Tower.Contains(c);
  }

  // ---------- town blocks: two storeys of stone and plaster, doors at street level ----------
  static void Facades(GameObject map) {
   var root=Group(map,"Facades");int index=0,block=0;
   foreach(var r in CrossingLayout.Blocks) {
    int side=0;
    foreach(var (a,b,normal) in Sides(r)) {
     if(AgainstWall(a,b))continue;
     Facade(root,a,b,normal,CrossingLayout.BlockHeight,ref index,true,(block+side++)%4);
    }
    block++;
    foreach(var corner in new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax)})
     if(!AgainstWall(corner,corner))Fit(root,Base("Pillar_01"),V(corner,CrossingLayout.BlockHeight/2),new Vector3(.55f,CrossingLayout.BlockHeight+.1f,.55f),0);
    Surface(root,"Flat roof",new Vector3(r.center.x,CrossingLayout.BlockHeight-.02f,r.center.y),new Vector3(r.width-.1f,.04f,r.height-.1f),RoofClay);
   }
   // Watchtowers rise over the roofs: the town's skyline from every lane.
   foreach(var p in new[]{new Vector3(-36,CrossingLayout.BlockHeight,-16),new Vector3(8,CrossingLayout.BlockHeight,16),new Vector3(32,CrossingLayout.BlockHeight,-16),new Vector3(-36,CrossingLayout.BlockHeight,16),new Vector3(48,CrossingLayout.BlockHeight,0)})
    Put(root,Adventure+"Buildings/SM_Bld_Village_07.prefab",p,p.x*3,1);
   foreach(var p in new[]{new Vector3(-20,CrossingLayout.BlockHeight,-10),new Vector3(22,CrossingLayout.BlockHeight,6),new Vector3(-22,CrossingLayout.BlockHeight,12)})
    Put(root,Adventure+"Buildings/SM_Bld_Village_06.prefab",p,p.z*9,1);
  }
  static IEnumerable<(Vector2 a,Vector2 b,Vector2 normal)> Sides(Rect r) {
   yield return (new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),Vector2.down);
   yield return (new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),Vector2.up);
   yield return (new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin),Vector2.left);
   yield return (new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),Vector2.right);
  }
  static bool AgainstWall(Vector2 a,Vector2 b)=>Mathf.Abs(a.x)>=CrossingLayout.HalfX-.01f&&Mathf.Abs(b.x)>=CrossingLayout.HalfX-.01f||Mathf.Abs(a.y)>=CrossingLayout.HalfZ-.01f&&Mathf.Abs(b.y)>=CrossingLayout.HalfZ-.01f;
  // One face from a to b whose outside is along normal: wall pieces stand just inside the collider face so bodies never clip them.
  static void Facade(GameObject root,Vector2 a,Vector2 b,Vector2 normal,float height,ref int index,bool dress,int style=0) {
   float length=Vector2.Distance(a,b);int count=Mathf.Max(1,Mathf.RoundToInt(length/2.5f));float piece=length/count;
   var along=(b-a).normalized;float yaw=Mathf.Atan2(normal.x,normal.y)*Mathf.Rad2Deg;int storeys=Mathf.Max(1,Mathf.RoundToInt(height/3));float storey=height/storeys;
   for(int i=0;i<count;i++) {
    var mid=a+along*(piece*(i+.5f))-normal*.12f;
    for(int s=0;s<storeys;s++) {
     int k=index++;
     string kind=s==0?(k%5==0?"Wall_Door_01":k%3==0?"Wall_Window_01":"Wall_01"):(k%2==0?"Wall_Window_01":"Wall_01");
     Restyle(Fit(root,Base(kind),V(mid,storey*(s+.5f)),new Vector3(piece,storey,.24f),yaw),style);
     if(!dress)continue;
     var front=V(mid+normal*.14f,0);var face=Quaternion.Euler(0,yaw,0);
     if(s==0&&k%7==3)Fit(root,Generic+"Environment/SM_Gen_Env_Ivy_0"+(k%3+1)+".prefab",front+Vector3.up*1.4f,new Vector3(Mathf.Min(piece,2.2f),2.2f,.08f),yaw);
     if(s==storeys-1&&k%4==1)Fit(root,Generic+"Environment/SM_Gen_Env_Ivy_Draped_01.prefab",front+Vector3.up*(height-.6f),new Vector3(Mathf.Min(piece,2.4f),1.2f,.4f),yaw);
     if(s==1&&kind=="Wall_Window_01"&&k%3==0)Fit(root,Prop("Pot_0"+(k%2+2)),front+face*new Vector3(0,storey+.95f,.25f),new Vector3(.9f,.3f,.45f),yaw);
     if(s==0&&k%9==4){var lamp=Put(root,Generic+"Props/SM_Gen_Prop_Light_Wall_01.prefab",front+Vector3.up*2.35f,yaw,1.2f);lamp.name="SM_Item_Lantern_Wall";}
     if(s==0&&k%11==6)Fit(root,Adventure+"Buildings/SM_Bld_Village_HangingCloth_01.prefab",front+face*new Vector3(0,1.8f,.1f),new Vector3(.3f,2.2f,1.6f),yaw+90);
    }
   }
   // A timber band between storeys and a cornice along the top edge.
   if(dress&&storeys>1)Fit(root,Generic+"Building/SM_Gen_Bld_Beam_01.prefab",V((a+b)/2+normal*.02f,storey),new Vector3(.22f,.22f,length),yaw+90);
   Fit(root,Base("Wall_Trim_01"),V((a+b)/2-normal*.05f,height+.05f),new Vector3(length,.18f,.42f),yaw);
  }

  // ---------- the town wall: two storeys inside, battlements on top ----------
  static void Perimeter(GameObject map) {
   var root=Group(map,"Town wall");int index=0;float h=CrossingLayout.WallHeight,hx=CrossingLayout.HalfX,hz=CrossingLayout.HalfZ;
   foreach(var (a,b,normal) in new[]{(new Vector2(-hx,-hz),new Vector2(hx,-hz),Vector2.up),(new Vector2(hx,hz),new Vector2(-hx,hz),Vector2.down),(new Vector2(-hx,hz),new Vector2(-hx,-hz),Vector2.right),(new Vector2(hx,-hz),new Vector2(hx,hz),Vector2.left)}) {
    // The inner face is at the boundary; pieces stand just outside it, facing in.
    Facade(root,a,b,normal,h,ref index,false,Brick);
    float length=Vector2.Distance(a,b);var along=(b-a).normalized;
    for(float t=1;t<length;t+=2)Fit(root,Adventure+"Buildings/SM_Bld_Wall_02.prefab",V(a+along*t-normal*.3f,h+.6f),new Vector3(.7f,1.2f,.7f),0);
    for(float t=0;t<=length+.1f;t+=16)Fit(root,Base("Pillar_01"),V(a+along*t-normal*.25f,h/2),new Vector3(.9f,h+.3f,.9f),0);
   }
  }

  // ---------- raised sites: stone walls below, stone tiles on top, a banner each ----------
  static void Sites(GameObject map) {
   var root=Group(map,"Sites");int index=0;
   foreach(var (r,color) in new[]{(CrossingLayout.SiteA,new Color(.75f,.18f,.15f)),(CrossingLayout.SiteB,new Color(.17f,.35f,.72f)),(CrossingLayout.Tower,new Color(.9f,.7f,.18f))}) {
    foreach(var (a,b,normal) in Sides(r))if(!AgainstWall(a,b))Facade(root,a,b,normal,CrossingLayout.Upper,ref index,false,Brick);
    Paving(root,r,CrossingLayout.Upper,"Site paving");
    foreach(var corner in new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin)})Fit(root,Base("Pillar_01"),V(corner,CrossingLayout.Upper/2),new Vector3(.6f,CrossingLayout.Upper+.05f,.6f),0);
    // Banners at the back of each site name it from across the courtyard.
    foreach(float x in new[]{r.xMin+2.5f,r.xMax-2.5f})Banner(root,new Vector3(x,CrossingLayout.Upper+1.5f,r.yMax-.6f),color);
    Fit(root,Generic+"Props/SM_Gen_Prop_Sack_Stack_01.prefab",new Vector3(r.center.x+(r.width>10?6:2.6f),CrossingLayout.Upper+.45f,r.yMax-.9f),new Vector3(1.1f,.9f,1.1f),20);
   }
   Put(root,Generic+"Props/SM_Gen_Prop_Statue_01.prefab",new Vector3(0,CrossingLayout.Upper,34.6f),180,1.2f);
  }
  static void CrossBanner(GameObject root,Vector3 p,Color color) {
   Surface(root,"Banner pole",p,new Vector3(.09f,3,.09f),Wood);
   var flag=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(flag.GetComponent<Collider>());flag.name="District pennant";flag.transform.SetParent(root.transform);
   flag.transform.position=p+new Vector3(.48f,.8f,0);flag.transform.localScale=new Vector3(.85f,1.15f,.035f);flag.GetComponent<Renderer>().sharedMaterial=VillageMaterial("Pennant_"+ColorUtility.ToHtmlStringRGB(color),color);
  }

  // ---------- stairs and ramps on the colliders' own frames ----------
  static void Climbs(GameObject map) {
   var root=Group(map,"Climbs");
   foreach(var (centre,yaw) in new[]{(CrossingLayout.StairA,0f),(CrossingLayout.StairB,0f),(CrossingLayout.TowerStair,-90f)}) {
    var frame=Quaternion.Euler(0,yaw,0);float l=CrossingLayout.StairLength,w=CrossingLayout.StairWidth,run=l/21,rise=CrossingLayout.Upper/21;
    for(int i=0;i<21;i++){float top=(i+1)*rise;Fit(root,Prop("StoneBlock_01"),centre+frame*new Vector3(0,top/2,-l/2+run*(i+.5f)),new Vector3(w,top,run+.01f),yaw);}
   }
   foreach(var (centre,yaw) in new[]{(CrossingLayout.RampA,-90f),(CrossingLayout.RampB,90f)}) {
    var frame=Quaternion.Euler(0,yaw,0);float l=CrossingLayout.RampLength,w=CrossingLayout.RampWidth,h=CrossingLayout.Upper,angle=Mathf.Atan2(h,l)*Mathf.Rad2Deg;
    Fit(root,Env("Road_Straight_01"),centre+frame*new Vector3(0,h/2+.03f,0),new Vector3(w,.06f,Mathf.Sqrt(l*l+h*h)),yaw,-angle);
    for(int k=0;k<12;k++){float z0=-l/2+k,top=h*k/l;if(top<.05f)continue;Fit(root,Prop("StoneBlock_01"),centre+frame*new Vector3(0,top/2,z0+.5f),new Vector3(w-.04f,top,1),yaw);}
    for(int k=1;k<12;k+=2)Fit(root,Generic+"Building/SM_Gen_Bld_Beam_01.prefab",centre+frame*new Vector3(0,h*(k+.5f)/l+.06f,-l/2+k+.5f),new Vector3(w-.2f,.1f,.18f),yaw,-angle);
   }
  }

  // ---------- the bridge: plank deck on a stone slab, stone piers ----------
  static void BridgeDressing(GameObject map) {
   var root=Group(map,"Bridge");float y=CrossingLayout.Upper,z=CrossingLayout.BridgeZ,w=CrossingLayout.BridgeWidth;
   foreach(var (from,to) in new[]{(CrossingLayout.SiteA.xMax,CrossingLayout.Tower.xMin),(CrossingLayout.Tower.xMax,CrossingLayout.SiteB.xMin)}) {
    Fit(root,Prop("StoneBlock_01"),new Vector3((from+to)/2,y-.25f,z),new Vector3(to-from,.4f,w),0);
    for(float x=from+.25f;x<to;x+=.5f)Fit(root,Generic+"Building/SM_Gen_Bld_Beam_01.prefab",new Vector3(x,y-.02f,z),new Vector3(.46f,.07f,w-.1f),0);
    foreach(float side in new[]{-1f,1f})Fit(root,Generic+"Building/SM_Gen_Bld_Beam_01.prefab",new Vector3((from+to)/2,y-.35f,z+side*(w/2-.1f)),new Vector3(.3f,.3f,to-from),90);
   }
   foreach(var x in new[]{-20f,-10f,10f,20f}) {
    Fit(root,Base("Pillar_01"),new Vector3(x,1.55f,z),new Vector3(.95f,3.1f,.95f),0);
    var lantern=Put(root,Adventure+"Items/SM_Item_Lantern_02.prefab",new Vector3(x,y-.75f,z-w/2-.15f),0,1.3f);lantern.name="SM_Item_Lantern_Bridge";
   }
  }
  // Stone parapets on every collider segment the runtime builds.
  static void Parapets(GameObject map) {
   var root=Group(map,"Parapets");
   foreach(var (from,to,at,alongX) in CrossingMap.RailSegments()) {
    float mid=(from+to)/2,length=to-from;
    var c=alongX?new Vector3(mid,CrossingLayout.Upper+.5f,at):new Vector3(at,CrossingLayout.Upper+.5f,mid);
    Fit(root,Adventure+"Buildings/SM_Bld_Wall_01.prefab",c,new Vector3(length,1,.34f),alongX?0:90);
   }
  }
  // The east passage: timber beams under clay roof sections.
  static void PassageRoof(GameObject map) {
   var root=Group(map,"Passage roof");var r=CrossingLayout.RoofSpan;
   for(float z=r.yMin;z<r.yMax-.1f;z+=4)Fit(root,Adventure+"Buildings/SM_Bld_Village_Top_01.prefab",new Vector3(r.center.x,3.55f,z+2),new Vector3(r.width+.4f,.3f,4),0);
   for(float z=r.yMin+.5f;z<r.yMax;z+=1.5f)Fit(root,Generic+"Building/SM_Gen_Bld_Beam_01.prefab",new Vector3(r.center.x,3.3f,z),new Vector3(.22f,.22f,r.width),90);
   foreach(float z in new[]{r.yMin+3,r.yMax-3}){var lantern=Put(root,Adventure+"Items/SM_Item_Lantern_02.prefab",new Vector3(r.center.x,2.6f,z),0,1.3f);lantern.name="SM_Item_Lantern_Passage";}
  }
  static void Cover(GameObject map) {
   var root=Group(map,"Cover");
   foreach(var (p,yaw) in CrossingMap.LowWalls)Fit(root,Adventure+"Buildings/SM_Bld_Wall_01.prefab",p+Vector3.up*.6f,new Vector3(4,1.2f,.6f),yaw);
   foreach(var (p,high) in CrossingMap.CrateStacks)for(int i=0;i<high;i++)for(int j=0;j<2;j++)
    Put(root,Prop(j==0?"Crate_01":"Crate_02"),p+new Vector3(j*1.05f-.525f,i*1.0f,0),(i*17+j*9)%12-6,1/.77f);
  }
  static void Market(GameObject map) {
   var root=Group(map,"Market");int i=0;
   foreach(var (p,yaw) in CrossingLayout.Stalls) {
    Fit(root,Adventure+"Buildings/SM_Bld_Stall_0"+(i%4+1)+".prefab",p+Vector3.up*1.5f,CrossingLayout.StallSize,yaw+180);
    Fit(root,Adventure+"Buildings/SM_Bld_Stall_Cover_0"+(i%3+1)+".prefab",p+Vector3.up*1.52f,CrossingLayout.StallSize+new Vector3(.05f,.05f,.05f),yaw+180);
    Put(root,Prop(i%2==0?"Basket_02":"Pumpkin_01"),p+new Vector3(-2.1f,0,.6f),yaw,1);Put(root,Prop("Sack_0"+(i%2+1)),p+new Vector3(2.1f,0,.7f),yaw+30,1);
    i++;
   }
   Put(root,Prop("RoadSign_01"),new Vector3(-8,0,-21.2f),180,1.1f);Put(root,Prop("Washingline_02"),new Vector3(2,2.2f,-12),90,1);Put(root,Prop("Washingline_01"),new Vector3(14,2.5f,12),90,.75f);
   Put(root,Prop("Logpile_01"),new Vector3(-50.6f,0,24.6f),90,.8f);Put(root,Prop("Barrel_01"),new Vector3(-51,0,-18.5f),0,1);Put(root,Prop("Barrel_01"),new Vector3(50.8f,0,-34.8f),40,1);
   Put(root,Prop("Cart_02"),new Vector3(-50.6f,0,-34.2f),90,1);Put(root,Prop("Chest_01"),new Vector3(-44,CrossingLayout.Upper,35.4f),180,1);
  }
  static void Greenery(GameObject map) {
   var root=Group(map,"Greenery");int i=0;
   // Trees where the Blender map had palms (their trunk colliders stay), and against the plaza walls.
   foreach(var p in new[]{new Vector3(-36,0,-30),new Vector3(36,0,-30),new Vector3(-20,0,-23),new Vector3(20,0,-23)})Put(root,Env("Tree_0"+(i++%3+1)),p,i*47,1.05f);
   foreach(var p in new[]{new Vector3(-30,0,-20.6f),new Vector3(30,0,-20.6f),new Vector3(-6,0,-20.6f),new Vector3(-40.6f,0,-14),new Vector3(36.6f,0,8),new Vector3(15.4f,0,-1),new Vector3(0.6f,0,-17),new Vector3(-27,0,20.6f),new Vector3(27,0,20.6f)}) {
    Put(root,Env(i%2==0?"Bush_03":"Plant_01"),p,i*31,.7f);Put(root,Env(i%3==0?"Flower_01":"Flower_03"),p+new Vector3(.5f,0,.3f),i*13,1.2f);i++;
   }
   // A grove outside the walls: treetops over the battlements.
   for(int k=0;k<36;k++){float a=k*Mathf.PI*2/36;var p=new Vector3(Mathf.Cos(a)*(CrossingLayout.HalfX+7+(k%3)*3),0,Mathf.Sin(a)*(CrossingLayout.HalfZ+7+(k%4)*3));Put(root,Env("Tree_0"+(k%3+1)),p,k*41,1.8f+(k%3)*.25f);}
  }
  static void Horizon(GameObject map)=>Horizon(map,165,150);
  static void Horizon(GameObject map,float rx,float rz,float plain=-.05f) {
   var root=Group(map,"Horizon");
   for(int k=0;k<8;k++){float a=k*Mathf.PI/4+.2f;var p=new Vector3(Mathf.Cos(a)*rx,-6,Mathf.Sin(a)*rz);var m=Put(root,Generic+"Environment/SM_Gen_Env_Background_Mountain_0"+(k%2+1)+".prefab",p,-a*Mathf.Rad2Deg-90,1.1f+(k%3)*.15f);m.transform.position+=Vector3.down*6;}
   Surface(root,"Outer plain",new Vector3(0,plain,0),new Vector3(420,.05f,420),new Color(.62f,.58f,.4f));
  }

  static GameObject Group(GameObject map,string name){var g=new GameObject(name);g.transform.SetParent(map.transform,false);return g;}
  static void CrossSurface(GameObject parent,string name,Vector3 center,Vector3 size,Color color) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent.transform);go.transform.position=center;go.transform.localScale=size;
   Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=VillageMaterial("Crossing_"+ColorUtility.ToHtmlStringRGB(color),color);
  }

  // ---------- merge static parts into chunk meshes ----------
  static bool Lives(Transform t,Transform root) {
   for(;t!=null&&t!=root;t=t.parent)if(t.name.StartsWith("SM_Env_Tree")||t.name.StartsWith("SM_Env_Bush")||t.name.StartsWith("SM_Item_Lantern")||t.name=="District pennant")return true;
   return false;
  }
  static void MergeStatic(GameObject map,string asset) {
   const float Chunk=28;
   var groups=new Dictionary<(Material mat,int cx,int cz),List<CombineInstance>>();var merged=new List<MeshRenderer>();
   foreach(var r in map.GetComponentsInChildren<MeshRenderer>()) {
    if(Lives(r.transform,map.transform))continue;var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
    var c=r.bounds.center;var key=(Mathf.FloorToInt((c.x+200)/Chunk),Mathf.FloorToInt((c.z+200)/Chunk));
    var mats=r.sharedMaterials;
    for(int s=0;s<f.sharedMesh.subMeshCount;s++) {
     var mat=mats[Mathf.Min(s,mats.Length-1)];if(mat==null)continue;
     if(!groups.TryGetValue((mat,key.Item1,key.Item2),out var list))groups[(mat,key.Item1,key.Item2)]=list=new List<CombineInstance>();
     list.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=s,transform=map.transform.worldToLocalMatrix*r.localToWorldMatrix});
    }
    merged.Add(r);
   }
   var path=Generated+asset+"Meshes.asset";AssetDatabase.DeleteAsset(path);Mesh container=null;
   var parent=Group(map,"Merged");int n=0;
   foreach(var g in groups) {
    var mesh=new Mesh{name=$"{asset} {g.Key.mat.name} {g.Key.cx},{g.Key.cz}",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(g.Value.ToArray(),true,true);mesh.RecalculateBounds();
    if(container==null){AssetDatabase.CreateAsset(mesh,path);container=mesh;}else AssetDatabase.AddObjectToAsset(mesh,container);
    var go=new GameObject(mesh.name);go.transform.SetParent(parent.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=g.Key.mat;n++;
   }
   foreach(var r in merged){var go=r.gameObject;Object.DestroyImmediate(r.GetComponent<MeshFilter>());Object.DestroyImmediate(r);}
   // Drop holders left with nothing to show.
   foreach(var t in map.GetComponentsInChildren<Transform>(true).Reverse())if(t!=map.transform&&t!=null&&t.childCount==0&&t.GetComponent<Renderer>()==null&&t.GetComponent<ParticleSystem>()==null)Object.DestroyImmediate(t.gameObject);
   AssetDatabase.SaveAssets();
   Debug.Log($"{asset} dressing: {merged.Count} pieces merged into {n} chunk meshes.");
  }
 }
}
