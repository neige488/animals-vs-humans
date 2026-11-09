using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Development-only capture of what the window shows (IMGUI included) and what the listener hears.
 /// Frames are JPEGs with real timestamps (concat list for ffmpeg), audio is one WAV of the final mix.
 /// </summary>
 public sealed class SessionRecorder : MonoBehaviour {
  string folder;float interval;double started,next;int frames;bool stopped;
  readonly List<double> stamps=new List<double>();
  AudioTap tap;
  public static SessionRecorder Begin(GameObject host,string folder,float fps) {
   var recorder=host.AddComponent<SessionRecorder>();recorder.folder=folder;recorder.interval=1f/Mathf.Max(1,fps);
   Directory.CreateDirectory(Path.Combine(folder,"frames"));recorder.started=Time.realtimeSinceStartupAsDouble;
   var listener=FindAnyObjectByType<AudioListener>();if(listener!=null){recorder.tap=listener.gameObject.AddComponent<AudioTap>();recorder.tap.Begin();}
   recorder.StartCoroutine(recorder.Capture());return recorder;
  }
  IEnumerator Capture() {
   var end=new WaitForEndOfFrame();
   while(!stopped) {
    yield return end;double now=Time.realtimeSinceStartupAsDouble;if(now<next)continue;next=now+interval;
    var texture=ScreenCapture.CaptureScreenshotAsTexture();
    File.WriteAllBytes(Path.Combine(folder,"frames",$"f{frames:00000}.jpg"),texture.EncodeToJPG(82));Destroy(texture);
    stamps.Add(now-started);frames++;
   }
  }
  /// <summary>Stops capturing and writes frames.txt (ffmpeg concat with durations) and audio.wav.</summary>
  public void Finish() {
   if(stopped)return;stopped=true;
   using(var w=new StreamWriter(Path.Combine(folder,"frames.txt"))) {
    for(int i=0;i<stamps.Count;i++){w.WriteLine($"file 'frames/f{i:00000}.jpg'");double d=i+1<stamps.Count?stamps[i+1]-stamps[i]:interval;w.WriteLine("duration "+d.ToString("F4",CultureInfo.InvariantCulture));}
    if(stamps.Count>0)w.WriteLine($"file 'frames/f{stamps.Count-1:00000}.jpg'");
   }
   tap?.Write(Path.Combine(folder,"audio.wav"));
  }
 }
 /// <summary>Copies the listener's final mix (presentation is unaffected).</summary>
 public sealed class AudioTap : MonoBehaviour {
  readonly List<float> samples=new List<float>(48000*2*60);int channels=2,rate;bool on;readonly object gate=new object();
  public void Begin(){rate=AudioSettings.outputSampleRate;on=true;}
  void OnAudioFilterRead(float[] data,int count){if(!on)return;lock(gate){channels=count;samples.AddRange(data);}}
  public void Write(string path) {
   lock(gate) {
    on=false;
    using(var w=new BinaryWriter(File.Create(path))) {
     int bytes=samples.Count*2;
     w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+bytes);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
     w.Write(16);w.Write((short)1);w.Write((short)channels);w.Write(rate);w.Write(rate*channels*2);w.Write((short)(channels*2));w.Write((short)16);
     w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);
     foreach(var s in samples)w.Write((short)(Mathf.Clamp(s,-1,1)*32767));
    }
    samples.Clear();
   }
  }
 }
}
