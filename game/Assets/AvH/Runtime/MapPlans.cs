using UnityEngine;
namespace AvH {
 /// <summary>
 /// The three planned maps (metres, y up). Every layout is original.
 /// Sky islands: islands over nothing joined by narrow bridges; a human who falls becomes an animal.
 /// Snowy hill village: four terraces climbing north to a summit lodge; animals come up from the gate below.
 /// Forest ruins: a castle bailey and keep north of a chasm crossed by three bridges.
 /// </summary>
 public static class MapPlans {
  static MapPlan sky,snow,ruins;
  public static MapPlan For(int map)=>map==GameMaps.Sky?Sky:map==GameMaps.Snow?Snow:map==GameMaps.Ruins?Ruins:null;
  public static MapPlan Sky=>sky??=BuildSky();
  public static MapPlan Snow=>snow??=BuildSnow();
  public static MapPlan Ruins=>ruins??=BuildRuins();
  /// <summary>Below this height a body has fallen off the sky islands.</summary>
  public const float SkyFallLine=-8;

  // ---------- sky islands ----------
  //            NW ──────── A (animals) ──────── NE
  //            │            │ narrow             │
  //            W ═══════ C (platform) ═══════ E
  //            │            │                    │
  //            SW ──────── S (humans) ──────── SE
  static MapPlan BuildSky() {
   var p=new MapPlan{Root="Sky islands",Theme="sky",Grid=new RouteGrid(-47,-42,.75f,126,116,1)};
   void Island(float x0,float x1,float z0,float z1)=>p.Slab(PlanKind.Rock,x0,x1,z0,z1,-6,0);
   void Bridge(float x0,float x1,float z0,float z1)=>p.Slab(PlanKind.Deck,x0,x1,z0,z1,-.4f,0);
   Island(-10,10,-41,-27);Island(-15,15,-15,15);Island(-46,-30,-4,12);Island(30,46,-4,12);Island(-9,9,32,44);
   Island(-42,-34,-38,-30);Island(34,42,-38,-30);Island(-42,-34,34,42);Island(34,42,34,42);
   Bridge(-1.5f,1.5f,-27,-15);Bridge(-1.25f,1.25f,15,32);Bridge(-30,-15,.75f,3.25f);Bridge(15,30,.75f,3.25f);
   foreach(float s in new[]{-1f,1f}) {
    Bridge(Mathf.Min(s*10,s*34),Mathf.Max(s*10,s*34),-35.25f,-32.75f);
    Bridge(Mathf.Min(s*36.75f,s*39.25f),Mathf.Max(s*36.75f,s*39.25f),-30,-4);
    Bridge(Mathf.Min(s*36.75f,s*39.25f),Mathf.Max(s*36.75f,s*39.25f),12,34);
    Bridge(Mathf.Min(s*9,s*34),Mathf.Max(s*9,s*34),36.75f,39.25f);
    // A watchtower on each side island, stair from the south.
    p.Slab(PlanKind.Platform,Mathf.Min(s*39,s*45),Mathf.Max(s*39,s*45),5,11,0,3.5f);p.Stair(new Vector3(s*42,0,5-MapPlan.StairLength(3.5f)/2),0,3.5f);
   }
   // The central platform: a stair from the spawn side, a ramp from the animals' bridge.
   p.Slab(PlanKind.Platform,-5,5,-5,5,0,3.5f);p.Stair(new Vector3(0,0,-5-MapPlan.StairLength(3.5f)/2),0,3.5f);p.Ramp(new Vector3(0,0,9.5f),180,3.5f,9);
   foreach(var t in new[]{new Vector3(-12,0,-12),new Vector3(12,0,12),new Vector3(-12,0,11),new Vector3(-7,0,42),new Vector3(7,0,42),new Vector3(-44,0,-2),new Vector3(44,0,-2)})p.Box(PlanKind.Tree,t,new Vector3(.6f,4,.6f));
   p.Crates.Add((new Vector3(-9,0,-9),1));p.Crates.Add((new Vector3(9,0,8),2));p.Crates.Add((new Vector3(-33,0,8),1));p.Crates.Add((new Vector3(33,0,8),1));p.Crates.Add((new Vector3(-7.5f,0,-30),1));
   p.LightCrates.AddRange(new[]{new Vector3(0,0,-21),new Vector3(-22,0,2),new Vector3(22,0,2),new Vector3(0,0,24),new Vector3(2,3.5f,2),new Vector3(-2,3.5f,-2),new Vector3(-38,0,-34),new Vector3(38,0,-34),new Vector3(-38,0,38),new Vector3(38,0,38)});
   p.Shelters=new[]{new Vector3(0,3.5f,0),new Vector3(-42,3.5f,8),new Vector3(42,3.5f,8)};
   p.Recovery=new[]{new Vector3(0,1,-34),new Vector3(0,1,-10),new Vector3(-36,1,0),new Vector3(36,1,0)};
   return p;
  }

