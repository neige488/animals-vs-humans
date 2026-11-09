using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Who shoved whom, readable in the chaos (presentation only, every viewer alike).
 /// Each judged shot hit (an animal staggered, a friend shoved) shows a short tag over the one who was hit
 /// for <see cref="TagSeconds"/>, and the last few hits stay listed bottom-left.
 /// </summary>
 public sealed class HitFeed {
  public const float TagSeconds=1.6f;public const int Lines=4;
  public struct Entry {public int Id,Shooter,Target;public bool Friendly;public string Text;public float Age;}
  readonly List<Entry> entries=new List<Entry>();
  int lastId;bool primed;
  public IReadOnlyList<Entry> Entries=>entries;
  /// <summary>Reads new shot hits from the public event window; the first look only primes, so joins never replay.</summary>
  public void Observe(SessionState state,float seconds) {
   for(int i=entries.Count-1;i>=0;i--){var e=entries[i];e.Age+=seconds;entries[i]=e;if(e.Age>8)entries.RemoveAt(i);}
   if(state?.Events==null)return;
   int newest=0;foreach(var e in state.Events)newest=Mathf.Max(newest,e.Id);
   if(!primed||newest<lastId){primed=true;lastId=newest;return;}
   foreach(var e in state.Events) {
    if(e.Id<=lastId||(e.Kind!=FeelEventKind.Shoved&&e.Kind!=FeelEventKind.Stagger))continue;
    if(e.Actor<0||e.Actor>=state.Players.Length||e.Target<0||e.Target>=state.Players.Length)continue;
    bool friendly=e.Kind==FeelEventKind.Shoved;var shooter=state.Players[e.Actor];var target=state.Players[e.Target];
    entries.Add(new Entry{Id=e.Id,Shooter=e.Actor,Target=e.Target,Friendly=friendly,Text=Describe(shooter.Nickname,target.Nickname,target.CharacterName,friendly,shooter.Tool)});
   }
   lastId=Mathf.Max(lastId,newest);
   while(entries.Count>12)entries.RemoveAt(0);
  }
  /// <summary>"민지의 글러브 → 준호 (아군!)" / "민지의 코르크 → 여우".</summary>
  public static string Describe(string shooter,string target,string targetCharacter,bool friendly,ToolKind tool=ToolKind.Cork)=>friendly?$"{shooter}의 {Tools.Short(tool)} → {target} (아군!)":$"{shooter}의 {Tools.Short(tool)} → {targetCharacter}";
  public void Reset(){entries.Clear();primed=false;lastId=0;}

  GUIStyle tag,line;
  static readonly Color Friendly=new Color(1,.83f,.25f),Enemy=new Color(.55f,.9f,1);
  public void Draw(Camera view,System.Func<int,Transform> body,int localSlot) {
   if(view==null)return;
   if(tag==null){tag=new GUIStyle(GUI.skin.label){fontSize=17,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};line=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleLeft};}
   foreach(var e in entries) {
    if(e.Age>TagSeconds)continue;var t=body(e.Target);if(t==null)continue;
    var screen=view.WorldToScreenPoint(t.position+Vector3.up*(2.25f+e.Age*.25f));if(screen.z<=0)continue;
    float alpha=Mathf.Clamp01((TagSeconds-e.Age)/.4f);var rect=new Rect(screen.x-170,Screen.height-screen.y-14,340,28);
    string text=e.Target==localSlot?(e.Friendly?$"{Short(e.Text)} — 나!":Short(e.Text)):Short(e.Text);
    Shadowed(rect,text,tag,(e.Friendly?Friendly:Enemy)*new Color(1,1,1,alpha));
   }
   int shown=0;
   for(int i=entries.Count-1;i>=0&&shown<Lines;i--,shown++) {
    var e=entries[i];float alpha=Mathf.Clamp01((8-e.Age)/1.5f);
    Shadowed(new Rect(20,Screen.height-130-shown*24,520,24),e.Text,line,(e.Friendly?Friendly:Enemy)*new Color(1,1,1,alpha));
   }
  }
  // The tag over a head keeps only the shooter and tool: "민지의 글러브!".
  static string Short(string text){int arrow=text.IndexOf(" →");return arrow<0?text:text.Substring(0,arrow)+"!";}
  static void Shadowed(Rect r,string text,GUIStyle style,Color color) {
   var old=style.normal.textColor;style.normal.textColor=new Color(0,0,0,color.a*.75f);GUI.Label(new Rect(r.x+1.5f,r.y+1.5f,r.width,r.height),text,style);
   style.normal.textColor=color;GUI.Label(r,text,style);style.normal.textColor=old;
  }
 }
}
