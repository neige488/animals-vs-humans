using UnityEngine;
namespace AvH {
 public static class PrototypeVillage {
  public static Material Material(Color color) {
   var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
   var material=new Material(shader);material.color=color;return material;
  }
  public static void Build(Transform parent, bool useOwned = true) {
   var map=new GameObject("Village");map.transform.SetParent(parent);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   if(useOwned && catalog!=null && catalog.Village!=null) {Object.Instantiate(catalog.Village,map.transform);return;}
   // Traversable collision blockout retained until owned village layout is verified.
   Box(map.transform,"Ground",new Vector3(0,-.5f,0),new Vector3(46,1,46),new Color(.32f,.44f,.35f));
   Box(map.transform,"Rooftop shelter",new Vector3(-12,2,10),new Vector3(7,4,6),new Color(.5f,.35f,.23f));
   // 24 shallow steps give both factions access without abilities.
   for(int i=0;i<24;i++) Box(map.transform,"Roof stair "+i,new Vector3(-12,(i+1)*.0833f, -2+i*.4f),new Vector3(2,(i+1)*.1666f,.42f),new Color(.6f,.49f,.37f));
   Box(map.transform,"Corner back",new Vector3(17,1.5f,14),new Vector3(1,3,8),new Color(.6f,.56f,.42f));
   Box(map.transform,"Corner side",new Vector3(14,1.5f,18),new Vector3(7,3,1),new Color(.6f,.56f,.42f));
   Box(map.transform,"Warehouse back",new Vector3(9,1.5f,-17),new Vector3(8,3,.5f),new Color(.42f,.4f,.5f));
   Box(map.transform,"Warehouse side",new Vector3(13,1.5f,-14),new Vector3(.5f,3,6),new Color(.42f,.4f,.5f));
   Box(map.transform,"Warehouse roof",new Vector3(9,3.3f,-14),new Vector3(8,.3f,6),new Color(.32f,.3f,.4f));
   Box(map.transform,"Alley house",new Vector3(-7,1.5f,-11),new Vector3(7,3,6),new Color(.65f,.5f,.35f));
   Box(map.transform,"Alley wall",new Vector3(5,1.2f,7),new Vector3(1,2.4f,7),new Color(.6f,.55f,.4f));
  }
  static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Color color) {
   var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent);
   box.transform.position=position;box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=Material(color);
  }
 }
}