  // ---------- snowy hill village ----------
  // z 22..36 summit (y 9) ─ lodge, two watchtowers
  // z  8..36 terrace 2 (y 6)
  // z -10..36 terrace 1 (y 3)
  // z -34..  gate and square (y 0): everyone starts here
  static MapPlan BuildSnow() {
   var p=new MapPlan{Root="Snowy hill village",Theme="snow",Grid=new RouteGrid(-45,-45,.75f,121,121,1,18)};
   p.Slab(PlanKind.Ground,-32,32,-34,36,-1,0);
   p.Slab(PlanKind.Terrace,-32,32,-10,36,0,3);p.Slab(PlanKind.Terrace,-32,32,8,36,0,6);p.Slab(PlanKind.Terrace,-14,14,22,36,0,9);
   p.Slab(PlanKind.Wall,-33.5f,-32,-35.5f,37.5f,0,14);p.Slab(PlanKind.Wall,32,33.5f,-35.5f,37.5f,0,14);p.Slab(PlanKind.Wall,-32,32,-35.5f,-34,0,4);p.Slab(PlanKind.Wall,-32,32,36,37.5f,0,14);
   float l3=MapPlan.StairLength(3);
   p.Stair(new Vector3(-22,0,-10-l3/2),0,3);p.Stair(new Vector3(22,0,-10-l3/2),0,3);p.Ramp(new Vector3(0,0,-14.5f),0,3,9,4);
   p.Stair(new Vector3(-12,3,8-l3/2),0,3);p.Stair(new Vector3(16,3,8-l3/2),0,3);p.Ramp(new Vector3(27,3,3.5f),0,3,9);
   p.Stair(new Vector3(0,6,22-l3/2),0,3);p.Ramp(new Vector3(-18.5f,6,30),90,3,9);
   p.Slab(PlanKind.Platform,-30,-24,26,32,0,9);p.Stair(new Vector3(-27,6,26-l3/2),0,3);
   p.Slab(PlanKind.Platform,20,26,26,32,0,9);p.Stair(new Vector3(23,6,26-l3/2),0,3);
   foreach(var (x0,x1,z0,z1,b) in new[]{(-28f,-22f,-30f,-24f,0f),(22f,28f,-30f,-24f,0f),(-12f,-7f,-20f,-15f,0f),(7f,12f,-20f,-15f,0f),(-28f,-22f,-4f,2f,3f),(4f,10f,-6f,0f,3f),(18f,24f,-2f,4f,3f),(-8f,-2f,10f,16f,6f),(6f,12f,11f,17f,6f),(-6f,6f,30f,36f,9f)})
    p.Slab(PlanKind.House,x0,x1,z0,z1,0,b+(b>=9?4.5f:5));
   p.Rail(new Vector3(-14,9,22.15f),new Vector3(-9,9,22.15f));p.Rail(new Vector3(-7,9,22.15f),new Vector3(-1.2f,9,22.15f));p.Rail(new Vector3(1.2f,9,22.15f),new Vector3(14,9,22.15f));
   foreach(var t in new[]{new Vector3(-30,0,-12),new Vector3(30,0,-14),new Vector3(-18,0,-32),new Vector3(18,0,-32),new Vector3(-30,3,6),new Vector3(31,3,-8),new Vector3(-31,6,18),new Vector3(31,6,20)})p.Box(PlanKind.Tree,t,new Vector3(.7f,4,.7f));
   p.Crates.Add((new Vector3(-6,0,-14),1));p.Crates.Add((new Vector3(14,0,-26),1));p.Crates.Add((new Vector3(-2,3,2),1));p.Crates.Add((new Vector3(12.5f,3,-4),1));p.Crates.Add((new Vector3(20,6,14),2));
   p.LightCrates.AddRange(new[]{new Vector3(-18,3,-9),new Vector3(10,3,-9),new Vector3(-20,6,9),new Vector3(4,6,9),new Vector3(-6,9,27),new Vector3(8,9,26),new Vector3(-4,0,-28),new Vector3(6,0,-22)});
   p.Shelters=new[]{new Vector3(0,9,26),new Vector3(-27,9,29),new Vector3(23,9,29)};
   p.Recovery=new[]{new Vector3(0,1,-26),new Vector3(-16,4,0),new Vector3(14,7,12),new Vector3(0,10,26)};
   return p;
  }

