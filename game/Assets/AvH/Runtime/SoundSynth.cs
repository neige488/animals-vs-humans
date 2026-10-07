using System;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Code-generated stand-ins for every cue (self-made, no license constraint). Used for the bubble sounds and
 /// whenever a CC0 or owned clip cannot be loaded, so a missing file never stops play.
 /// </summary>
 public static class SoundSynth {
  const int Rate=22050;
  public static AudioClip Make(string cue) {
   if(cue==null)return null;
   if(cue=="fire")return Bubble(true);
   if(cue=="pop")return Bubble(false);
   if(cue.StartsWith("step",StringComparison.Ordinal))return Thump(cue,.09f,140,.5f);
   if(cue=="land")return Thump(cue,.22f,90,.8f);
   if(cue=="prop-knock")return Thump(cue,.14f,210,1.1f);
   if(cue=="hit")return Hit();
   if(cue=="swing")return Whoosh();
   if(cue=="transform")return Sweep(cue,.45f,300,1400,true);
   if(cue=="stagger")return Sweep(cue,.5f,700,180,false);
   if(cue=="reload")return Clicks(cue,new[]{0f,.16f});
   if(cue=="dry-fire")return Clicks(cue,new[]{0f});
   if(cue=="snarl")return Growl(cue,.35f,85);
   if(cue.StartsWith("growl",StringComparison.Ordinal))return Growl(cue,.8f,cue=="growl-rabbit"||cue=="growl-penguin"?420:110);
   if(cue=="ambience")return Ambience();
   if(cue=="music-prepare")return Music(cue,92,new[]{0,4,7,9},false);
   if(cue=="music-chase")return Music(cue,128,new[]{0,3,7,10},true);
   if(cue=="music-final")return Music(cue,156,new[]{0,3,6,10},true);
   return Thump(cue,.12f,200,.5f);
  }
  static AudioClip Clip(string name,float[] samples){var clip=AudioClip.Create("Synth "+name,samples.Length,1,Rate,false);clip.SetData(samples,0);return clip;}
  static float Envelope(float t,float attack=.08f,float decay=7)=>Mathf.Sin(Mathf.PI*.5f*Mathf.Min(1,t/attack))*Mathf.Exp(-t*decay);
  // The original bubble shot (air puff) and pop, unchanged from the first prototype.
  static AudioClip Bubble(bool air) {
   int count=air?2205:3307;var samples=new float[count];var random=new System.Random(19);float phase=0;
   for(int i=0;i<count;i++){float t=(float)i/count;phase+=(air?900:1600)*(1-t*.85f)*Mathf.PI*2/Rate;float noise=(float)random.NextDouble()*2-1;samples[i]=(air?noise*.5f:Mathf.Sin(phase)*.6f+noise*.15f)*Mathf.Sin(Mathf.PI*Mathf.Min(1,t*8))*Mathf.Exp(-t*7);}
   return Clip(air?"fire":"pop",samples);
  }
  static AudioClip Thump(string name,float seconds,float hz,float noise) {
   int count=(int)(Rate*seconds);var s=new float[count];var random=new System.Random(name.Length*31);float phase=0,low=0;
   for(int i=0;i<count;i++){float t=(float)i/count;phase+=hz*(1-t*.6f)*Mathf.PI*2/Rate;low+=((float)random.NextDouble()*2-1-low)*.15f;s[i]=(Mathf.Sin(phase)*.7f+low*noise*2)*Envelope(t,.02f,9)*.8f;}
   return Clip(name,s);
  }
  static AudioClip Hit() {
   int count=(int)(Rate*.25f);var s=new float[count];var random=new System.Random(5);float phase=0;
   for(int i=0;i<count;i++){float t=(float)i/count;phase+=(160-100*t)*Mathf.PI*2/Rate;float crack=t<.08f?((float)random.NextDouble()*2-1)*(1-t/.08f):0;s[i]=(Mathf.Sin(phase)*.8f+crack*.6f)*Envelope(t,.01f,6);}
   return Clip("hit",s);
  }
  static AudioClip Whoosh() {
   int count=(int)(Rate*.3f);var s=new float[count];var random=new System.Random(11);float low=0,band=0;
   for(int i=0;i<count;i++){float t=(float)i/count;float k=.05f+.25f*Mathf.Sin(Mathf.PI*t);low+=((float)random.NextDouble()*2-1-low)*k;band+=(low-band)*.3f;s[i]=(low-band)*2.2f*Mathf.Sin(Mathf.PI*t);}
   return Clip("swing",s);
  }
  static AudioClip Sweep(string name,float seconds,float from,float to,bool sparkle) {
   int count=(int)(Rate*seconds);var s=new float[count];float phase=0,phase2=0;
   for(int i=0;i<count;i++){float t=(float)i/count;float hz=from*Mathf.Pow(to/from,t);phase+=hz*Mathf.PI*2/Rate;phase2+=hz*1.5f*Mathf.PI*2/Rate;float wobble=sparkle?1:(1+.3f*Mathf.Sin(t*60));s[i]=(Mathf.Sin(phase)*.5f+(sparkle?Mathf.Sin(phase2)*.25f:0))*wobble*Mathf.Sin(Mathf.PI*Mathf.Min(1,t*10))*(1-t*.7f);}
   return Clip(name,s);
  }
  static AudioClip Clicks(string name,float[] at) {
   int count=(int)(Rate*(at[at.Length-1]+.06f));var s=new float[count];var random=new System.Random(3);
   foreach(var start in at){int first=(int)(start*Rate);for(int i=0;i<(int)(Rate*.03f)&&first+i<count;i++){float t=i/(Rate*.03f);s[first+i]+=(((float)random.NextDouble()*2-1)*.5f+Mathf.Sin(i*2*Mathf.PI*2400/Rate)*.5f)*Mathf.Exp(-t*9);}}
   return Clip(name,s);
  }
  static AudioClip Growl(string name,float seconds,float hz) {
   int count=(int)(Rate*seconds);var s=new float[count];var random=new System.Random(name.Length*7);float phase=0,low=0;
   for(int i=0;i<count;i++){float t=(float)i/count;float f=hz*(1+.15f*Mathf.Sin(t*Mathf.PI*2*3));phase=Mathf.Repeat(phase+f/Rate,1);float saw=phase*2-1;low+=(((float)random.NextDouble()*2-1)-low)*.1f;float rasp=1+.5f*Mathf.Sin(t*Mathf.PI*2*28);s[i]=(saw*.45f+low*.4f)*rasp*.6f*Mathf.Sin(Mathf.PI*Mathf.Min(1,t*5))*Mathf.Min(1,(1-t)*4);}
   return Clip(name,s);
  }
  // A seamless 8 second loop: soft wind plus a few bird chirps.
  static AudioClip Ambience() {
   int count=Rate*8;var s=new float[count];var random=new System.Random(23);float low=0,slow=0;
   for(int i=0;i<count;i++){float t=(float)i/Rate;low+=(((float)random.NextDouble()*2-1)-low)*.02f;slow=.5f+.5f*Mathf.Sin(t*Mathf.PI*2/8);s[i]=low*.9f*(.4f+.6f*slow);}
   foreach(var start in new[]{1.1f,1.35f,3.7f,5.2f,5.4f,6.9f}){int first=(int)(start*Rate);int n=(int)(Rate*.12f);float phase=0;for(int i=0;i<n&&first+i<count;i++){float t=(float)i/n;phase+=(2600+900*Mathf.Sin(t*Mathf.PI))*Mathf.PI*2/Rate;s[first+i]+=Mathf.Sin(phase)*.12f*Mathf.Sin(Mathf.PI*t);}}
   return Clip("ambience",s);
  }
  // A four bar loop: bass on the beat and an arpeggio over a chord (semitones from the root), louder and faster as tension rises.
  static AudioClip Music(string name,float bpm,int[] chord,bool drive) {
   float beat=60/bpm;int count=(int)(Rate*beat*16);var s=new float[count];var random=new System.Random(41);
   int[] roots={0,-3,-7,-5};
   for(int bar=0;bar<4;bar++)for(int step=0;step<8;step++) {
    int first=(int)(Rate*beat*(bar*4+step*.5f));int n=(int)(Rate*beat*.5f);
    float hz=220*Mathf.Pow(2,(roots[bar]+chord[step%chord.Length]+(step>=4?12:0))/12f);
    float bass=110*Mathf.Pow(2,roots[bar]/12f)/2;
    for(int i=0;i<n&&first+i<count;i++){float t=(float)i/Rate;float note=Mathf.Sin(t*hz*Mathf.PI*2)*.22f*Mathf.Exp(-t*6);
     float b=step%2==0?Mathf.Sin(t*bass*Mathf.PI*2)*.3f*Mathf.Exp(-t*(drive?5:3)):0;
     float hat=drive&&step%2==1?((float)random.NextDouble()*2-1)*.06f*Mathf.Exp(-t*60):0;
     s[first+i]+=note+b+hat;}
   }
   return Clip(name,s);
  }
 }
}
