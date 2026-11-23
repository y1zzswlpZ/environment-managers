using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PythonManager;
class PythonManagerTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static int Main(){try{Test();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static void Wheel(string file,string version){using(FileStream stream=File.Create(file))using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Create)){
  Dictionary<string,string> files=new Dictionary<string,string>{{"ctf_isolation_probe.py","VERSION = '"+version+"'\n"},{"ctf_isolation_probe-"+version+".dist-info/METADATA","Metadata-Version: 2.1\nName: ctf-isolation-probe\nVersion: "+version+"\n"},{"ctf_isolation_probe-"+version+".dist-info/WHEEL","Wheel-Version: 1.0\nGenerator: EnvironmentManagerTests\nRoot-Is-Purelib: true\nTag: py2-none-any\nTag: py3-none-any\n"}};
  foreach(var pair in files){var entry=archive.CreateEntry(pair.Key);using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false)))writer.Write(pair.Value);}string record="ctf_isolation_probe-"+version+".dist-info/RECORD";var r=archive.CreateEntry(record);using(var writer=new StreamWriter(r.Open(),new UTF8Encoding(false))){foreach(string name in files.Keys)writer.WriteLine(name+",,");writer.WriteLine(record+",,");}
 }}
 static void Test(){
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"题目 & 中文 空格 "+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(root);string javaHome=Environment.GetEnvironmentVariable("JAVA_HOME",EnvironmentVariableTarget.User),userPath=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User);
  Environment.SetEnvironmentVariable("PYTHONHOME",@"D:\nonexistent-home");Environment.SetEnvironmentVariable("PYTHONPATH",@"D:\nonexistent-packages");Environment.SetEnvironmentVariable("PIP_TARGET",@"D:\nonexistent-target");Environment.SetEnvironmentVariable("VIRTUAL_ENV",@"D:\other-venv");
  List<string> errors;List<PythonRuntime> list=Core.Scan(out errors);Assert(errors.Count==0&&list.Count==3,"all three installed runtimes detected despite inherited Python overrides");
  string[] wheels={Path.Combine(root,"ctf_isolation_probe-1.0-py2.py3-none-any.whl"),Path.Combine(root,"ctf_isolation_probe-2.0-py2.py3-none-any.whl")};Wheel(wheels[0],"1.0");Wheel(wheels[1],"2.0");
  string script=Path.Combine(root,"中文 script.py");File.WriteAllText(script,"# coding: utf-8\nimport sys, os, json, site, ctf_isolation_probe\ndef text(v):\n return v.decode(sys.getfilesystemencoding()) if isinstance(v,bytes) else v\nprint(json.dumps(dict(version=sys.version,executable=text(sys.executable),prefix=text(sys.prefix),cwd=text(os.getcwd()),args=[text(a) for a in sys.argv[1:]],package=ctf_isolation_probe.VERSION,user_site=site.ENABLE_USER_SITE,pythonhome=os.environ.get('PYTHONHOME'),pythonpath=os.environ.get('PYTHONPATH'),pip_target=os.environ.get('PIP_TARGET'))))\n",new UTF8Encoding(false));
  string[] input={"中文 空格","C:\\folder with spaces\\","quote\"value","& whoami | echo","%PATH%"};
  List<string> folders=new List<string>();int index=0;
  foreach(PythonRuntime runtime in list){string before=Core.Freeze(runtime.Executable);string folder=Path.Combine(root,runtime.Version);Directory.CreateDirectory(folder);folders.Add(folder);Console.WriteLine("Creating "+runtime.Version);Core.CreateProject(folder,runtime,new string[0]);ProjectPin pin=Core.LoadPin(folder,true);Assert(pin.Version==runtime.Version&&pin.Build==runtime.Build,"exact interpreter version/build pinned "+runtime.Version);
   PythonRuntime venv=Core.Inspect(pin.Executable);Assert(venv.IsVirtual&&Core.Same(venv.BasePrefix,runtime.BasePrefix),"dedicated environment and base interpreter "+runtime.Version);
   bool denied=false;try{Core.CreateProject(folder,runtime,new string[0]);}catch(InvalidOperationException){denied=true;}Assert(denied,"existing environment not overwritten "+runtime.Version);
   CommandResult install=Core.Pip(folder,new[]{"install","--no-index","--no-deps",wheels[index%2]});Assert(install.ExitCode==0,"offline wheel installed into project "+runtime.Version+" "+install.Output);pin=Core.LoadPin(folder,true);Assert(pin.Packages.Contains("ctf-isolation-probe")&& !pin.Packages.Contains("DEPRECATION"),"dependency snapshot records installed package, excludes stderr "+runtime.Version);
   ProcessStartInfo info=Core.Script(pin.Executable,script,folder,pin.Options,input);CommandResult run=Core.Run(info);Assert(run.ExitCode==0,"script runs in selected project "+runtime.Version+" "+run.Output);var data=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(run.StandardOutput.Trim());Assert(Core.Same((string)data["executable"],pin.Executable)&&Core.Same((string)data["cwd"],folder)&&(string)data["package"]==(index%2==0?"1.0":"2.0"),"working directory, exact interpreter and per-project dependency "+runtime.Version);Assert(data["pythonhome"]==null&&data["pythonpath"]==null&&data["pip_target"]==null&&!(bool)data["user_site"],"environment variables and user packages isolated "+runtime.Version);string[] actual=((System.Collections.ArrayList)data["args"]).Cast<string>().ToArray();Assert(actual.SequenceEqual(input),"Unicode, spaces, shell metacharacters and quotes preserved "+runtime.Version);
   var terminal=Core.ProcessInfo(pin.Executable,new string[0],folder,true);string first=terminal.EnvironmentVariables["Path"].Split(';')[0];Assert(Core.Same(first,Path.GetDirectoryName(pin.Executable))&&Core.Same(terminal.EnvironmentVariables["VIRTUAL_ENV"],venv.Prefix),"terminal python and pip resolve to project Scripts "+runtime.Version);
   Assert(Core.Freeze(runtime.Executable)==before,"base Python packages unchanged "+runtime.Version);
   string record=Path.Combine(folder,Core.PinName),original=File.ReadAllText(record);pin.BaseHashes[pin.BaseHashes.Keys.First()]=new string('0',64);Core.Save(record,pin);denied=false;try{Core.LoadPin(folder,true);}catch(InvalidOperationException){denied=true;}Assert(denied,"modified interpreter fingerprint rejected "+runtime.Version);File.WriteAllText(record,original,new UTF8Encoding(false));
   string lockFile=Path.Combine(folder,Core.LockName),oldLock=File.ReadAllText(lockFile);File.AppendAllText(lockFile,"wrong==1.0\n");denied=false;try{Core.LoadPin(folder,true);}catch(InvalidOperationException){denied=true;}Assert(denied,"dependency drift rejected "+runtime.Version);File.WriteAllText(lockFile,oldLock,new UTF8Encoding(false));
   index++;
  }
  string working=folders[1];ProjectPin p=Core.LoadPin(working,true);string egg=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(p.Executable)),@"Lib\site-packages\ctf_drift-1.0.dist-info");Directory.CreateDirectory(egg);File.WriteAllText(Path.Combine(egg,"METADATA"),"Metadata-Version: 2.1\nName: ctf-drift\nVersion: 1.0\n");bool drift=false;try{Core.LoadPin(working,true);}catch(InvalidOperationException){drift=true;}Assert(drift,"actual installed package drift rejected");Core.UpdatePin(working,new string[0]);Assert(Core.LoadPin(working,true).Packages.Contains("ctf-drift==1.0"),"explicit update records external dependency changes");
  Assert(userPath==Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User)&&javaHome==Environment.GetEnvironmentVariable("JAVA_HOME",EnvironmentVariableTarget.User),"project operations preserve user Path and Java default");
  Assert(Core.FilterPath(@"D:\code\Java\JDK8u451\bin;D:\code\NodeJS;D:\code\Python\Python314;D:\code\Python\bin")==@"D:\code\Java\JDK8u451\bin;D:\code\NodeJS","Python Path filtering preserves Java and NodeJS");
  Console.WriteLine("ALL TESTS PASSED");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verified.txt"),"All tests passed "+DateTimeOffset.Now.ToString("o"));
 }
}
