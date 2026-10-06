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
  public static StartScreenLayout For(float width,float height,bool displayRows) {
   // Lifted so the local display rows fit under the join controls at 1280x720.
   float c=height/2-80;
   return new StartScreenLayout{Center=c,Title=new Rect(width/2-220,c-145,440,290),Connect=ConnectPanel(width,c,displayRows)};
  }
  /// <summary>The connection panel below a given anchor (the network draw routine's own frame).</summary>
  public static Rect ConnectPanel(float width,float center,bool displayRows)=>new Rect(width/2-280,center+65,560,275+(displayRows?DisplaySettingsMenu.Height:0));
 }
}
