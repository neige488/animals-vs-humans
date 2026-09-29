using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace AvH.Editor {
 public static class BuildPlaytest {
  public static void Mac() => Build(BuildTarget.StandaloneOSX,"Builds/macOS/AnimalsVsHumans.app");
  public static void Windows() => Build(BuildTarget.StandaloneWindows64,"Builds/Windows/AnimalsVsHumans.exe");
  static void Build(BuildTarget target,string path) {
   if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target)) throw new InvalidOperationException("빌드 모듈을 설치하세요: "+target);
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/SampleScene.unity"},locationPathName=path,target=target,options=BuildOptions.Development});
   if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("빌드 실패: "+report.summary.result);
  }
 }
}
