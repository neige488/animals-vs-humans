using UnityEngine;
namespace AvH {
 /// <summary>
 /// The two local display rows shared by the start screen and the Esc menu:
 /// 그래픽 품질 [낮음][보통][높음] · 화면 흔들림 [켜기][끄기]. The selected choice carries ■.
 /// Every click applies at once and saves on this PC; a failed save shows a short notice and play continues.
 /// </summary>
 public sealed class DisplaySettingsMenu {
  public const float RowHeight=34,Height=RowHeight*2+30;
  static readonly string[] qualities={"낮음","보통","높음"};
  float noticeUntil;
  public void Draw(Rect area,LocalDisplaySettings display,GUIStyle label) {
   if(display==null)return;
   var left=new GUIStyle(label){alignment=TextAnchor.MiddleLeft,fontSize=18};
   float labelWidth=130,buttons=area.width-labelWidth;
   GUI.Label(new Rect(area.x,area.y,labelWidth,RowHeight-4),"그래픽 품질",left);
   for(int i=0;i<3;i++){var q=(GraphicsQuality)i;if(GUI.Button(new Rect(area.x+labelWidth+i*buttons/3,area.y,buttons/3-6,RowHeight-4),(display.Quality==q?"■":"")+qualities[i]))Result(display.SetQuality(q));}
   float y=area.y+RowHeight;
   GUI.Label(new Rect(area.x,y,labelWidth,RowHeight-4),"화면 흔들림",left);
   if(GUI.Button(new Rect(area.x+labelWidth,y,buttons/2-6,RowHeight-4),(display.ScreenShake?"■":"")+"켜기"))Result(display.SetScreenShake(true));
   if(GUI.Button(new Rect(area.x+labelWidth+buttons/2,y,buttons/2-6,RowHeight-4),(!display.ScreenShake?"■":"")+"끄기"))Result(display.SetScreenShake(false));
   if(display.Notice!=null&&Time.unscaledTime<noticeUntil)GUI.Label(new Rect(area.x,y+RowHeight,area.width,26),display.Notice,new GUIStyle(left){fontSize=15});
  }
  void Result(bool saved){if(!saved)ShowNotice();}
  /// <summary>Shows the current save notice for a few seconds.</summary>
  public void ShowNotice(){noticeUntil=Time.unscaledTime+4;}
 }
}
