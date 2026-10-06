using System;
using System.Collections.Generic;
using NUnit.Framework;
namespace AvH.Tests {
 // Remote bodies play host frames back on a slightly delayed, steadily advancing clock.
 public class InterpolationTests {
  const double Send=.05,Speed=5;
  // A host frame every 50 ms (position = 5 m/s × host time); TCP keeps order but arrivals bunch and stall.
  static List<(double arrival,double host)> Jittered(int seed,double seconds,double jitter) {
   var random=new Random(seed);var result=new List<(double,double)>();double last=0;
   for(double host=0;host<seconds;host+=Send){double arrival=Math.Max(last,host+.02+random.NextDouble()*jitter);result.Add((arrival,host));last=arrival;}
   return result;
  }
  static double Position(InterpolationTimeline<double[]> timeline,double now) {
   Assert.IsTrue(timeline.Sample(now,out var from,out var to,out float t));
   return from[0]+(to[0]-from[0])*t;
  }
  [Test] public void JitteredArrivalsPlayBackSmoothlyAboutOneFrameIntervalBehind() {
   var timeline=new InterpolationTimeline<double[]>();var frames=Jittered(7,8,.045);int next=0;
   double previous=double.NaN,dt=1/60.0,maxLag=0,minLag=double.MaxValue;
   for(double now=0;now<8;now+=dt) {
    while(next<frames.Count&&frames[next].arrival<=now){timeline.Add(frames[next].host,now,new[]{frames[next].host*Speed});next++;}
    if(next==0)continue;
    double position=Position(timeline,now);
    if(now>1) {
     double step=position-previous;
     Assert.GreaterOrEqual(step,-1e-9,"Never moves backwards at "+now);
     Assert.LessOrEqual(step,Speed*dt*1.3,"No catch-up jump at "+now);
     double lag=now-position/Speed;maxLag=Math.Max(maxLag,lag);minLag=Math.Min(minLag,lag);
    }
    previous=position;
   }
   Assert.Less(maxLag,.25,"Playback stays close to the host");Assert.Greater(minLag,.05,"Keeps a small buffer to absorb jitter");
  }
  [Test] public void StarvedTimelineHoldsTheNewestFrameThenResumesWithoutJumping() {
   var timeline=new InterpolationTimeline<double[]>();double now=0,dt=1/60.0,host=0,previous=0;
   for(;now<1;now+=dt){while(host<=now-.02){timeline.Add(host,now,new[]{host*Speed});host+=Send;}previous=Position(timeline,now);}
   double held=double.NaN;
   for(double stall=now+.4;now<stall;now+=dt){double position=Position(timeline,now);Assert.GreaterOrEqual(position,previous-1e-9);Assert.LessOrEqual(position,host*Speed);previous=position;held=position;}
   Assert.AreEqual((host-Send)*Speed,held,1e-6,"A stalled stream holds the newest host frame instead of guessing");
   for(double end=now+1;now<end;now+=dt) {
    while(host<=now-.02){timeline.Add(host,now,new[]{host*Speed});host+=Send;}
    double position=Position(timeline,now);
    Assert.GreaterOrEqual(position,previous-1e-9);Assert.LessOrEqual(position-previous,Speed*dt*1.3+1e-9,"Resumes on the same clock instead of jumping");previous=position;
   }
  }
  [Test] public void OldOrDuplicateFramesAreIgnoredAndARestartedHostResynchronises() {
   var timeline=new InterpolationTimeline<double[]>();
   timeline.Add(10,0,new[]{1.0});timeline.Add(10.05,.05,new[]{2.0});timeline.Add(10.05,.06,new[]{99.0});timeline.Add(10.02,.07,new[]{99.0});
   Assert.AreEqual(2.0,timeline.Latest[0]);
   timeline.Add(0,.1,new[]{7.0});
   Assert.AreEqual(7.0,timeline.Latest[0],"A host clock that went far back is a new session");
   Assert.AreEqual(7.0,Position(timeline,.1));
  }
 }
}
