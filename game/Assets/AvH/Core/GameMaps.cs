using System;
namespace AvH {
 /// <summary>
 /// Playable maps. The host picks one in the settings (<see cref="PlaytestValues.Map"/>); it takes effect
 /// at the next round start, and every viewer builds the map the session state names.
 /// </summary>
 public static class GameMaps {
  public const int Town=0, Warehouse=1, Crossing=2, Sky=3, Snow=4, Ruins=5, Count=6;
  public static string Name(int map) {
   switch(map){case Warehouse:return "2층 창고";case Crossing:return "모래 교차로";case Sky:return "하늘 섬";case Snow:return "눈 덮인 산골";case Ruins:return "숲속 성터";default:return "마을";}
  }
  /// <summary>Sky islands: a human who falls to the bottom becomes an animal (and restarts on the animals' island).</summary>
  public static bool FallTurnsAnimal(int map)=>map==Sky;
  public static int Clamp(int map)=>map<0||map>=Count?Town:map;
  /// <summary>
  /// Maps where the first animals leave from their own start when the chase begins instead of turning where they stand.
  /// On the crossing they return to the south plaza: humans have had the preparation time to reach the sites,
  /// and the animals must come up through the three lanes.
  /// </summary>
  public static bool AnimalsStartApart(int map)=>map>=Crossing;
  /// <summary>Where the index-th first animal starts on a map with <see cref="AnimalsStartApart"/>: a row at the south end of the plaza.</summary>
  public static WorldPosition AnimalStart(int map,int index) {
   float x=(index%6-2.5f)*3,row=index/6*1.5f;
   switch(map){case Sky:return new WorldPosition(x,1,38+row);case Snow:return new WorldPosition(x,1,-32.5f+row);case Ruins:return new WorldPosition(x,1,-30.5f+row);default:return new WorldPosition(x,1,-33.5f+row);}
  }
  /// <summary>
  /// Per-map balance on top of the host's settings, measured in bot matches with abilities on (target: animals win 40-60%).
  /// Sky islands end fast (a fall turns a human), so their chase is shorter; the crossing's three lanes and the ruins' long sight lines need a longer one;
  /// the town's spread-out streets and rooftop need two more first animals. The settings panel and the clock show the result.
  /// </summary>
  /// <summary>Per-map balance switch. Test assemblies turn it off so rule tests see the host settings as they are.</summary>
  public static bool PerMapBalance=true;
  public static float ChaseScale(int map){if(!PerMapBalance)return 1;switch(map){case Sky:return .75f;case Crossing:case Ruins:return 1.2f;default:return 1;}}
  public static int ExtraAnimals(int map)=>PerMapBalance&&map==Town?2:0;
  /// <summary>Chase length on this map for the host's round time.</summary>
  public static double ChaseSeconds(int map,double roundSeconds)=>Math.Round(roundSeconds*ChaseScale(map));
  /// <summary>First animals on this map: the host's count plus the map's extra, leaving at least one human.</summary>
  public static int FirstAnimals(int map,int initialAnimals,int players)=>Math.Max(1,Math.Min(players-1,initialAnimals+ExtraAnimals(map)));
  /// <summary>One line for the settings panel: what this map changes, or empty.</summary>
  public static string BalanceNote(int map) {
   string note="";if(ChaseScale(map)!=1)note+=$"추격 시간 ×{ChaseScale(map):0.##}";if(ExtraAnimals(map)>0)note+=(note.Length>0?" · ":"")+$"최초 동물 +{ExtraAnimals(map)}";
   return note.Length==0?"":$"{Name(map)} 맵 보정: {note}";
  }
  /// <summary>Round start position. Every map starts in an open ground-floor area: the warehouse south of the mezzanine, the crossing in its south plaza.</summary>
  public static WorldPosition Spawn(int map,int slot) {
   float x=(slot%4-1.5f)*3,z=(slot/4-1)*3;
   switch(map){case Warehouse:return new WorldPosition(x,1,z-3);case Crossing:return new WorldPosition(x,1,z-28);case Sky:return new WorldPosition(x,1,z-34);case Snow:return new WorldPosition(x,1,z-27);case Ruins:return new WorldPosition(x,1,z-26);default:return new WorldPosition(x,1,z);}
  }
 }
}
