using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace AvH.Editor {
 /// <summary>
 /// The two-storey warehouse dressed as a timber granary with the owned Synty kit: plank-and-plaster walls, an open
 /// roof truss (daylight still reaches the floor), a board floor, shelving under the mezzanine, a wooden stair and ramp,
 /// fenced farmyard. Visual only: every collider stays with <see cref="WarehouseMap"/> and the dressing follows
 /// <see cref="WarehouseLayout"/>.
 /// </summary>
 public static partial class ConfigureOwnedAssets {
  const string Beam=Generic+"Building/SM_Gen_Bld_Beam_01.prefab";
  static readonly Color Board=new Color(.55f,.4f,.26f),BoardDark=new Color(.47f,.33f,.21f),Gap=new Color(.2f,.15f,.1f);
  static GameObject BuildWarehouse() {
   var map=new GameObject("Warehouse dressing");
   WarehouseGround(map);WarehouseWalls(map);Truss(map);Mezzanine(map);WarehouseClimbs(map);Palisade(map);WarehouseProps(map);
   var grove=Group(map,"Grove");
   for(int k=0;k<30;k++){float a=k*Mathf.PI*2/30;var p=new Vector3(Mathf.Cos(a)*(WarehouseLayout.YardHalfX+6+(k%3)*3),0,Mathf.Sin(a)*(WarehouseLayout.YardHalfZ+6+(k%4)*3));Put(grove,Env("Tree_0"+(k%3+1)),p,k*41,1.7f+(k%3)*.25f);}
   Horizon(map,120,110);
   MergeStatic(map,"Warehouse");
   var dressing=PrefabUtility.SaveAsPrefabAsset(map,Generated+"Warehouse.prefab");Object.DestroyImmediate(map);
   return dressing;
  }

  static void WarehouseGround(GameObject map) {
   var root=Group(map,"Ground");float hx=WarehouseLayout.HalfX,hz=WarehouseLayout.HalfZ,deck=WarehouseLayout.DeckHalfX;
   CrossSurface(root,"Farmyard earth",new Vector3(0,.004f,0),new Vector3(WarehouseLayout.YardHalfX*2,.008f,WarehouseLayout.YardHalfZ*2),new Color(.5f,.45f,.3f));
   int n=0;
   for(float x=-WarehouseLayout.YardHalfX;x<WarehouseLayout.YardHalfX-.1f;x+=4)for(float z=-WarehouseLayout.YardHalfZ;z<WarehouseLayout.YardHalfZ-.1f;z+=4) {
    if(x+4>-hx-.3f&&x<hx+.3f&&z+4>-hz-.3f&&z<hz+.3f)continue;
    Fit(root,Env("FloorTile_0"+(n++%3+1)),new Vector3(x+2,.015f,z+2),new Vector3(4,0,4),(n*90)%360);
   }
   // Cart tracks from each door out to the fence.
   foreach(var x in WarehouseLayout.SouthDoors)for(float z=-hz-.3f;z>-WarehouseLayout.YardHalfZ+.1f;z-=6)Fit(root,Env("Road_Straight_01"),new Vector3(x,.03f,Mathf.Max(z-3,-WarehouseLayout.YardHalfZ+3)),new Vector3(3.6f,.04f,6),0);
   foreach(float side in new[]{-1f,1f})for(float x=hx+.3f;x<WarehouseLayout.YardHalfX-.1f;x+=5)Fit(root,Env("Road_Straight_01"),new Vector3(side*(x+2.5f),.03f,WarehouseLayout.SideDoorZ),new Vector3(3.6f,.04f,5),90);
   // Board floors: the hall and the mezzanine deck.
   Planks(root,Rect.MinMaxRect(-hx,-hz,hx,WarehouseLayout.DeckFront),0,"Hall floor");
   Planks(root,Rect.MinMaxRect(-hx,WarehouseLayout.DeckFront,-deck,hz),0,"Hall floor west");Planks(root,Rect.MinMaxRect(deck,WarehouseLayout.DeckFront,hx,hz),0,"Hall floor east");
   Planks(root,Rect.MinMaxRect(-deck,WarehouseLayout.DeckFront,deck,hz),WarehouseLayout.Upper,"Deck floor");
  }
  static void Planks(GameObject root,Rect r,float y,string name)=>Paving(root,r,y,name,2.4f,.32f,.025f,Gap,Board,BoardDark);

  // Plank outside, warm plaster inside; doorways keep a lintel wall above them.
  static void WarehouseWalls(GameObject map) {
   var root=Group(map,"Walls");float hx=WarehouseLayout.HalfX,hz=WarehouseLayout.HalfZ,t=WarehouseLayout.WallThickness,h=WarehouseLayout.WallHeight,door=WarehouseLayout.DoorWidth/2;int index=0;
   var south=Openings(-hx,hx,WarehouseLayout.SouthDoors);var side=Openings(-hz,hz,new[]{WarehouseLayout.SideDoorZ});
   foreach(var (a,b) in south){Facade(root,new Vector2(b,-hz),new Vector2(a,-hz),Vector2.up,h,ref index,false,1);Facade(root,new Vector2(a,-hz-t),new Vector2(b,-hz-t),Vector2.down,h,ref index,false,Timber);}
   Facade(root,new Vector2(hx,hz),new Vector2(-hx,hz),Vector2.down,h,ref index,false,1);Facade(root,new Vector2(-hx,hz+t),new Vector2(hx,hz+t),Vector2.up,h,ref index,false,Timber);
   foreach(var (a,b) in side) {
    Facade(root,new Vector2(-hx,a),new Vector2(-hx,b),Vector2.right,h,ref index,false,1);Facade(root,new Vector2(-hx-t,b),new Vector2(-hx-t,a),Vector2.left,h,ref index,false,Timber);
    Facade(root,new Vector2(hx,b),new Vector2(hx,a),Vector2.left,h,ref index,false,1);Facade(root,new Vector2(hx+t,a),new Vector2(hx+t,b),Vector2.right,h,ref index,false,Timber);
   }
   // Lintels and posts at every doorway.
   var doors=new List<(Vector3 c,float yaw)>();foreach(var x in WarehouseLayout.SouthDoors)doors.Add((new Vector3(x,0,-hz-t/2),0));foreach(float s in new[]{-1f,1f})doors.Add((new Vector3(s*(hx+t/2),0,WarehouseLayout.SideDoorZ),90));
   foreach(var (c,yaw) in doors) {
    Restyle(Fit(root,Base("Wall_01"),c+Vector3.up*4.8f,new Vector3(door*2,2.4f,t+.02f),yaw),Timber);
    Fit(root,Beam,c+Vector3.up*3.5f,new Vector3(.3f,.3f,door*2+.6f),yaw+90);
    var across=Quaternion.Euler(0,yaw,0)*Vector3.right*(door+.15f);
    foreach(float s in new[]{-1f,1f})Restyle(Fit(root,Base("Pillar_01"),c+across*s+Vector3.up*3.5f,new Vector3(.4f,3.5f,t+.15f),0),Timber);
   }
   // Timber frame posts on the inside faces, one under each truss, with a wall plate along the top.
   for(float x=-hx+4;x<hx-.1f;x+=4)foreach(float s in new[]{-1f,1f}){if(s<0&&System.Array.Exists(WarehouseLayout.SouthDoors,d=>Mathf.Abs(d-x)<2.3f))continue;Restyle(Fit(root,Base("Pillar_01"),new Vector3(x,h/2,s*(hz-.12f)),new Vector3(.32f,h,.24f),0),Timber);}
   for(float z=-hz+4;z<hz-.1f;z+=4)foreach(float s in new[]{-1f,1f}){if(Mathf.Abs(z-WarehouseLayout.SideDoorZ)<2.3f)continue;Restyle(Fit(root,Base("Pillar_01"),new Vector3(s*(hx-.12f),h/2,z),new Vector3(.24f,h,.32f),0),Timber);}
   foreach(float s in new[]{-1f,1f}){Fit(root,Beam,new Vector3(0,h-.2f,s*(hz-.14f)),new Vector3(.24f,.28f,hx*2),90);Fit(root,Beam,new Vector3(s*(hx-.14f),h-.2f,0),new Vector3(.24f,.28f,hz*2),0);}
   foreach(var corner in new[]{new Vector2(-hx-t/2,-hz-t/2),new Vector2(hx+t/2,-hz-t/2),new Vector2(-hx-t/2,hz+t/2),new Vector2(hx+t/2,hz+t/2)})Restyle(Fit(root,Base("Pillar_01"),V(corner,h/2+.1f),new Vector3(.7f,h+.2f,.7f),0),Timber);
  }
  static List<(float a,float b)> Openings(float from,float to,float[] doors) {
   var runs=new List<(float,float)>();float start=from;var sorted=new List<float>(doors);sorted.Sort();
   foreach(var d in sorted){runs.Add((start,d-WarehouseLayout.DoorWidth/2));start=d+WarehouseLayout.DoorWidth/2;}
   runs.Add((start,to));return runs;
  }

  // An open king-post truss every 4 m: the hall reads as a barn while daylight still falls on the floor.
  static void Truss(GameObject map) {
   var root=Group(map,"Roof truss");float hz=WarehouseLayout.HalfZ+.3f,top=WarehouseLayout.WallHeight+.1f,ridge=top+3;
   float half=Mathf.Sqrt(hz*hz+(ridge-top)*(ridge-top)),angle=Mathf.Atan2(ridge-top,hz)*Mathf.Rad2Deg;
   for(float x=-WarehouseLayout.HalfX;x<=WarehouseLayout.HalfX+.1f;x+=4) {
    Fit(root,Beam,new Vector3(x,top,0),new Vector3(.32f,.32f,hz*2),0);
    Fit(root,Beam,new Vector3(x,(top+ridge)/2,0),new Vector3(.26f,.26f,ridge-top),0,90);
    foreach(float s in new[]{-1f,1f})Fit(root,Beam,new Vector3(x,(top+ridge)/2,s*hz/2),new Vector3(.26f,.26f,half),0,s*angle);
   }
   Fit(root,Beam,new Vector3(0,ridge,0),new Vector3(.3f,.3f,WarehouseLayout.HalfX*2+.6f),90);
   foreach(float s in new[]{-1f,1f})Fit(root,Beam,new Vector3(0,(top+ridge)/2+.05f,s*hz/2),new Vector3(.24f,.24f,WarehouseLayout.HalfX*2+.6f),90);
   // Lanterns hang from the tie beams over the hall.
   foreach(var x in new[]{-12f,-4f,4f,12f})foreach(var z in new[]{-8f,0f}) {
    Fit(root,Beam,new Vector3(x,(top+4.9f)/2,z),new Vector3(.04f,top-4.9f,.04f),0,90);
    var lantern=Put(root,Adventure+"Items/SM_Item_Lantern_02.prefab",new Vector3(x,4.4f,z),0,1.4f);lantern.name="SM_Item_Lantern_Hall";
   }
  }

  // The rack blocks under the deck become plank shelving with stored goods on the ledges; a fence runs along the deck edge.
  static void Mezzanine(GameObject map) {
   var root=Group(map,"Mezzanine");float d=WarehouseLayout.DeckHalfX,front=WarehouseLayout.DeckFront,back=WarehouseLayout.DeckBack,up=WarehouseLayout.Upper;int index=0;
   Facade(root,new Vector2(-d,front),new Vector2(d,front),Vector2.down,up,ref index,false,Timber);
   Facade(root,new Vector2(-d,back),new Vector2(-d,front),Vector2.left,up,ref index,false,Timber);Facade(root,new Vector2(d,front),new Vector2(d,back),Vector2.right,up,ref index,false,Timber);
   foreach(var y in new[]{1.15f,2.3f})Fit(root,Beam,new Vector3(0,y,front-.08f),new Vector3(.12f,.12f,d*2),90);
   Fit(root,Beam,new Vector3(0,up-.12f,front-.1f),new Vector3(.24f,.24f,d*2+.2f),90);
   for(float x=-d;x<=d+.1f;x+=4)Restyle(Fit(root,Base("Pillar_01"),new Vector3(x,up/2,front-.12f),new Vector3(.32f,up,.32f),0),Timber);
   foreach(var (from,to) in WarehouseMap.RailSegments())Fit(root,Adventure+"Buildings/SM_Bld_Fence_01.prefab",new Vector3((from+to)/2,up+.5f,front-.05f),new Vector3(to-from,1,.18f),0);
   // Goods stored against the back wall up on the deck (out of the way of the stair and ramp heads).
   foreach(var x in new[]{-8f,-2f,4f,9f}){Fit(root,Generic+"Props/SM_Gen_Prop_Sack_Stack_01.prefab",new Vector3(x,up+.45f,back-.6f),new Vector3(1.1f,.9f,1),x*10);Put(root,Prop("Barrel_01"),new Vector3(x+1.3f,up,back-.55f),x*7,.9f);}
  }

  static void WarehouseClimbs(GameObject map) {
   var root=Group(map,"Climbs");float up=WarehouseLayout.Upper;
   {var centre=new Vector3(WarehouseLayout.StairX,0,WarehouseLayout.DeckFront-WarehouseLayout.StairLength/2);float l=WarehouseLayout.StairLength,w=WarehouseLayout.StairWidth,run=l/21,rise=up/21;
    for(int i=0;i<21;i++){float top=(i+1)*rise;Fit(root,Beam,centre+new Vector3(0,top/2,-l/2+run*(i+.5f)),new Vector3(w,top,run+.01f),0);}
    foreach(float s in new[]{-1f,1f})Fit(root,Beam,centre+new Vector3(s*(w/2+.07f),up/2+.2f,0),new Vector3(.14f,.4f,Mathf.Sqrt(l*l+up*up)),0,-Mathf.Atan2(up,l)*Mathf.Rad2Deg);}
   {var centre=new Vector3(WarehouseLayout.RampX,0,WarehouseLayout.DeckFront-WarehouseLayout.RampLength/2);float l=WarehouseLayout.RampLength,w=WarehouseLayout.RampWidth,angle=Mathf.Atan2(up,l)*Mathf.Rad2Deg;
    Fit(root,Beam,centre+new Vector3(0,up/2+.04f,0),new Vector3(w,.08f,Mathf.Sqrt(l*l+up*up)),0,-angle);
    for(int k=1;k<12;k++){float top=up*k/l;Fit(root,Beam,centre+new Vector3(0,top/2,-l/2+k+.5f),new Vector3(w-.04f,top,1),0);}
    for(int k=1;k<12;k+=2)Fit(root,Beam,centre+new Vector3(0,up*(k+.5f)/l+.1f,-l/2+k+.5f),new Vector3(.18f,.08f,w-.2f),90);}
  }

  // A 3 m plank palisade on the yard fence line, posts every few metres.
  static void Palisade(GameObject map) {
   var root=Group(map,"Palisade");float hx=WarehouseLayout.YardHalfX,hz=WarehouseLayout.YardHalfZ,h=WarehouseLayout.FenceHeight;
   foreach(var (a,b) in new[]{(new Vector2(-hx,-hz),new Vector2(hx,-hz)),(new Vector2(-hx,hz),new Vector2(hx,hz)),(new Vector2(-hx,-hz),new Vector2(-hx,hz)),(new Vector2(hx,-hz),new Vector2(hx,hz))}) {
    float length=Vector2.Distance(a,b);int count=Mathf.CeilToInt(length/3.1f);float piece=length/count;var along=(b-a).normalized;float yaw=Mathf.Abs(along.x)>.5f?0:90;
    for(int i=0;i<count;i++){var mid=a+along*(piece*(i+.5f));Fit(root,Adventure+"Buildings/SM_Bld_Fence_01.prefab",V(mid,h/2),new Vector3(piece,h,.3f),yaw);}
    for(int i=0;i<=count;i++)Fit(root,Adventure+"Buildings/SM_Bld_FencePost_01.prefab",V(a+along*(piece*i),h/2+.15f),new Vector3(.3f,h+.3f,.3f),0);
   }
  }

  // Farm goods: against walls and fences only, so nothing visual stands in the open play space.
  static void WarehouseProps(GameObject map) {
   var root=Group(map,"Props");
   foreach(var (p,high) in WarehouseMap.CoverStacks)for(int i=0;i<high;i++)for(int j=0;j<2;j++)
    Put(root,Prop(j==0?"Crate_01":"Crate_02"),p+new Vector3(j*1.05f-.525f,i*1.0f,0),(i*17+j*9)%12-6,1/.77f);
   float hx=WarehouseLayout.HalfX,hz=WarehouseLayout.HalfZ,yx=WarehouseLayout.YardHalfX,yz=WarehouseLayout.YardHalfZ;
   foreach(var p in new[]{new Vector3(-23.3f,0,-13.3f),new Vector3(23.3f,0,-13.3f),new Vector3(-23.3f,0,2),new Vector3(23.3f,0,-2)})Put(root,Prop("Barrel_01"),p,p.x*5,.85f);
   foreach(var p in new[]{new Vector3(-6,0,-13.4f),new Vector3(2,0,-13.4f),new Vector3(-23.4f,0,-4)})Fit(root,Generic+"Props/SM_Gen_Prop_Sack_Stack_01.prefab",p+Vector3.up*.45f,new Vector3(1,.9f,1),p.x*9);
   Put(root,Prop("Cart_02"),new Vector3(-yx+1,0,-yz+3),0,1);Put(root,Prop("Cart_01"),new Vector3(yx-1,0,yz-3),180,1);
   Put(root,Prop("Logpile_01"),new Vector3(-yx+1.1f,0,yz-4),90,.9f);Put(root,Prop("Logpile_01"),new Vector3(hx+1.2f,0,hz-3),90,.85f);
   foreach(var p in new[]{new Vector3(-hx-1,0,-hz+2),new Vector3(hx+.9f,0,4),new Vector3(-12,0,hz+.9f),new Vector3(14,0,hz+.9f),new Vector3(yx-.8f,0,-yz+1.5f)})Put(root,Prop("Barrel_02"),p,p.x*3,.8f);
   foreach(var p in new[]{new Vector3(-20,0,-yz+.8f),new Vector3(22,0,-yz+.8f),new Vector3(-yx+.8f,0,6),new Vector3(yx-.8f,0,12),new Vector3(0,0,yz-.8f)}){Put(root,Env("Bush_03"),p,p.x*11,.8f);Put(root,Env("Flower_03"),p+new Vector3(.6f,0,0),p.z*7,1.2f);}
   Put(root,Prop("RoadSign_01"),new Vector3(-18.5f,0,-hz-1.2f),180,1.1f);
  }
 }
}
