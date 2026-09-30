using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
namespace AvH {
 public static class PrototypeVillage {
  public static IReadOnlyList<Vector3> DefaultShelters {get;}=Array.AsReadOnly(new[]{TownLayout.Roof,TownLayout.Courtyard,TownLayout.Workshop});
  public static Material Material(Color color) {
   var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
   var material=new Material(shader);material.color=color;return material;
  }
  public static IReadOnlyList<Vector3> Build(Transform parent, bool useOwned = true) {
   var map=new GameObject("Village");map.transform.SetParent(parent);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   if(useOwned && catalog!=null && catalog.Village!=null) {UnityEngine.Object.Instantiate(catalog.Village,map.transform);
    if(catalog.ShelterPoints==null||catalog.ShelterPoints.Length!=3||catalog.ShelterPoints.Distinct().Count()!=3||catalog.ShelterPoints.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)))throw new InvalidOperationException("보유 마을 쉘터 정의를 ConfigureOwnedAssets로 다시 생성하세요.");
    return Array.AsReadOnly((Vector3[])catalog.ShelterPoints.Clone());}
   var roof=DefaultShelters[0];var corner=DefaultShelters[1];var warehouse=DefaultShelters[2];
   // Traversable collision blockout retained until owned village layout is verified.
   Box(map.transform,"Ground",new Vector3(0,-.5f,0),new Vector3(TownLayout.HalfExtent*2,1,TownLayout.HalfExtent*2),new Color(.32f,.44f,.35f));
   Box(map.transform,"Rooftop shelter",new Vector3(roof.x,roof.y/2,roof.z),new Vector3(7,roof.y,6),new Color(.5f,.35f,.23f));
   // 24 shallow steps give both factions access without abilities.
   for(int i=0;i<24;i++) Box(map.transform,"Roof stair "+i,new Vector3(roof.x,(i+1)*roof.y/48,roof.z-12+i*.4f),new Vector3(2,(i+1)*roof.y/24,.42f),new Color(.6f,.49f,.37f));
   Box(map.transform,"Corner back",corner+new Vector3(3,1.5f,-1),new Vector3(1,3,8),new Color(.6f,.56f,.42f));
   Box(map.transform,"Corner side",corner+new Vector3(0,1.5f,3),new Vector3(7,3,1),new Color(.6f,.56f,.42f));
   Box(map.transform,"Warehouse back",warehouse+new Vector3(0,1.5f,-3),new Vector3(8,3,.5f),new Color(.42f,.4f,.5f));
   Box(map.transform,"Warehouse side",warehouse+new Vector3(4,1.5f,0),new Vector3(.5f,3,6),new Color(.42f,.4f,.5f));
   Box(map.transform,"Warehouse roof",warehouse+new Vector3(0,3.3f,0),new Vector3(8,.3f,6),new Color(.32f,.3f,.4f));
   Box(map.transform,"Alley house",new Vector3(-7,1.5f,-11),new Vector3(7,3,6),new Color(.65f,.5f,.35f));
   Box(map.transform,"Alley wall",new Vector3(5,1.2f,7),new Vector3(1,2.4f,7),new Color(.6f,.55f,.4f));
   return DefaultShelters;
  }
  static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Color color) {
   var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent);
   box.transform.position=position;box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=Material(color);
  }
 }
}
