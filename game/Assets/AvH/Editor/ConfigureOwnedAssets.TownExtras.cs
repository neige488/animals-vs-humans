using UnityEngine;
namespace AvH.Editor {
 public static partial class ConfigureOwnedAssets {
  // Streets of the generated village (x/z min-max), kept clear of ground patches.
  static readonly Rect[] TownStreets={Rect.MinMaxRect(-5,-40,5,40),Rect.MinMaxRect(-41,-4.5f,41,4.5f),Rect.MinMaxRect(-31,10,33,18),Rect.MinMaxRect(-31,-19,34,-11),Rect.MinMaxRect(-34,-19,-26,30),Rect.MinMaxRect(29,-30,37,30),Rect.MinMaxRect(-12,-11,12,11)};
  /// <summary>
  /// Visual-only finish for the village, on top of its unchanged colliders: grass and dirt patches on bare ground
  /// away from the streets, plants at house corners, and a far plain with mountains well below the open edges
  /// (a fall still reads as a fall).
  /// </summary>
  static void TownExtras(GameObject map,Vector3[] homes) {
   var root=new GameObject("Finish");root.transform.SetParent(map.transform,false);int n=0;
   for(float x=-42;x<42;x+=4)for(float z=-42;z<42;z+=4) {
    var c=new Vector2(x+2,z+2);bool street=false;foreach(var r in TownStreets)if(r.Overlaps(Rect.MinMaxRect(c.x-2.5f,c.y-2.5f,c.x+2.5f,c.y+2.5f)))street=true;
    if(street||((int)(x*7+z*3)&3)==0)continue;
    Fit(root,Env("FloorTile_0"+(n++%3+1)),new Vector3(c.x,.012f,c.y),new Vector3(4,0,4),(n*90)%360);
   }
   for(int i=0;i<homes.Length;i++){float half=2.05f*(1.15f+(i%3)*.13f)+.35f;foreach(var (sx,sz) in new[]{(1,1),(-1,-1)}){var p=homes[i]+new Vector3(sx*half,0,sz*half);Put(root,Env(i%2==0?"Plant_01":"Bush_03"),p,i*37,.6f);Put(root,Env("Flower_0"+(i%2==0?1:3)),p+new Vector3(-sx*.4f,0,0),i*11,1.2f);}}
   MergeStatic(root,"TownFinish");
   Horizon(map,170,170,-34);
  }
 }
}
