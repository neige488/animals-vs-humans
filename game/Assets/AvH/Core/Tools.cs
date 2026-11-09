using System;
namespace AvH {
 /// <summary>
 /// Workshop tools humans shove animals with (no guns). Each round every human draws one at random
 /// (<see cref="PlaytestValues.Tool"/> = -1) or the host fixes one for everybody.
 /// All of them push without harm; friends are shoved too while <see cref="PlaytestValues.FriendlyPush"/> is on.
 /// </summary>
 public enum ToolKind { Cork, Glove, Disc, Blower, Cushion }
 public static class Tools {
  public const int Count=5;
  public const int Random=-1;
  public static string Name(ToolKind tool) {
   switch(tool){
    case ToolKind.Glove:return "펀치 글러브";
    case ToolKind.Disc:return "고무 원반 발사기";
    case ToolKind.Blower:return "송풍기";
    case ToolKind.Cushion:return "쿠션 팔";
    default:return "코르크 팝퍼";
   }
  }
  /// <summary>Short noun for the hit feed: "민지의 글러브".</summary>
  public static string Short(ToolKind tool) {
   switch(tool){case ToolKind.Glove:return "글러브";case ToolKind.Disc:return "원반";case ToolKind.Blower:return "송풍기";case ToolKind.Cushion:return "쿠션";default:return "코르크";}
  }
  /// <summary>Time between uses, as a multiple of the shot interval (FireInterval).</summary>
  public static double IntervalScale(ToolKind tool) {
   switch(tool){case ToolKind.Glove:return 3.4;case ToolKind.Disc:return 2;case ToolKind.Blower:return .4;case ToolKind.Cushion:return 2.7;default:return 1;}
  }
  /// <summary>Shooters and the blower's tank empty the magazine; the glove and the cushion never need reloading.</summary>
  public static bool UsesAmmo(ToolKind tool)=>tool==ToolKind.Cork||tool==ToolKind.Disc||tool==ToolKind.Blower;
  /// <summary>How long one use is shown (punch out and back, cushion pop, gust).</summary>
  public static double ActionSeconds(ToolKind tool) {
   switch(tool){case ToolKind.Glove:return .35;case ToolKind.Cushion:return .4;case ToolKind.Blower:return .2;default:return .1;}
  }
  /// <summary>Reach (m): shots fly the shot range; the rest work up close.</summary>
  public static float Range(ToolKind tool,PlaytestValues rules) {
   switch(tool){case ToolKind.Glove:return GloveReach;case ToolKind.Cushion:return CushionReach;case ToolKind.Blower:return BlowerReach;default:return rules.BubbleRange;}
  }
  public const float GloveReach=1.9f,CushionReach=2.6f,BlowerReach=6f;
  /// <summary>Push strength as a multiple of PushForce, per use (the blower per gust).</summary>
  public static float PushScale(ToolKind tool) {
   switch(tool){case ToolKind.Glove:return 3;case ToolKind.Disc:return 1.2f;case ToolKind.Blower:return .22f;case ToolKind.Cushion:return 1.6f;default:return 1;}
  }
  /// <summary>Upward kick added to the push (m/s): the glove launches.</summary>
  public static float Lift(ToolKind tool) {
   switch(tool){case ToolKind.Glove:return 8;case ToolKind.Cushion:return 2.2f;case ToolKind.Blower:return .15f;default:return 1.5f;}
  }
  /// <summary>Gusts never stun (they would lock an animal in place); everything else staggers an animal like a cork.</summary>
  public static bool Stuns(ToolKind tool)=>tool!=ToolKind.Blower;
  /// <summary>Disc bounces off walls and floors this many times before it stops.</summary>
  public const int DiscBounces=2;
 }
}
