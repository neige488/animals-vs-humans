using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static partial class ConfigureOwnedAssets {
  const string Adventure="Synty/PolygonAdventure/Prefabs/";
  static GameObject BuildVillage() {
   var map=new GameObject("Market village");PrototypeVillage.Build(map.transform,false);
   // Keep visible solid geometry coincident with the tested walkable surfaces.
   var layout=map.transform.GetChild(0);
   foreach(Transform block in layout) {
    var color=block.name=="Ground"?new Color(.47f,.55f,.36f):block.name.Contains("stair")||block.name.Contains("roof")?new Color(.55f,.39f,.23f):new Color(.76f,.68f,.5f);
    block.GetComponent<Renderer>().sharedMaterial=VillageMaterial(block.name=="Ground"?"Grass":block.name.Contains("stair")||block.name.Contains("roof")?"Timber":"Plaster",color);
   }
   // A crossing main street links the plaza, market and the three defensive positions.
   Surface(map,"Main street",new Vector3(0,.012f,0),new Vector3(7,.02f,44),new Color(.63f,.56f,.42f));
   Surface(map,"Market lane",new Vector3(0,.014f,0),new Vector3(44,.02f,5),new Color(.63f,.56f,.42f));
   Surface(map,"Market square",new Vector3(0,.016f,-3),new Vector3(14,.02f,12),new Color(.68f,.62f,.49f));
   Surface(map,"Courtyard path",new Vector3(11,.014f,13),new Vector3(18,.02f,4),new Color(.63f,.56f,.42f));
   Surface(map,"Warehouse lane",new Vector3(8,.014f,-11),new Vector3(10,.02f,6),new Color(.63f,.56f,.42f));
   // Buildings retain uniform scale. Closed obstacle volumes avoid trapping bots inside decorative hollow meshes.
   Place(map,"Buildings/SM_Bld_Village_01.prefab",new Vector3(-18,0,-16),0,1);
   Place(map,"Buildings/SM_Bld_Village_02.prefab",new Vector3(18,0,-7),180,1);
   Place(map,"Buildings/SM_Bld_Village_03.prefab",new Vector3(-18,0,3),90,1);
   Place(map,"Buildings/SM_Bld_Village_04.prefab",new Vector3(8,0,20),180,1);
   Place(map,"Buildings/SM_Bld_Village_01.prefab",new Vector3(-4,0,20),180,.85f);
   Place(map,"Buildings/SM_Bld_Village_02.prefab",new Vector3(-19,0,-9),90,.8f);
   Place(map,"Buildings/SM_Bld_Village_03.prefab",new Vector3(20,0,5),270,.85f);
   // Working stalls sit beside the square, with supplies at their backs.
   foreach(float x in new[]{-7f,7f}) {
    Place(map,"Buildings/SM_Bld_Stall_01.prefab",new Vector3(x,0,-7),x<0?90:270,1);
    Place(map,"Props/SM_Prop_Crate_01.prefab",new Vector3(x+2.5f,0,-8),12,1);
    Place(map,"Props/SM_Prop_Barrel_01.prefab",new Vector3(x+3.6f,0,-8.4f),0,1);
   }
   Place(map,"Buildings/SM_Bld_Well_01.prefab",new Vector3(-6,0,7),0,.8f);
   Place(map,"Props/SM_Prop_Cart_01.prefab",new Vector3(17,0,-13),25,1);
   foreach(var p in new[]{new Vector3(11,0,-19),new Vector3(12,0,-19),new Vector3(19,0,16),new Vector3(-16,0,16)})
    Place(map,"Props/SM_Prop_Crate_01.prefab",p,0,.85f);
   foreach(var p in new[]{new Vector3(-20,0,19),new Vector3(-5,0,15),new Vector3(21,0,20),new Vector3(20,0,-20),new Vector3(-20,0,-21)})
    Place(map,"Environments/SM_Env_Tree_05.prefab",p,35,.8f);
   return map;
  }
  static Material VillageMaterial(string name,Color color) {
   var path=Generated+"Village_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",0);EditorUtility.SetDirty(mat);return mat;
  }
  static void Surface(GameObject map,string name,Vector3 center,Vector3 size,Color color) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(map.transform);go.transform.position=center;go.transform.localScale=size;
   Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=VillageMaterial("Surface_"+name,color);
  }
  static GameObject Place(GameObject map,string path,Vector3 floor,float yaw,float scale,bool solid=true) {
   var go=Object.Instantiate(Load(Adventure+path),map.transform);Clean(go);go.transform.localScale*=scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);
   var bounds=Bounds(go);go.transform.position+=floor-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   foreach(var collider in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
   if(solid) {
    var b=Bounds(go);var obstacle=new GameObject(go.name+" collision");obstacle.transform.SetParent(map.transform);
    if(path.Contains("Tree")) {
     // Foliage is visual; only the visible trunk blocks movement.
     obstacle.transform.position=new Vector3(b.center.x,b.min.y+b.size.y*.22f,b.center.z);
     var trunk=obstacle.AddComponent<CapsuleCollider>();trunk.height=b.size.y*.44f;trunk.radius=Mathf.Min(b.size.x,b.size.z)*.055f;
    } else if(path.Contains("SM_Bld_Village_")) {obstacle.transform.position=b.center;obstacle.AddComponent<BoxCollider>().size=b.size;}
    else {
     // One convex silhouette closes decorative cavities without an invisible flat AABB top.
     var parts=go.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray();
     var mesh=new Mesh();mesh.CombineMeshes(parts,true,true);
     var meshPath=Generated+"Collision_"+map.transform.childCount+".asset";
     var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
     if(stored==null){AssetDatabase.CreateAsset(mesh,meshPath);stored=mesh;}else {EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);}
     EditorUtility.SetDirty(stored);
     var hull=obstacle.AddComponent<MeshCollider>();hull.sharedMesh=stored;hull.convex=true;
    }
    if(path.Contains("SM_Bld_Village_")) {
     // These modular houses have open parapets. A visible flat deck closes the same volume as collision.
     Surface(map,go.name+" roof deck",new Vector3(b.center.x,b.max.y,b.center.z),new Vector3(b.size.x,.025f,b.size.z),new Color(.51f,.36f,.23f));
    }
   }
   return go;
  }
 }
}
