using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 /// <summary>
 /// Dresses a <see cref="MapPlan"/> with the owned Synty kit by block kind and theme: snowy terraces with stone retaining
 /// walls, huts and pines; forest ruins with broken brick walls, a cliffed chasm and a dense wood; sky islands with grassy
 /// tops, rocky undersides, plank bridges and clouds. Visual only; the plan's colliders are built by the game.
 /// </summary>
 public static partial class ConfigureOwnedAssets {
  static readonly Color Snow=new Color(.9f,.93f,.97f),SnowShade=new Color(.8f,.85f,.92f);
  static readonly Color[] ShelterColors={new Color(.75f,.18f,.15f),new Color(.17f,.35f,.72f),new Color(.9f,.7f,.18f)};
  static GameObject BuildPlanned(MapPlan plan) {
   var map=new GameObject(plan.Root+" dressing");string theme=plan.Theme;int index=0,platform=0,tree=0;
   foreach(var b in plan.Blocks) {
    var r=Rect.MinMaxRect(b.Center.x-b.Size.x/2,b.Center.z-b.Size.z/2,b.Center.x+b.Size.x/2,b.Center.z+b.Size.z/2);float top=b.Center.y+b.Size.y/2,bottom=b.Center.y-b.Size.y/2;
    switch(b.Kind) {
     case PlanKind.Ground:case PlanKind.Terrace:
      Top(map,r,top,theme);
      if(b.Kind==PlanKind.Terrace)foreach(var (a,c,n) in Sides(r))if(!Boundary(a,c))Retaining(map,a,c,n,bottom,top,theme);
      break;
     case PlanKind.Rock:Island(map,r,top,bottom);break;
     case PlanKind.Wall:WallDressing(map,r,top,bottom,theme,ref index);break;
     case PlanKind.House:HouseDressing(map,b,theme,index++);break;
     case PlanKind.Platform:
      foreach(var (a,c,n) in Sides(r))Facade(map,a,c,n,top-bottom,ref index,false,theme=="snow"?Timber:Brick);
      if(theme=="snow")CrossSurface(map,"Snow top",new Vector3(r.center.x,top+.012f,r.center.y),new Vector3(r.width,.02f,r.height),Snow);else Paving(map,r,top,"Platform paving");
      var colour=ShelterColors[platform++%3];foreach(float x in new[]{r.xMin+.8f,r.xMax-.8f})CrossBanner(map,new Vector3(x,top+1.5f,r.yMax-.5f),colour);
      break;
     case PlanKind.Deck:DeckDressing(map,r,top,theme);break;
     case PlanKind.Rubble:
      Fit(map,Prop("StoneBlock_01"),b.Center+Vector3.down*.15f,new Vector3(b.Size.x,b.Size.y*.7f,b.Size.z),b.Center.x*13);
      Fit(map,Generic+"Environment/SM_Gen_Env_Rock_0"+(index++%3+1)+".prefab",b.Center+new Vector3(.3f,.1f,-.2f),new Vector3(b.Size.x*.7f,b.Size.y,b.Size.z*.7f),b.Center.z*7);
      break;
     case PlanKind.Tree:
      string model=theme=="snow"?Env("TreePine_0"+(tree%3+1)+"_Snow"):theme=="ruins"?(tree%2==0?Generic+"Environment/SM_Gen_Env_Tree_0"+(tree/2%3+1)+".prefab":Env("Tree_04")):Env("Tree_0"+(tree%3+1));
      Put(map,model,b.Center,tree*47,theme=="ruins"?1.4f:1.2f);tree++;
      break;
    }
   }
   foreach(var c in plan.Climbs)ClimbDressing(map,c,theme);
   foreach(var rail in plan.Rails) {
    var d=rail.To-rail.From;float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg-90;var mid=(rail.From+rail.To)/2+Vector3.up*.5f;
    Fit(map,theme=="snow"?Adventure+"Buildings/SM_Bld_Fence_01_Snow.prefab":theme=="sky"?Adventure+"Buildings/SM_Bld_Fence_01.prefab":Adventure+"Buildings/SM_Bld_Wall_01.prefab",mid,new Vector3(d.magnitude,1,.3f),yaw);
   }
   foreach(var (p,high) in plan.Crates)for(int i=0;i<high;i++)for(int j=0;j<2;j++)Put(map,Prop(j==0?"Crate_01":"Crate_02"),p+new Vector3(j*1.05f-.525f,i,0),(i*17+j*9)%12-6,1/.77f);
   if(theme=="sky")SkyExtras(map);else if(theme=="snow")SnowExtras(map);else RuinsExtras(map);
   var asset=plan.Root.Replace(" ","");
   MergeStatic(map,asset);
   var dressing=PrefabUtility.SaveAsPrefabAsset(map,Generated+asset+".prefab");Object.DestroyImmediate(map);
   return dressing;
  }
  static bool Boundary(Vector2 a,Vector2 b)=>Mathf.Abs(a.x)>=31.9f&&Mathf.Abs(b.x)>=31.9f||a.y>=35.9f&&b.y>=35.9f;

  static void Top(GameObject map,Rect r,float y,string theme) {
   if(theme=="snow"){CrossSurface(map,"Snow",new Vector3(r.center.x,y+.01f,r.center.y),new Vector3(r.width,.02f,r.height),Snow);return;}
   int n=(int)(r.xMin*3+r.yMin);
   for(float x=r.xMin;x<r.xMax-.1f;x+=4)for(float z=r.yMin;z<r.yMax-.1f;z+=4) {
    float w=Mathf.Min(4,r.xMax-x),d=Mathf.Min(4,r.yMax-z);
    Fit(map,Env("FloorTile_0"+((n++&1)+1)),new Vector3(x+w/2,y+.015f,z+d/2),new Vector3(w,0,d),(n*90)%360);
   }
  }
  // Snowy terraces: a stone retaining wall in 4 m courses, snow drifts at its foot.
  static void Retaining(GameObject map,Vector2 a,Vector2 b,Vector2 normal,float bottom,float top,string theme) {
   float length=Vector2.Distance(a,b);int count=Mathf.Max(1,Mathf.RoundToInt(length/4));float piece=length/count;var along=(b-a).normalized;float yaw=Mathf.Atan2(along.x,along.y)*Mathf.Rad2Deg-90;
   for(int i=0;i<count;i++) {
    var mid=a+along*(piece*(i+.5f))-normal*.2f;
    Fit(map,Adventure+"Buildings/SM_Bld_Wall_01.prefab",V(mid,(bottom+top)/2),new Vector3(piece,top-bottom,.42f),yaw);
    if(i%3==1)Put(map,Env("SnowPile_0"+(i%3+1)),V(mid+normal*.6f,Mathf.Max(0,top-3)),yaw,.8f);
   }
   Fit(map,Base("Wall_Trim_01"),V((a+b)/2-normal*.05f,top),new Vector3(length,.16f,.5f),yaw);
  }
  // A floating island: grass on top (the plan's ground), rocks round its rim, a big inverted crag beneath.
  static void Island(GameObject map,Rect r,float top,float bottom) {
   Top(map,r,top,"sky");
   int k=0;
   foreach(var (a,b,n) in Sides(r)) {
    float length=Vector2.Distance(a,b);var along=(b-a).normalized;
    for(float t=1.5f;t<length;t+=3,k++)Fit(map,Generic+"Environment/SM_Gen_Env_Rock_0"+(k%3+1)+".prefab",V(a+along*t+n*.4f,(top+bottom)/2-.3f),new Vector3(3.4f,top-bottom-.4f,2.4f),k*53);
   }
   var under=Fit(map,Generic+"Environment/SM_Gen_Env_Rock_0"+(k%3+1)+".prefab",new Vector3(r.center.x,bottom-r.width*.25f,r.center.y),new Vector3(r.width*.85f,r.width*.6f,r.height*.85f),r.center.x*3,180);
   under.name="Island crag";
  }
  static void WallDressing(GameObject map,Rect r,float top,float bottom,string theme,ref int index) {
   if(theme=="snow") {
    // The valley sides: snowy cliffs beyond the play edge.
    foreach(var (a,b,n) in Sides(r)) {
     // Only the outward side: a 4 m deep cliff whose inner face sits on the play edge (the wall's inner side).
     if(Vector2.Dot(n,(a+b)/2)<=0||Vector2.Distance(a,b)<3)continue;
     float length=Vector2.Distance(a,b);var along=(b-a).normalized;
     for(float t=4;t<length+3.9f;t+=8)Fit(map,Generic+"Environment/SM_Gen_Env_Cliff_0"+((int)t%2+2)+".prefab",V(a+along*Mathf.Min(t,length)+n*.5f,top/2-1),new Vector3(9,top+2,4),Mathf.Atan2(n.x,n.y)*Mathf.Rad2Deg+180);
    }
    return;
   }
   // Ruins: broken brick walls, whole and destroyed pieces in turn, ivy here and there.
   foreach(var (a,b,n) in Sides(r)) {
    float length=Vector2.Distance(a,b);if(length<1.5f)continue;
    float h=top-Mathf.Max(bottom,0);int count=Mathf.Max(1,Mathf.RoundToInt(length/2.5f));float piece=length/count;var along=(b-a).normalized;float yaw=Mathf.Atan2(n.x,n.y)*Mathf.Rad2Deg;
    for(int i=0;i<count;i++,index++) {
     var mid=a+along*(piece*(i+.5f))-n*.12f;string kind=index%4==1?"Wall_Destroyed_01":index%4==3?"Wall_Destroyed_02":"Wall_01";
     Restyle(Fit(map,Base(kind),V(mid,h/2),new Vector3(piece,h,.24f),yaw),Brick);
     if(index%5==2)Fit(map,Generic+"Environment/SM_Gen_Env_Ivy_0"+(index%3+1)+".prefab",V(mid+n*.14f,1.6f),new Vector3(Mathf.Min(piece,2.2f),2.2f,.08f),yaw);
    }
   }
  }
  static void HouseDressing(GameObject map,PlanBlock b,string theme,int i) {
   var size=b.Size;var floor=b.Center-Vector3.up*size.y/2;
   if(theme=="snow"){Fit(map,Adventure+"Buildings/SM_Bld_Hut_01_Snow.prefab",b.Center+Vector3.up*.05f,size+new Vector3(.1f,.1f,.1f),(i%2)*180);
    var lantern=Put(map,Adventure+"Items/SM_Item_Lantern_02.prefab",floor+new Vector3(size.x/2+.2f,2.1f,-size.z/2-.2f),0,1.3f);lantern.name="SM_Item_Lantern_Hut";return;}
   if(theme=="ruins") {
    // A roofless ruin: broken brick walls round an overgrown top.
    var r=Rect.MinMaxRect(b.Center.x-size.x/2,b.Center.z-size.z/2,b.Center.x+size.x/2,b.Center.z+size.z/2);int index=i*3+1;
    WallDressing(map,r,b.Center.y+size.y/2,floor.y,"ruins",ref index);Top(map,r,b.Center.y+size.y/2,"ruins");
    Fit(map,Generic+"Environment/SM_Gen_Env_Ivy_Draped_01.prefab",floor+new Vector3(0,size.y-.5f,-size.z/2-.1f),new Vector3(size.x*.6f,1.4f,.4f),0);return;
   }
   Fit(map,Adventure+"Buildings/SM_Bld_Village_0"+(i%5+1)+".prefab",b.Center+Vector3.up*.02f,size+new Vector3(.08f,.04f,.08f),(i%4)*90);
  }
  static void DeckDressing(GameObject map,Rect r,float top,string theme) {
   bool alongZ=r.height>r.width;float span=alongZ?r.height:r.width,width=alongZ?r.width:r.height;float yaw=alongZ?0:90;
   var centre=new Vector3(r.center.x,top,r.center.y);
   if(width<2){Fit(map,Generic+"Environment/SM_Gen_Env_Log_01.prefab",centre+Vector3.down*.28f,alongZ?new Vector3(width,.56f,span+.6f):new Vector3(span+.6f,.56f,width),0);return;}
   if(width>4){Fit(map,Prop("StoneBlock_01"),centre+Vector3.down*.3f,new Vector3(r.width,.6f,r.height),0);Paving(map,r,top-.02f,"Bridge paving");return;}
   // Planks across the span on two stringers; rope posts along both edges (sky).
   Fit(map,Beam,centre+Vector3.down*.3f,alongZ?new Vector3(width-.3f,.3f,span):new Vector3(span,.3f,width-.3f),0);
   for(float t=.25f;t<span;t+=.5f){var at=alongZ?new Vector3(r.center.x,top-.04f,r.yMin+t):new Vector3(r.xMin+t,top-.04f,r.center.y);Fit(map,Beam,at,new Vector3(.44f,.08f,width),alongZ?90:0);}
   if(theme=="sky")for(float t=0;t<=span+.1f;t+=3)foreach(float s in new[]{-1f,1f}){var at=alongZ?new Vector3(r.center.x+s*(width/2-.08f),top,r.yMin+t):new Vector3(r.xMin+t,top,r.center.y+s*(width/2-.08f));Fit(map,Adventure+"Buildings/SM_Bld_FencePost_01.prefab",at+Vector3.up*.45f,new Vector3(.14f,.9f,.14f),0);}
  }
  static void ClimbDressing(GameObject map,PlanClimb c,string theme) {
   var frame=Quaternion.Euler(0,c.Yaw,0);
   if(!c.Ramp) {
    int steps=MapPlan.Steps(c.Rise);float run=c.Length/steps;string block=theme=="sky"?Beam:Prop("StoneBlock_01");
    for(int i=0;i<steps;i++){float h=(i+1)*c.Rise/steps;Fit(map,block,c.Center+frame*new Vector3(0,h/2,-c.Length/2+run*(i+.5f)),new Vector3(c.Width,h,run+.01f),c.Yaw);}
    if(theme=="snow")for(int i=1;i<steps;i+=3){float h=(i+1)*c.Rise/steps;CrossSurface(map,"Snow step",c.Center+frame*new Vector3(0,h+.008f,-c.Length/2+run*(i+.5f)),new Vector3(c.Width-.1f,.012f,run*.7f),Snow);}
    return;
   }
   float angle=Mathf.Atan2(c.Rise,c.Length)*Mathf.Rad2Deg;
   string surface=theme=="snow"?Env("Road_Straight_01_Snow"):theme=="sky"?Beam:Env("Road_Straight_01");
   Fit(map,surface,c.Center+frame*new Vector3(0,c.Rise/2+.03f,0),new Vector3(c.Width,.06f,Mathf.Sqrt(c.Length*c.Length+c.Rise*c.Rise)),c.Yaw,-angle);
   for(int k=1;k<Mathf.FloorToInt(c.Length);k++){float h=c.Rise*k/c.Length;Fit(map,Prop("StoneBlock_01"),c.Center+frame*new Vector3(0,h/2,-c.Length/2+k+.5f),new Vector3(c.Width-.04f,h,1),c.Yaw);}
  }

  static void SkyExtras(GameObject map) {
   var root=Group(map,"Sky");
   // A sea of cloud far below and banks drifting past the islands.
   CrossSurface(root,"Cloud sea",new Vector3(0,-60,0),new Vector3(600,.1f,600),new Color(.93f,.95f,.98f));
   for(int k=0;k<30;k++){float a=k*2.4f;float rad=85+(k%5)*16;var p=new Vector3(Mathf.Cos(a)*rad,-26-(k%4)*8,Mathf.Sin(a)*rad);var cloud=Put(root,Env("Cloud_0"+(k%5+1)),p,k*37,2.2f+(k%3)*.6f);cloud.name="Cloud";}
   for(int k=0;k<10;k++){float a=k*.63f+.3f;var p=new Vector3(Mathf.Cos(a)*170,26+(k%3)*8,Mathf.Sin(a)*160);Put(root,Generic+"Environment/SM_Gen_Env_Cloud_0"+(k%2+1)+".prefab",p,k*29,2);}
   foreach(var p in new[]{new Vector3(-4.6f,3.5f,-4.6f),new Vector3(4.6f,3.5f,4.6f),new Vector3(-42,3.5f,5.4f),new Vector3(42,3.5f,5.4f)}){var lantern=Put(root,Adventure+"Items/SM_Item_Lantern_01.prefab",p,0,1.4f);lantern.name="SM_Item_Lantern_Sky";}
   foreach(var p in new[]{new Vector3(-8,0,-38),new Vector3(8,0,-29),new Vector3(-13,0,0),new Vector3(13,0,-6),new Vector3(-44,0,10),new Vector3(44,0,10),new Vector3(-6,0,34)}){Put(root,Env("Bush_0"+((int)Mathf.Abs(p.x)%3+1)),p,p.z*9,.7f);Put(root,Env("Flower_0"+((int)Mathf.Abs(p.z)%2==0?1:3)),p+new Vector3(.7f,0,.3f),p.x,1.3f);}
  }
  static void SnowExtras(GameObject map) {
   var root=Group(map,"Snow");
   for(int k=0;k<40;k++){float a=k*Mathf.PI*2/40;var p=new Vector3(Mathf.Cos(a)*(46+(k%3)*5),k%2==0?6:2,Mathf.Sin(a)*(50+(k%4)*5));Put(root,Env("TreePine_0"+(k%3+1)+"_Snow"),p,k*31,2+(k%3)*.4f);}
   for(int k=0;k<6;k++){float a=k*1.05f+.4f;Put(root,Env("HillSnow_0"+(k%2+1)),new Vector3(Mathf.Cos(a)*95,-2,Mathf.Sin(a)*95),k*60,6);}
   Horizon(map,160,160,-1.2f);CrossSurface(root,"Snowfield",new Vector3(0,-1.1f,0),new Vector3(420,.05f,420),SnowShade);
   foreach(var p in new[]{new Vector3(-30,0,-32),new Vector3(30,0,-32),new Vector3(-31,3,-8),new Vector3(31,6,10),new Vector3(-31,6,34)})Put(root,Env("SnowPile_0"+((int)Mathf.Abs(p.x+p.z)%3+1)),p,p.x*5,1.2f);
   Put(root,Adventure+"Buildings/SM_Bld_Market_Snow_01.prefab",new Vector3(-30,0,-20),90,1);
  }
  static void RuinsExtras(GameObject map) {
   var root=Group(map,"Forest");
   // The chasm: cliff faces down both lips, a dark floor far below.
   foreach(float z in new[]{-8f,-2f})for(float x=-30;x<=30;x+=8)Fit(root,Generic+"Environment/SM_Gen_Env_Cliff_0"+((int)Mathf.Abs(x)%2+2)+".prefab",new Vector3(x,-7.6f,z+(z<-5?-1.4f:1.4f)),new Vector3(8.4f,15,3),z<-5?0:180);
   CrossSurface(root,"Chasm floor",new Vector3(0,-15,-5),new Vector3(70,.1f,8),new Color(.16f,.17f,.15f));
   // A dense wood all round the walls.
   for(int k=0;k<56;k++){float a=k*Mathf.PI*2/56;var p=new Vector3(Mathf.Cos(a)*(41+(k%3)*4),0,Mathf.Sin(a)*(40+(k%4)*4));Put(root,k%3==0?Env("TreePine_0"+(k%2+1)):Generic+"Environment/SM_Gen_Env_Tree_0"+(k%3+1)+".prefab",p,k*29,1.6f+(k%3)*.3f);}
   foreach(var p in new[]{new Vector3(-33,0,-20),new Vector3(-33,0,12),new Vector3(33,0,-26),new Vector3(33,0,4),new Vector3(-16.4f,0,26),new Vector3(16.4f,0,10),new Vector3(-20,0,-31),new Vector3(26,0,31)}) {
    Put(root,Generic+"Environment/SM_Gen_Env_Fern_0"+((int)Mathf.Abs(p.x)%2+1)+".prefab",p,p.z*7,1.2f);Put(root,Generic+"Environment/SM_Gen_Env_Grass_Tall_0"+((int)Mathf.Abs(p.z)%2+1)+".prefab",p+new Vector3(.6f,0,.4f),p.x,1.1f);
   }
   foreach(var p in new[]{new Vector3(-26,0,-31.2f),new Vector3(30,0,-8.8f),new Vector3(-33,0,30)})Put(root,Generic+"Environment/SM_Gen_Env_Log_01.prefab",p,90,.9f);
   foreach(var p in new[]{new Vector3(-33.2f,0,-4),new Vector3(33.2f,0,20),new Vector3(-12,0,31.2f)})Put(root,Env("Mushroom_01"),p,p.x,1.4f);
   foreach(var p in new[]{new Vector3(-3.6f,4,25.6f),new Vector3(5.2f,4,25.6f),new Vector3(-28,3.5f,23.4f),new Vector3(28,3.5f,15.4f)}){var lantern=Put(root,Adventure+"Items/SM_Item_Lantern_01.prefab",p,0,1.4f);lantern.name="SM_Item_Lantern_Ruins";}
   Put(root,Generic+"Props/SM_Gen_Prop_Statue_01.prefab",new Vector3(0,4,26.2f),180,1.3f);
   Horizon(map,150,150,-20);
   // Forest floor outside the walls (the chasm inside stays open).
   foreach(var (c,size) in new[]{(new Vector3(0,-.02f,-60),new Vector3(160,.02f,53)),(new Vector3(0,-.02f,60),new Vector3(160,.02f,53)),(new Vector3(-60,-.02f,0),new Vector3(49,.02f,67)),(new Vector3(60,-.02f,0),new Vector3(49,.02f,67))})CrossSurface(root,"Forest floor",c,size,new Color(.33f,.43f,.25f));
  }
 }
}