  // ---------- forest ruins ----------
  //  z 7..30  the bailey: broken walls round the keep (y 4), towers outside it
  //  z -8..-2 the chasm: west timber, centre stone and east log bridges
  //  z -32..-8 the forest road: everyone starts here
  static MapPlan BuildRuins() {
   var p=new MapPlan{Root="Forest ruins",Theme="ruins"};
   p.Slab(PlanKind.Ground,-34,34,-32,-8,-2,0);p.Slab(PlanKind.Ground,-34,34,-2,32,-2,0);
   p.Slab(PlanKind.Wall,-35.5f,-34,-33.5f,33.5f,-2,5);p.Slab(PlanKind.Wall,34,35.5f,-33.5f,33.5f,-2,5);p.Slab(PlanKind.Wall,-34,34,-33.5f,-32,-2,5);p.Slab(PlanKind.Wall,-34,34,32,33.5f,-2,5);
   p.Slab(PlanKind.Deck,-25.25f,-22.75f,-8,-2,-.5f,0);p.Slab(PlanKind.Deck,-2.5f,2.5f,-8,-2,-.6f,0);p.Slab(PlanKind.Deck,21.2f,22.8f,-8,-2,-.4f,0);
   // Bailey walls with a gate, three breaches.
   foreach(var (x0,x1,z0,z1) in new[]{(-18f,-14f,6f,7f),(-10f,-2f,6f,7f),(2f,18f,6f,7f),(-18f,18f,29f,30f),(-18f,-17f,7f,16f),(-18f,-17f,20f,29f),(17f,18f,7f,22f),(17f,18f,26f,29f)})p.Slab(PlanKind.Wall,x0,x1,z0,z1,0,4);
   // The keep: a stair from the gate, a rubble ramp from the west breach.
   p.Slab(PlanKind.Platform,-6,6,17,27,0,4);p.Stair(new Vector3(3,0,17-MapPlan.StairLength(4)/2),0,4);p.Ramp(new Vector3(-11.25f,0,24),90,4,10.5f);
   p.Rail(new Vector3(-6,4,17.15f),new Vector3(-3,4,17.15f));p.Rail(new Vector3(-1,4,17.15f),new Vector3(1.8f,4,17.15f));p.Rail(new Vector3(4.2f,4,17.15f),new Vector3(6,4,17.15f));
   p.Slab(PlanKind.Platform,-31,-25,18,24,0,3.5f);p.Stair(new Vector3(-28,0,18-MapPlan.StairLength(3.5f)/2),0,3.5f);
   p.Slab(PlanKind.Platform,25,31,10,16,0,3.5f);p.Stair(new Vector3(28,0,10-MapPlan.StairLength(3.5f)/2),0,3.5f);
   foreach(var (x0,x1,z0,z1) in new[]{(-28f,-22f,-28f,-22f),(20f,26f,-26f,-20f),(-14f,-9f,-20f,-15f),(9f,14f,-22f,-17f),(-31f,-25f,1f,7f),(24f,30f,22f,28f)})p.Slab(PlanKind.House,x0,x1,z0,z1,0,3.5f);
   foreach(var (c,s) in new[]{(new Vector3(-6,.6f,-24),new Vector3(2,1.2f,2)),(new Vector3(6,.6f,-14),new Vector3(2.5f,1.2f,1.6f)),(new Vector3(-20,.6f,0),new Vector3(2,1.2f,2.2f)),(new Vector3(12,.6f,2),new Vector3(1.8f,1.2f,2))})p.Box(PlanKind.Rubble,c,s);
   foreach(var t in new[]{new Vector3(-30,0,-14),new Vector3(30,0,-12),new Vector3(-8,0,-30),new Vector3(14,0,-30),new Vector3(-30,0,28),new Vector3(-10,0,2),new Vector3(8,0,1),new Vector3(32,0,30)})p.Box(PlanKind.Tree,t,new Vector3(.7f,4.5f,.7f));
   p.Crates.Add((new Vector3(-6,0,-18),1));p.Crates.Add((new Vector3(16,0,-12),2));p.Crates.Add((new Vector3(-22,0,12),1));p.Crates.Add((new Vector3(10,0,12),1));p.Crates.Add((new Vector3(-10,0,10),1));
   p.LightCrates.AddRange(new[]{new Vector3(-24,0,-5),new Vector3(1,0,-5),new Vector3(-3,4,24),new Vector3(3,4,25),new Vector3(-14,0,0),new Vector3(14,0,-10),new Vector3(-20,0,-26),new Vector3(22,0,-14)});
   p.Shelters=new[]{new Vector3(0,4,22),new Vector3(-28,3.5f,21),new Vector3(28,3.5f,13)};
   p.Recovery=new[]{new Vector3(-20,1,-18),new Vector3(20,1,-18),new Vector3(0,1,-14),new Vector3(0,1,4),new Vector3(-24,1,4),new Vector3(24,1,4)};
   return p;
  }
 }
}
