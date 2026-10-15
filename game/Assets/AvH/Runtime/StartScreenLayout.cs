using UnityEngine;
namespace AvH {
 /// <summary>
 /// Vertical placement of the start screen: the title panel (name, nickname entry) above the
 /// "같은 LAN 또는 기존 VPN에서 연결" panel (host address, room code and, optionally, the local display rows).
 /// <see cref="Center"/> is the anchor both draw routines position their controls from.
 /// </summary>
 public struct StartScreenLayout {
  public float Center;
  public Rect Title,Connect;
  const float TitleAbove=145,TitleBelow=50,Margin=4;
  public static StartScreenLayout For(float width,float height,bool displayRows) {
   // The title panel ends under the nickname hint (c+50); the connection panel starts at c+65.
   // Both are centred together and never pushed above the top edge; a window too short for both clips the bottom only.
   float span=TitleAbove+ConnectPanel(width,0,displayRows).yMax;
   float c=Mathf.Max(TitleAbove+Margin,(height-span)/2+TitleAbove);
   return new StartScreenLayout{Center=c,Title=new Rect(width/2-220,c-TitleAbove,440,TitleAbove+TitleBelow),Connect=ConnectPanel(width,c,displayRows)};
  }
  /// <summary>Connecting/failed/interrupted notice: takes the connection panel's place, under the title.</summary>
  public static Rect StatusPanel(float width,float center)=>new Rect(width/2-280,center+65,560,230);
  /// <summary>The connection panel below a given anchor (the network draw routine's own frame).</summary>
  public static Rect ConnectPanel(float width,float center,bool displayRows)=>new Rect(width/2-280,center+65,560,275+(displayRows?DisplaySettingsMenu.Height:0));
 }
}
