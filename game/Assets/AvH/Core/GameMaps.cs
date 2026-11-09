namespace AvH {
 /// <summary>
 /// Playable maps. The host picks one in the settings (<see cref="PlaytestValues.Map"/>); it takes effect
 /// at the next round start, and every viewer builds the map the session state names.
 /// </summary>
 public static class GameMaps {
  public const int Town=0, Warehouse=1, Count=2;
  public static string Name(int map)=>map==Warehouse?"2층 창고":"마을";
  public static int Clamp(int map)=>map<0||map>=Count?Town:map;
  /// <summary>Round start position. Both maps start in an open ground-floor area; the warehouse one sits south of the mezzanine.</summary>
  public static WorldPosition Spawn(int map,int slot) {
   float x=(slot%4-1.5f)*3,z=(slot/4-1)*3;
   return map==Warehouse?new WorldPosition(x,1,z-3):new WorldPosition(x,1,z);
  }
 }
}
