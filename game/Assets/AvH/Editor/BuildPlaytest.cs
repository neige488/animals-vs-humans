using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace AvH.Editor {
 public static class BuildPlaytest {
  public static void Mac() => Build(BuildTarget.StandaloneOSX,"Builds/macOS/AnimalsVsHumans.app");
  public static void Windows() => Build(BuildTarget.StandaloneWindows64,"Builds/Windows/AnimalsVsHumans.exe");
  static void Build(BuildTarget target,string path) {
   if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target)) throw new InvalidOperationException("빌드 모듈을 설치하세요: "+target);
   ConfigureFishNet.Configure();
   PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.neige488.animalsvshumans");
   // Owned sounds live in the Git-ignored catalog; refresh it so every build carries them when the assets exist.
   ConfigureOwnedAssets.ConfigureSounds();
   // A village generated before pushable props existed keeps every crate fixed; rebuild it from the owned assets.
   if(ConfigureOwnedAssets.VillageNeedsRebuild()||ConfigureOwnedAssets.CrossingNeedsRebuild())ConfigureOwnedAssets.Configure();
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/SampleScene.unity"},locationPathName=path,target=target,options=BuildOptions.Development});
   if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("빌드 실패: "+report.summary.result);
   if(target==BuildTarget.StandaloneOSX){ConfigureMacPrivacy(path);SignMac(path);}
  }
  // Editing Info.plist breaks Unity's ad-hoc signature, and macOS then calls a downloaded app damaged. Sign again and check it.
  static void SignMac(string appPath) {
   Codesign("--force","--deep","--sign","-",appPath);
   Codesign("--verify","--deep","--strict",appPath);
  }
  static void Codesign(params string[] args) {
   var start=new ProcessStartInfo("/usr/bin/codesign",string.Join(" ",Array.ConvertAll(args,a=>"\""+a.Replace("\"","\\\"")+"\""))){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true};
   using var p=Process.Start(start);string err=p.StandardError.ReadToEnd();p.StandardOutput.ReadToEnd();p.WaitForExit();
   if(p.ExitCode!=0)throw new InvalidOperationException("macOS 앱 서명 실패: "+err.Trim());
  }
  static void ConfigureMacPrivacy(string appPath) {
   var path=Path.Combine(appPath,"Contents/Info.plist");
   var plist=new XmlDocument {XmlResolver=null};
   plist.Load(path);
   // XmlDocument adds an empty internal DTD subset that Apple's plist parser rejects.
   if(plist.DocumentType!=null)plist.RemoveChild(plist.DocumentType);
   var dict=plist.SelectSingleNode("/plist/dict");
   if(dict==null)throw new InvalidOperationException("macOS Info.plist의 dict가 없습니다.");
   const string key="NSLocalNetworkUsageDescription";
   var existing=dict.SelectSingleNode("key[text()='"+key+"']");
   if(existing!=null) {
    var value=existing.NextSibling;
    if(value!=null)dict.RemoveChild(value);
    dict.RemoveChild(existing);
   }
   var keyNode=plist.CreateElement("key");keyNode.InnerText=key;dict.AppendChild(keyNode);
   var description=plist.CreateElement("string");
   description.InnerText="같은 네트워크의 PC와 비공개 멀티플레이 방을 만들고 참가하기 위해 로컬 네트워크를 사용합니다.";
   dict.AppendChild(description);
   plist.Save(path);
  }
 }
}
