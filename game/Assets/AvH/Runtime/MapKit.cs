using UnityEngine;
namespace AvH {
 /// <summary>
 /// Physics layers. <see cref="CameraOnly"/> holds collision copies of the dressed maps' visible-only scenery
 /// (truss beams, handrails, awnings): the third person camera keeps clear of them, while bodies, shots, props and
 /// the bot grid ignore them (<see cref="World"/> is the mask every game query uses).
 /// </summary>
 public static class Layers {
  public const int CameraOnly=29;
  public const int World=Physics.DefaultRaycastLayers&~(1<<CameraOnly);
  static bool set;
  /// <summary>Bodies and props never touch camera-only scenery.</summary>
  public static void Ensure(){if(set)return;set=true;for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(CameraOnly,i,true);}
 }
 /// <summary>
 /// Building blocks shared by the module maps (warehouse, sand crossing): Blender modules are visual only,
 /// colliders are plain boxes, stepped stair columns and solid ramp wedges the bot grid reads as one floor.
 /// </summary>
 public static class MapKit {
  static readonly Color Fallback=new Color(.55f,.55f,.52f);
  /// <summary>One Blender module from Resources (e.g. "Warehouse/WH_Floor"). Without the imported model a plain box stands in.</summary>
  public static Transform Module(Transform parent,string resource,Vector3 foot,float yaw) {
   var prefab=Resources.Load<GameObject>(resource);
   GameObject go;
   if(prefab!=null){go=Object.Instantiate(prefab,parent);foreach(var c in go.GetComponentsInChildren<Collider>())Object.Destroy(c);}
   else{go=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(Fallback);}
   go.name=resource.Substring(resource.LastIndexOf('/')+1);go.transform.localPosition=foot;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
   return go.transform;
  }
  public static void Collider(GameObject owner,Vector3 center,Vector3 size){var box=owner.AddComponent<BoxCollider>();box.center=center;box.size=size;}
  /// <summary>A visible primitive box with its own collider.</summary>
  public static Transform Solid(Transform parent,string name,Vector3 center,Vector3 size,Color color) {
   var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);
   box.transform.localPosition=center;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(color);
   return box.transform;
  }
  /// <summary>A collider-only box (the visible modules sit on top of it).</summary>
  public static void Invisible(Transform parent,string name,Vector3 center,Vector3 size) {
   var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=center;Collider(go,Vector3.zero,size);
  }
  /// <summary>Stepped columns under a module whose origin is the stair's floor centre and which climbs toward local +Z.</summary>
  public static void StairColliders(Transform stair,float length,float width,float height,int steps) {
   float run=length/steps,rise=height/steps;
   for(int i=0;i<steps;i++){float h=(i+1)*rise;Collider(stair.gameObject,new Vector3(0,h/2,-length/2+run*(i+.5f)),new Vector3(width,h,run));}
  }
  /// <summary>A solid convex wedge rising toward local +Z, so the slope is the only floor the bot grid sees there.</summary>
  public static void RampWedge(Transform ramp,float length,float width,float height) {
   float l=length,w=width,h=height;
   var mesh=new Mesh{name="Ramp wedge"};
   mesh.vertices=new[]{new Vector3(-w/2,0,-l/2),new Vector3(w/2,0,-l/2),new Vector3(-w/2,0,l/2),new Vector3(w/2,0,l/2),new Vector3(-w/2,h,l/2),new Vector3(w/2,h,l/2)};
   mesh.triangles=new[]{0,2,1, 1,2,3, 0,1,4, 1,5,4, 2,4,3, 3,4,5, 0,4,2, 1,3,5};
   mesh.RecalculateNormals();mesh.RecalculateBounds();
   var wedge=new GameObject("Ramp collider");wedge.transform.SetParent(ramp,false);var collider=wedge.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
  }
 }
}
