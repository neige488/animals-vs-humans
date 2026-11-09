namespace AvH {
 /// <summary>
 /// Playable maps. The host picks one in the settings (<see cref="PlaytestValues.Map"/>); it takes effect
 /// at the next round start, and every viewer builds the map the session state names.
 /// </summary>
 public static class GameMaps {
  public const int Town=0, Warehouse=1, Crossing=2, Count=3;
  public static string Name(int map)=>map==Warehouse?"2층 창고":map==Crossing?"모래 교차로":"마을";
  public static int Clamp(int map)=>map<0||map>=Count?Town:map;
  /// <summary>
  /// Maps where the first animals leave from their own start when the chase begins instead of turning where they stand.
  /// On the crossing they return to the south plaza: humans have had the preparation time to reach the sites,
  /// and the animals must come up through the three lanes.
  /// </summary>
  public static bool AnimalsStartApart(int map)=>map==Crossing;
  /// <summary>Where the index-th first animal starts on a map with <see cref="AnimalsStartApart"/>: a row at the south end of the plaza.</summary>
  public static WorldPosition AnimalStart(int map,int index)=>new WorldPosition((index%6-2.5f)*3,1,-33.5f+index/6*1.5f);
  /// <summary>Round start position. Every map starts in an open ground-floor area: the warehouse south of the mezzanine, the crossing in its south plaza.</summary>
  public static WorldPosition Spawn(int map,int slot) {
   float x=(slot%4-1.5f)*3,z=(slot/4-1)*3;
   return map==Warehouse?new WorldPosition(x,1,z-3):map==Crossing?new WorldPosition(x,1,z-28):new WorldPosition(x,1,z);
  }
 }
}
