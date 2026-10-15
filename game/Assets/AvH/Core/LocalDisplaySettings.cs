using System;
using System.IO;
using System.Xml.Serialization;
namespace AvH {
 public enum GraphicsQuality { Low, Medium, High }
 [Serializable] public sealed class DisplayValues {
  public GraphicsQuality Quality=GraphicsQuality.High;
  public bool ScreenShake=true;
 }
 /// <summary>
 /// This PC's display preferences (graphics quality, screen shake). Any participant may change them;
 /// they live in their own file, apart from the host-only <see cref="PlaytestValues"/>.
 /// Every change applies at once and is saved; a failed save keeps the choice for this run only.
 /// </summary>
 public sealed class LocalDisplaySettings {
  public const string TemporaryNotice="이번 실행에만 적용됩니다";
  readonly string path;DisplayValues values=new DisplayValues();
  public GraphicsQuality Quality=>values.Quality;
  public bool ScreenShake=>values.ScreenShake;
  /// <summary>Short notice after a failed save; null once saving works.</summary>
  public string Notice {get;private set;}
  /// <summary>Increments on every change so presenters can re-apply.</summary>
  public int Version {get;private set;}
  public LocalDisplaySettings(string file) {
   path=file;if(path==null||!File.Exists(path))return;
   try {using(var reader=File.OpenRead(path)){
    var loaded=(DisplayValues)new XmlSerializer(typeof(DisplayValues)).Deserialize(reader);
    if(loaded!=null&&Enum.IsDefined(typeof(GraphicsQuality),loaded.Quality))values=loaded;
   }}catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException){values=new DisplayValues();}
  }
  public bool SetQuality(GraphicsQuality quality){if(!Enum.IsDefined(typeof(GraphicsQuality),quality))return false;values.Quality=quality;return Changed();}
  public bool SetScreenShake(bool enabled){values.ScreenShake=enabled;return Changed();}
  bool Changed() {
   Version++;if(path==null){Notice=null;return true;}
   try {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
    using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(DisplayValues)).Serialize(stream,values);stream.Flush(true);}
    if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
    Notice=null;return true;
   }catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException){Notice=TemporaryNotice;return false;}
  }
 }
}
