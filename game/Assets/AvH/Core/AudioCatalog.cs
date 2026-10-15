using System;
using System.Linq;
namespace AvH {
 /// <summary>Where a sound comes from. Only these three are allowed (CONTEXT.md 「실감 개선 정렬」, PA-5).</summary>
 public enum SoundOrigin { Cc0, Owned, Synthesized }
 /// <summary>One named sound and the clips that may voice it (a random variant is chosen per play).</summary>
 public sealed class SoundEntry {
  public readonly string Cue;public readonly SoundOrigin Origin;public readonly string[] Clips;
  public SoundEntry(string cue,SoundOrigin origin,params string[] clips){Cue=cue;Origin=origin;Clips=clips??Array.Empty<string>();}
 }
 /// <summary>
 /// Every sound the game may load. CC0 clips are Resources paths committed under Assets/AvH/Resources;
 /// owned clips are Polyperfect sounds referenced through the generated (Git-ignored) owned asset catalog.
 /// Anything that cannot be loaded is synthesized at runtime. docs/audio-sources.md records each clip.
 /// </summary>
 public static class AudioCatalog {
  public static readonly SoundEntry[] Entries={
   new SoundEntry("step-human",SoundOrigin.Cc0,"Audio/step-human-1","Audio/step-human-2","Audio/step-human-3"),
   new SoundEntry("step-paw",SoundOrigin.Cc0,"Audio/step-paw-1","Audio/step-paw-2","Audio/step-paw-3"),
   new SoundEntry("land",SoundOrigin.Cc0,"Audio/land-heavy"),
   new SoundEntry("hit",SoundOrigin.Cc0,"Audio/hit-1","Audio/hit-2"),
   new SoundEntry("swing",SoundOrigin.Cc0,"Audio/swing"),
   new SoundEntry("transform",SoundOrigin.Cc0,"Audio/transform"),
   new SoundEntry("stagger",SoundOrigin.Cc0,"Audio/stagger"),
   new SoundEntry("reload",SoundOrigin.Cc0,"Audio/reload"),
   new SoundEntry("dry-fire",SoundOrigin.Cc0,"Audio/dry-fire"),
   new SoundEntry("fire",SoundOrigin.Synthesized),
   new SoundEntry("pop",SoundOrigin.Synthesized),
   new SoundEntry("prop-knock",SoundOrigin.Synthesized),
   new SoundEntry("snarl",SoundOrigin.Owned,"SFX_Bear_Calm"),
   new SoundEntry("growl-bear",SoundOrigin.Owned,"SFX_Bear_Growl_2"),
   new SoundEntry("growl-wolf",SoundOrigin.Owned,"SFX_Wolf_Howl"),
   new SoundEntry("growl-penguin",SoundOrigin.Owned,"SFX_Penguin"),
   new SoundEntry("growl-rabbit",SoundOrigin.Owned,"SFX_Ice_Squeels"),
   new SoundEntry("ambience",SoundOrigin.Cc0,"Audio/ambience-village"),
   new SoundEntry("music-prepare",SoundOrigin.Cc0,"Audio/music-prepare"),
   new SoundEntry("music-chase",SoundOrigin.Cc0,"Audio/music-chase"),
   new SoundEntry("music-final",SoundOrigin.Cc0,"Audio/music-final"),
  };
  public static SoundEntry Find(string cue)=>Entries.FirstOrDefault(e=>e.Cue==cue);
 }
}
