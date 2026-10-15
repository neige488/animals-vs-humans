using NUnit.Framework;
namespace AvH.Tests {
 // The start screen's title panel (nickname) and the LAN/VPN connection panel never overlap.
 public class StartScreenLayoutTests {
  [Test] public void TitleAndConnectionPanelsNeverOverlapAndFitCommonWindows() {
   foreach(float height in new[]{480f,540f,600f,640f,720f,768f,800f,900f,1080f,1440f})
   foreach(bool rows in new[]{true,false}) {
    var layout=StartScreenLayout.For(1280,height,rows);
    Assert.LessOrEqual(layout.Title.yMax,layout.Connect.yMin,$"{height}px: the nickname panel overlaps the connection panel");
    Assert.AreEqual(layout.Connect,StartScreenLayout.ConnectPanel(1280,layout.Center,rows),"Both draw routines share one frame");
    Assert.IsFalse(layout.Title.Overlaps(StartScreenLayout.StatusPanel(1280,layout.Center)),$"{height}px: the connecting notice covers the nickname panel");
    Assert.GreaterOrEqual(layout.Title.yMin,0,$"{height}px: the title is cut off at the top");
    if(height>=600)Assert.LessOrEqual(layout.Connect.yMax,height,$"{height}px: the connection panel is cut off at the bottom");
   }
  }
 }
}
