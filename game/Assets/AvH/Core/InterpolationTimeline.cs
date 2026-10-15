using System;
using System.Collections.Generic;
namespace AvH {
 /// <summary>
 /// Plays timestamped host frames back on a local clock that runs a little behind the host (snapshot buffer
 /// interpolation). The playback clock never jumps for ordinary jitter: it speeds up or slows down by at most
 /// <see cref="MaxRateChange"/> to keep <see cref="Delay"/> behind the estimated host time, holds the newest frame
 /// when the stream stalls, and only re-synchronises after a gap longer than <see cref="ResyncSeconds"/>.
 /// </summary>
 public sealed class InterpolationTimeline<T> where T : class {
  /// <summary>Playback delay behind the estimated host time. Two 20 Hz frame intervals: one to interpolate, one for jitter.</summary>
  public const double Delay=.1;
  public const double MaxRateChange=.25,ResyncSeconds=1;
  const int Capacity=32;
  readonly List<(double time,T frame)> frames=new List<(double,T)>();
  double offset,render,lastLocal;bool clockKnown,playing;
  public T Latest=>frames.Count==0?null:frames[frames.Count-1].frame;
  public int Count=>frames.Count;
  public void Clear(){frames.Clear();clockKnown=playing=false;}
  /// <summary>Adds a frame stamped with host time, received at local time. Duplicates and stale frames are ignored.</summary>
  public void Add(double hostTime,double localTime,T frame) {
   if(frame==null||double.IsNaN(hostTime)||double.IsInfinity(hostTime))return;
   if(frames.Count>0) {
    double newest=frames[frames.Count-1].time;
    if(hostTime<=newest){if(newest-hostTime<ResyncSeconds)return;Clear();}
   }
   frames.Add((hostTime,frame));if(frames.Count>Capacity)frames.RemoveAt(0);
   // Host-to-local clock offset: follow the fastest arrival at once, drift slowly toward later ones.
   double sample=localTime-hostTime;
   if(!clockKnown||sample<offset){offset=sample;clockKnown=true;}else offset+=(sample-offset)*.05;
  }
  /// <summary>The two frames around the playback time and the 0..1 blend between them.</summary>
  public bool Sample(double localTime,out T from,out T to,out float t) {
   from=to=null;t=0;if(frames.Count==0)return false;
   double target=localTime-offset-Delay;
   if(!playing){render=Math.Min(target,frames[frames.Count-1].time);playing=true;}
   else {
    double dt=Math.Max(0,localTime-lastLocal),error=target-(render+dt);
    if(Math.Abs(error)>ResyncSeconds)render=target;
    else render+=dt*(1+Math.Max(-MaxRateChange,Math.Min(MaxRateChange,error*2)));
   }
   lastLocal=localTime;
   render=Math.Max(frames[0].time,Math.Min(render,frames[frames.Count-1].time));
   int index=frames.Count-1;while(index>0&&frames[index].time>render)index--;
   // Frames older than the one being played from are no longer needed.
   if(index>0){frames.RemoveRange(0,index);index=0;}
   from=frames[index].frame;
   if(index+1>=frames.Count){to=from;t=1;return true;}
   to=frames[index+1].frame;double span=frames[index+1].time-frames[index].time;
   t=span<=0?1:(float)Math.Max(0,Math.Min(1,(render-frames[index].time)/span));
   return true;
  }
 }
}
