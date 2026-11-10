using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>
 /// Bot-only matches for balance numbers. Runs only when AVH_BALANCE_OUT names a result file:
 /// AVH_BALANCE_MAPS "0,1,2" (default all), AVH_BALANCE_SEEDS worlds per map (default 2), AVH_BALANCE_ROUNDS rounds per
 /// world (default 3), AVH_BALANCE_SET "AnimalSpeed=5.2;RoundSeconds=150" overrides PlaytestValues fields.
 /// One line per round: map;seed;round;winner;seconds;humansLeft;players (slot:human id:first animal 0/1:animal id:catches:turned at).
 /// </summary>
 public class BalanceHarness {
  static string Env(string name,string fallback=null){var v=Environment.GetEnvironmentVariable(name);return string.IsNullOrEmpty(v)?fallback:v;}
  [UnityTest,Timeout(3600000)] public IEnumerator BotMatches() {
   string output=Env("AVH_BALANCE_OUT");if(output==null){Assert.Ignore("AVH_BALANCE_OUT not set");yield break;}
   var maps=Env("AVH_BALANCE_MAPS","0,1,2,3,4,5").Split(',').Select(int.Parse).ToArray();
   int seeds=int.Parse(Env("AVH_BALANCE_SEEDS","2")),rounds=int.Parse(Env("AVH_BALANCE_ROUNDS","3")),firstSeed=int.Parse(Env("AVH_BALANCE_FIRST_SEED","1"));
   string overrides=Env("AVH_BALANCE_SET","");
   foreach(int map in maps)for(int seed=firstSeed;seed<firstSeed+seeds;seed++) {
    string profile=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xml");
    var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var values=setup.ObserveSettings().Edit;
    values.Tool=Tools.Random;values.ResultSeconds=1;values.Map=map;
    foreach(var pair in overrides.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)) {
     var kv=pair.Split('=');var field=typeof(PlaytestValues).GetField(kv[0].Trim());Assert.NotNull(field,kv[0]);
     field.SetValue(values,Convert.ChangeType(kv[1].Trim(),field.FieldType,CultureInfo.InvariantCulture));
    }
    setup.UpdateSettingsEdit(0,values);Assert.IsTrue(setup.ApplySettingsNow(0,true));
    var root=new GameObject("balance world");
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotsDriveEveryone=true;
    game.StartSolo("봇 0",seed*7919+map,profile,map);yield return null;
    for(int played=0;played<rounds;played++) {
     int round=game.Observe().Round,lastId=0,steps=0;var human=new string[12];var first=new bool[12];var catches=new int[12];var turned=new float[12];
     foreach(var p in game.Observe().Players){human[p.Slot]=p.CharacterId;turned[p.Slot]=-1;}
     bool chase=false;double chaseSeconds=0;SessionState state=null;
     while(true) {
      game.Step(.02f);state=game.Observe();if(++steps%200==0)yield return null;
      if(state.Round!=round)break;
      if(!chase&&state.Phase==RoundPhase.Chase){chase=true;foreach(var p in state.Players)first[p.Slot]=p.Faction==Faction.Animal;}
      if(state.Phase==RoundPhase.Chase)chaseSeconds+=.02;
      foreach(var e in state.Events.Where(e=>e.Id>lastId)){lastId=e.Id;
       if(e.Kind==FeelEventKind.AttackHit&&e.Actor>=0)catches[e.Actor]++;
       if(e.Kind==FeelEventKind.Transform&&e.Target>=0&&turned[e.Target]<0&&chase)turned[e.Target]=(float)chaseSeconds;}
      if(state.Phase==RoundPhase.Results)break;
     }
     var line=string.Join(";",new[]{map.ToString(),seed.ToString(),round.ToString(),state.Winner.HasValue?state.Winner.Value.ToString():"None",chaseSeconds.ToString("F1",CultureInfo.InvariantCulture),state.Players.Count(p=>p.Faction==Faction.Human).ToString(),
      string.Join(",",state.Players.Select(p=>$"{p.Slot}:{human[p.Slot]}:{(first[p.Slot]?1:0)}:{(p.Faction==Faction.Animal?p.CharacterId:"")}:{catches[p.Slot]}:{turned[p.Slot].ToString("F1",CultureInfo.InvariantCulture)}"))});
     File.AppendAllText(output,line+"\n");
     // Through the result screen into the next round.
     while(game.Observe().Round==round){game.Step(.02f);if(++steps%200==0)yield return null;}
    }
    UnityEngine.Object.Destroy(root);if(File.Exists(profile))File.Delete(profile);yield return null;
   }
  }
 }
}
