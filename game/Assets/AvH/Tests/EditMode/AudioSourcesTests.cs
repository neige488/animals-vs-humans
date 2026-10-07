using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace AvH.Tests {
 // PA-5: every sound the game ships or loads is owned, synthesized or CC0, and matches the repository source record.
 public class AudioSourcesTests {
  sealed class Row {public string Clip,Cue,Origin,Source,Url,License,Processing;}
  static string Project=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
  static string RecordPath=>Path.GetFullPath(Path.Combine(Project,"..","docs","audio-sources.md"));
  static readonly string[] AudioExtensions={".ogg",".wav",".mp3",".aif",".aiff",".flac",".m4a"};
  static List<Row> ReadRecord() {
   Assert.IsTrue(File.Exists(RecordPath),"The sound source record must exist: "+RecordPath);
   var rows=new List<Row>();
   foreach(var line in File.ReadAllLines(RecordPath)) {
    if(!line.TrimStart().StartsWith("|"))continue;
    var cells=line.Trim().Trim('|').Split('|').Select(c=>c.Trim().Trim('`')).ToArray();
    if(cells.Length<7||cells[0]=="파일"||cells[0].StartsWith("-"))continue;
    rows.Add(new Row{Clip=cells[0],Cue=cells[1],Origin=cells[2],Source=cells[3],Url=cells[4],License=cells[5],Processing=cells[6]});
   }
   return rows;
  }
  static string Label(SoundOrigin origin)=>origin==SoundOrigin.Cc0?"CC0":origin==SoundOrigin.Owned?"보유":"합성";

  [Test] public void EveryCommittedSoundFileIsRecordedAsCc0() {
   var rows=ReadRecord();
   var resources=Path.Combine(Project,"Assets","AvH","Resources");
   var files=Directory.GetFiles(Path.Combine(Project,"Assets","AvH"),"*",SearchOption.AllDirectories)
    .Where(f=>AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
   Assert.IsNotEmpty(files,"The audio slice ships CC0 sound files");
   foreach(var file in files) {
    Assert.IsTrue(file.StartsWith(resources),"Shipped sounds live under Resources/Audio: "+file);
    var clip=file.Substring(resources.Length+1).Replace('\\','/');
    clip=clip.Substring(0,clip.Length-Path.GetExtension(clip).Length);
    var row=rows.FirstOrDefault(r=>r.Clip==clip);
    Assert.IsNotNull(row,"Unrecorded sound file: "+clip);
    Assert.AreEqual("CC0",row.Origin,"Only CC0 sounds are committed: "+clip);
    StringAssert.StartsWith("CC0",row.License,clip);
    StringAssert.StartsWith("http",row.Url,"A CC0 source can be checked again: "+clip);
    Assert.IsNotEmpty(row.Source,clip);Assert.IsNotEmpty(row.Processing,clip);
   }
   foreach(var row in rows.Where(r=>r.Origin=="CC0"))
    Assert.IsTrue(files.Any(f=>Path.GetFileNameWithoutExtension(f)==Path.GetFileName(row.Clip)),"Recorded CC0 file is missing: "+row.Clip);
  }

  [Test] public void EveryCatalogSoundMatchesTheRecordAndNothingElseIsAllowed() {
   var rows=ReadRecord();
   foreach(var row in rows)Assert.Contains(row.Origin,new[]{"CC0","보유","합성"},"Unknown or non-CC0 origin for "+row.Clip+": "+row.Origin);
   Assert.IsNotEmpty(AudioCatalog.Entries);
   foreach(var entry in AudioCatalog.Entries) {
    if(entry.Origin==SoundOrigin.Synthesized){Assert.IsTrue(rows.Any(r=>r.Cue==entry.Cue&&r.Origin=="합성"),"Synthesized cue is recorded: "+entry.Cue);continue;}
    Assert.IsNotEmpty(entry.Clips,entry.Cue);
    foreach(var clip in entry.Clips) {
     var row=rows.FirstOrDefault(r=>r.Clip==clip);
     Assert.IsNotNull(row,"Catalog clip is recorded: "+clip);
     Assert.AreEqual(Label(entry.Origin),row.Origin,clip);Assert.AreEqual(entry.Cue,row.Cue,clip);
    }
   }
   // Every recorded clip is used by the catalog, so the record never drifts from what the game loads.
   foreach(var row in rows.Where(r=>r.Origin!="합성"))
    Assert.IsTrue(AudioCatalog.Entries.Any(e=>e.Clips.Contains(row.Clip)),"Recorded but unused clip: "+row.Clip);
  }
 }
}
