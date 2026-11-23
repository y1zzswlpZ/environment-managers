using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JavaManager;
class JavaManagerTests {
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static string Run(Jdk j,string exe,string[] args,string folder){int exit;string result=Core.Capture(Core.EnvironmentFor(j,exe,args,folder,false),out exit);if(exit!=0)throw new Exception(exe+" failed: "+result);return result;}
 static int Main(){try{Test();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static void Test(){
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"题目 & 中文 空格");Directory.CreateDirectory(root);
  string oldProcessJava=Environment.GetEnvironmentVariable("JAVA_HOME");string oldUserJava=Environment.GetEnvironmentVariable("JAVA_HOME",EnvironmentVariableTarget.User);
  Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS","-Dtest.poison=present");Environment.SetEnvironmentVariable("JRE_HOME",@"D:\missing-jre");Environment.SetEnvironmentVariable("CLASSPATH",@"D:\missing-classpath");
  List<string> errors;List<Jdk> jdks=Core.Scan(out errors);Assert(errors.Count==0&&jdks.Count==5,"five installed JDKs detected by actual runtime");
  string source="import java.nio.file.*; import java.nio.charset.StandardCharsets; public class Probe { public static void main(String[] a) throws Exception { String t=System.getProperty(\"java.runtime.version\")+\"|\"+System.getProperty(\"java.home\")+\"|\"+System.getProperty(\"user.dir\")+\"|\"+System.getProperty(\"test.poison\",\"clean\")+\"|\"+System.getenv(\"JAVA_HOME\")+\"|\"+System.getenv(\"JRE_HOME\")+\"|\"+System.getenv(\"CLASSPATH\")+\"|\"+System.getProperty(\"ctf.flag\",\"none\")+\"|\"+String.join(\";\",a); Files.write(Paths.get(\"result.txt\"),t.getBytes(StandardCharsets.UTF_8)); }}";
  string[] trickyArgs={"中文 空格","C:\\folder with spaces\\","quote\"value","& whoami | echo","%PATH%"};
  List<Process> children=new List<Process>();List<string> folders=new List<string>();
  foreach(Jdk j in jdks){
   string folder=Path.Combine(root,j.Name);Directory.CreateDirectory(folder);folders.Add(folder);
   File.WriteAllText(Path.Combine(folder,"Probe.java"),source,new UTF8Encoding(false));
   Run(j,Path.Combine(j.Home,@"bin\javac.exe"),new[]{"-encoding","UTF-8","Probe.java"},folder);
   Run(j,Path.Combine(j.Home,@"bin\jar.exe"),new[]{"cfe","Probe.jar","Probe","Probe.class"},folder);
   string originalDir=Environment.CurrentDirectory;Core.SaveProject(folder,j,new[]{"-Dctf.flag=bound value"});ProjectPin pin=Core.LoadProject(folder,true);
   Assert(pin.RuntimeVersion==j.RuntimeVersion&&pin.Fingerprints.Count>=4,j.Name+" exact build and runtime fingerprints pinned");
   var info=Core.JarInfo(j,Path.Combine(folder,"Probe.jar"),folder,pin.JvmOptions,trickyArgs);
   Assert(info.EnvironmentVariables["JAVA_HOME"]==j.Home&&info.EnvironmentVariables["JRE_HOME"]==null&&info.EnvironmentVariables["JAVA_TOOL_OPTIONS"]==null,j.Name+" isolated child environment");
   var p=Process.Start(info);children.Add(p);
   string maven=Path.Combine(Core.JavaRoot,@"Maven\bin\mvn.cmd");int mvnExit;
   var mvnInfo=Core.EnvironmentFor(j,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe"),new[]{"/D","/C",maven,"-v"},folder,false);
   string mvn=Core.Capture(mvnInfo,out mvnExit);Assert(mvnExit==0&&mvn.Contains("Java version: "+j.Version),j.Name+" Maven uses selected JDK");
   Assert(Environment.CurrentDirectory==originalDir,"binding preserves working directory");
  }
  for(int i=0;i<children.Count;i++){Assert(children[i].WaitForExit(20000)&&children[i].ExitCode==0,jdks[i].Name+" independent concurrent launch");children[i].Dispose();string result=File.ReadAllText(Path.Combine(folders[i],"result.txt"));string[] values=result.Split('|');Assert(values[0]==jdks[i].RuntimeVersion&&Core.SamePath(values[2],folders[i])&&values[3]=="clean"&&values[4]==jdks[i].Home&&values[5]=="null"&&values[6]=="null"&&values[7]=="bound value",jdks[i].Name+" actual runtime, cwd and clean settings");Assert(result.EndsWith(String.Join(";",trickyArgs)),jdks[i].Name+" Unicode and special arguments round-trip");}
  string target=folders[0];string pinPath=Path.Combine(target,Core.PinName);string original=File.ReadAllText(pinPath);ProjectPin tamper=Core.ReadJson<ProjectPin>(pinPath);tamper.Fingerprints[tamper.Fingerprints.Keys.First()]=new string('0',64);Core.SaveJson(pinPath,tamper);bool rejected=false;try{Core.LoadProject(target,true);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"modified runtime fingerprint rejected without fallback");File.WriteAllText(pinPath,original);
  File.WriteAllText(Path.Combine(target,".java-version"),"wrong-build");rejected=false;try{Core.LoadProject(target,true);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"conflicting project version records rejected");Core.SaveProject(target,jdks[0],new string[0]);
  Assert(Environment.GetEnvironmentVariable("JAVA_HOME")==oldProcessJava&&Environment.GetEnvironmentVariable("JAVA_HOME",EnvironmentVariableTarget.User)==oldUserJava,"project binding and launches do not modify process or user default");
  Assert(Core.FilterPath(@"C:\Windows;C:\Program Files\Common Files\Oracle\Java\javapath;D:\code\Java\JDK21\bin;D:\code\Java\Maven\bin;D:\code\NodeJS")==@"C:\Windows;D:\code\Java\Maven\bin;D:\code\NodeJS","Java Path cleanup preserves Maven and other developer tools");
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification.txt"),"All tests passed at "+DateTimeOffset.Now.ToString("o"));Console.WriteLine("ALL TESTS PASSED");
 }
}
