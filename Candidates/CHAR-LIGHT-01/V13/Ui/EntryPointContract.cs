// Checks the actual compiled UI capability before and after the candidate; Native semantics remain Root-owned.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
static class EntryPointContract
{
 static int Main(string[] args)
 {
  string root="E:/WORLDCREATOR/new/Project_ANKALUTE";
  AssemblyLoadContext.Default.Resolving+=(context,name)=>{
   foreach(string folder in new[]{root+"/Library/ScriptAssemblies","E:/unity/Tuanjie Hub/2022.3.62t16/Editor/Data/Managed/UnityEngine","E:/unity/Tuanjie Hub/2022.3.62t16/Editor/Data/Managed"}){string path=Path.Combine(folder,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}return null;
  };
  if(args.Length!=1)throw new ArgumentException("Explicit compiled assembly required");
  var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
  var type=assembly.GetType("Ossastra.Development.CharacterLighting.CharacterLightingPanel",true);
  var apply=type.GetMethod("ApplySunlightPreset",BindingFlags.Public|BindingFlags.Instance,null,new[]{typeof(int)},null);
  if(apply==null){Console.WriteLine("RED: current Panel cannot apply a saved complete sunlight A/B result");return 1;}
  Console.WriteLine("GREEN: compiled Panel exposes saved sunlight A/B entry; runtime semantics require Native verification");return 0;
 }
}
