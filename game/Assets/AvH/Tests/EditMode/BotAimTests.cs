using System;
using NUnit.Framework;
namespace AvH.Tests {
 // A bot's aim reacts like a person: it lags what it sees and wanders slightly. Zero settings keep the original exact aim.
 public class BotAimTests {
  const double Dt=.02;
  static double DeltaAngle(float a,float b){double d=(b-a)%360;if(d>180)d-=360;if(d<-180)d+=360;return d;}
  [Test] public void ZeroDelayAndErrorAimExactlyAndFireAsSoonAsTheTargetIsVisible() {
   var aim=new BotAim(3);var random=new Random(1);
   for(int i=0;i<500;i++) {
    float yaw=(float)(random.NextDouble()*360-180),pitch=(float)(random.NextDouble()*60-30);int target=random.Next(4);bool visible=random.Next(3)>0;
    var output=aim.Step(i*Dt,target,visible,yaw,pitch,0,0);
    Assert.AreEqual(yaw,output.Yaw);Assert.AreEqual(pitch,output.Pitch);Assert.AreEqual(visible,output.CanFire);
   }
  }
  [Test] public void DelayedAimFollowsWhereTheTargetWasAndFiresOnlyAfterReacting() {
   var aim=new BotAim(3);BotAimOutput output=default;
   for(int i=0;i<=100;i++){double t=i*Dt;output=aim.Step(t,1,true,(float)(100*t)-170,-10,.2f,0);
    if(t>=.2)Assert.AreEqual(0,DeltaAngle((float)(100*(t-.2))-170,output.Yaw),.5,"Aim lags the seen heading by the reaction time at "+t);
    Assert.AreEqual(t>=.2-1e-9,output.CanFire,"Fires only after reacting to the target at "+t);}
   Assert.AreEqual(-10,output.Pitch,.01);
   output=aim.Step(2.02,2,true,0,0,.2f,0);Assert.IsFalse(output.CanFire,"A new target needs a new reaction");
   for(int i=1;i<=10;i++)output=aim.Step(2.02+i*Dt,2,i<5,0,0,.2f,0);
   Assert.IsFalse(output.CanFire,"Losing sight restarts the reaction");
  }
  [Test] public void AimErrorWandersSmoothlyWithinTheConfiguredDegrees() {
   var aim=new BotAim(9);float min=float.MaxValue,max=float.MinValue,previous=0,largestStep=0;
   for(int i=0;i<1000;i++){var output=aim.Step(i*Dt,1,true,0,5,0,3);
    min=Math.Min(min,output.Yaw);max=Math.Max(max,output.Yaw);if(i>0)largestStep=Math.Max(largestStep,Math.Abs(output.Yaw-previous));previous=output.Yaw;
    Assert.LessOrEqual(Math.Abs(output.Yaw),3.001f);Assert.LessOrEqual(Math.Abs(output.Pitch-5),3.001f);}
   Assert.Greater(max-min,2,"The error actually varies");Assert.Less(largestStep,.6f,"It drifts, it does not jitter");
   var other=new BotAim(10);Assert.AreNotEqual(new BotAim(9).Step(1,1,true,0,0,0,3).Yaw,other.Step(1,1,true,0,0,0,3).Yaw,"Each bot wanders differently");
  }
 }
}
