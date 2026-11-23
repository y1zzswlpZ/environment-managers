using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace PythonManager {
public sealed class PythonRuntime {
 public string Name {get;set;} public string Executable {get;set;} public string Version {get;set;}
 public string Build {get;set;} public string Architecture {get;set;} public string Prefix {get;set;}
 public string BasePrefix {get;set;} public string Implementation {get;set;} public bool IsVirtual {get;set;}
 public override string ToString(){return Name+"  /  "+Version+"  /  "+Architecture;}
}
public sealed class ProjectPin {
 public int Format {get;set;} public string BaseExecutable {get;set;} public string Executable {get;set;}
 public string Version {get;set;} public string Build {get;set;} public string Architecture {get;set;}
 public string BasePrefix {get;set;} public string[] Options {get;set;}
 public Dictionary<string,string> BaseHashes {get;set;} public Dictionary<string,string> EnvironmentHashes {get;set;}
 public string Packages {get;set;} public string UpdatedAt {get;set;}
}
public sealed class Settings {
 public List<string> AdditionalExecutables {get;set;} public List<string> RecentProjects {get;set;}
 public string DefaultExecutable {get;set;}
}
public sealed class CommandResult {public int ExitCode;public string Output;public string StandardOutput;public string StandardError;}
public static class Core {
 public static readonly string Root=@"D:\code\Python";
 public static readonly string AppRoot=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
 public static readonly string DataRoot=Path.Combine(AppRoot,"data");
 public const string PinName=".python-environment.json";
 public const string EnvironmentName=".ctf-venv";
 public const string LockName="python-environment.lock.txt";
 public static string Json(object obj){return new JavaScriptSerializer().Serialize(obj);}
 public static T Read<T>(string path){return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path,Encoding.UTF8));}
 public static void Write(string path,string contents){
  path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.WriteAllText(temp,contents,new UTF8Encoding(false));
  try{if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static void Save(string path,object data){Write(path,Json(data));}
 public static bool Same(string a,string b){return !String.IsNullOrWhiteSpace(a)&&!String.IsNullOrWhiteSpace(b)&&String.Equals(Path.GetFullPath(a).TrimEnd('\\'),Path.GetFullPath(b).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}
 public static string Quote(string value){StringBuilder b=new StringBuilder("\"");int n=0;foreach(char c in value){if(c=='\\'){n++;continue;}b.Append('\\',c=='"'?n*2+1:n);b.Append(c);n=0;}b.Append('\\',n*2);b.Append('"');return b.ToString();}
 public static string Args(IEnumerable<string> args){return String.Join(" ",args.Select(Quote));}
 public static Settings Config(){string path=Path.Combine(DataRoot,"settings.json");Settings s=File.Exists(path)?Read<Settings>(path):new Settings();if(s==null)throw new InvalidDataException("设置文件损坏，请从 data\\backups 恢复。");if(s.AdditionalExecutables==null)s.AdditionalExecutables=new List<string>();if(s.RecentProjects==null)s.RecentProjects=new List<string>();return s;}
 public static void SaveConfig(Settings s){Save(Path.Combine(DataRoot,"settings.json"),s);}
 public static void Remember(string folder){Settings s=Config();s.RecentProjects.RemoveAll(x=>Same(x,folder));s.RecentProjects.Insert(0,folder);s.RecentProjects=s.RecentProjects.Take(20).ToList();SaveConfig(s);}
 public static void Register(string exe){PythonRuntime p=Inspect(exe);if(p.IsVirtual)throw new InvalidOperationException("版本列表请选择基础 Python；虚拟环境请从题目页管理。");Settings s=Config();if(!s.AdditionalExecutables.Any(x=>Same(x,exe)))s.AdditionalExecutables.Add(Path.GetFullPath(exe));SaveConfig(s);}
 public static List<string> Discover(){
  List<string> paths=new List<string>();
  if(Directory.Exists(Root))foreach(string folder in Directory.GetDirectories(Root)){string exe=Path.Combine(folder,"python.exe");if(File.Exists(exe))paths.Add(exe);}
  foreach(RegistryKey root in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string key in new[]{@"Software\Python\PythonCore",@"Software\WOW6432Node\Python\PythonCore"})using(RegistryKey python=root.OpenSubKey(key)){
   if(python!=null)foreach(string name in python.GetSubKeyNames())using(RegistryKey install=python.OpenSubKey(name+@"\InstallPath")){
    if(install==null)continue;string exe=Convert.ToString(install.GetValue("ExecutablePath"));if(String.IsNullOrEmpty(exe))exe=Path.Combine(Convert.ToString(install.GetValue("")),"python.exe");if(File.Exists(exe))paths.Add(exe);
   }
  }
  paths.AddRange(Config().AdditionalExecutables.Where(File.Exists));return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 static bool IsPythonPath(string entry,IEnumerable<string> exePaths){
  string value=entry.Trim().Trim('"').Replace('/','\\').TrimEnd('\\');
  if(Same(value,Path.Combine(Root,"bin")))return true;
  foreach(string exe in exePaths){string home=Path.GetDirectoryName(exe);if(Same(value,home)||Same(value,Path.Combine(home,"Scripts")))return true;}
  string active=Environment.GetEnvironmentVariable("VIRTUAL_ENV");
  if(!String.IsNullOrEmpty(active)&&(Same(value,active)||Same(value,Path.Combine(active,"Scripts"))))return true;
  return String.Equals(value,"%PYTHON_HOME%",StringComparison.OrdinalIgnoreCase)||String.Equals(value,@"%PYTHON_HOME%\Scripts",StringComparison.OrdinalIgnoreCase);
 }
 public static string FilterPath(string path){List<string> exes=Discover();return String.Join(";",(path??"").Split(';').Where(x=>!IsPythonPath(x,exes)));}
 public static ProcessStartInfo ProcessInfo(string exe,IEnumerable<string> args,string directory,bool visible=false){
  if(!File.Exists(exe))throw new FileNotFoundException("找不到 Python，请检查绑定或重新选择版本。",exe);
  if(!Directory.Exists(directory))throw new DirectoryNotFoundException("找不到工作目录："+directory);
  ProcessStartInfo info=new ProcessStartInfo(exe,Args(args)){UseShellExecute=false,WorkingDirectory=directory,CreateNoWindow=!visible};
  foreach(string key in info.EnvironmentVariables.Keys.Cast<string>().ToArray())if(key.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase)||key.StartsWith("PIP_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("CONDA",StringComparison.OrdinalIgnoreCase)||key.StartsWith("_CE_",StringComparison.OrdinalIgnoreCase)||key=="VIRTUAL_ENV"||key=="VIRTUAL_ENV_PROMPT")info.EnvironmentVariables.Remove(key);
  string home=Path.GetDirectoryName(exe);string venv=null;
  if(Path.GetFileName(home).Equals("Scripts",StringComparison.OrdinalIgnoreCase)&&(File.Exists(Path.Combine(Path.GetDirectoryName(home),"pyvenv.cfg"))||File.Exists(Path.Combine(Path.GetDirectoryName(home),@"Lib\orig-prefix.txt"))))venv=Path.GetDirectoryName(home);
  string machine=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)??"";string user=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User)??"";
  info.EnvironmentVariables["Path"]=home+";"+Path.Combine(home,"Scripts")+";"+FilterPath(machine+";"+user);
  info.EnvironmentVariables["PYTHONNOUSERSITE"]="1";info.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";info.EnvironmentVariables["PYTHONUNBUFFERED"]="1";
  if(venv!=null)info.EnvironmentVariables["VIRTUAL_ENV"]=venv;
  return info;
 }
 public static CommandResult Run(ProcessStartInfo info,int timeout=30000,Action<string> output=null,Action<Process> onStart=null){
  info.RedirectStandardOutput=true;info.RedirectStandardError=true;info.RedirectStandardInput=true;
  info.StandardOutputEncoding=Encoding.UTF8;info.StandardErrorEncoding=Encoding.UTF8;
  StringBuilder result=new StringBuilder(),stdout=new StringBuilder(),stderr=new StringBuilder();object gate=new object();
  using(Process p=new Process()){p.StartInfo=info;DataReceivedEventHandler outHandler=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(gate){result.AppendLine(e.Data);stdout.AppendLine(e.Data);}if(output!=null)output(e.Data);};
   DataReceivedEventHandler errHandler=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(gate){result.AppendLine(e.Data);stderr.AppendLine(e.Data);}if(output!=null)output(e.Data);};
   p.OutputDataReceived+=outHandler;p.ErrorDataReceived+=errHandler;p.Start();if(onStart!=null)onStart(p);p.StandardInput.Close();p.BeginOutputReadLine();p.BeginErrorReadLine();
   if(!p.WaitForExit(timeout)){try{p.Kill();}catch{}p.WaitForExit();throw new TimeoutException("操作超时，环境文件保留，请检查日志。");}p.WaitForExit();return new CommandResult{ExitCode=p.ExitCode,Output=result.ToString(),StandardOutput=stdout.ToString(),StandardError=stderr.ToString()};}
 }
 public static PythonRuntime Inspect(string exe){
  exe=Path.GetFullPath(exe);string code="import sys,struct,json,platform; data=dict(Version=platform.python_version(),Build=sys.version,Architecture=str(struct.calcsize('P')*8)+' bit',Prefix=sys.prefix,BasePrefix=getattr(sys,'base_prefix',getattr(sys,'real_prefix',sys.prefix)),Implementation=platform.python_implementation()); data=dict((k,v.decode(sys.getfilesystemencoding()) if isinstance(v,bytes) else v) for k,v in data.items()); print(json.dumps(data))";
  CommandResult result=Run(ProcessInfo(exe,new[]{"-E","-s","-c",code},Path.GetDirectoryName(exe)),20000);
  if(result.ExitCode!=0)throw new InvalidOperationException("Python 检查失败："+result.Output);
  PythonRuntime runtime=new JavaScriptSerializer().Deserialize<PythonRuntime>(result.StandardOutput.Trim());runtime.Executable=exe;runtime.IsVirtual=!Same(runtime.Prefix,runtime.BasePrefix);runtime.Name=runtime.IsVirtual?"题目虚拟环境":Path.GetFileName(Path.GetDirectoryName(exe));return runtime;
 }
 public static List<PythonRuntime> Scan(out List<string> errors){errors=new List<string>();List<PythonRuntime> list=new List<PythonRuntime>();foreach(string exe in Discover()){try{PythonRuntime p=Inspect(exe);if(!p.IsVirtual)list.Add(p);}catch(Exception e){errors.Add(exe+"："+e.Message);}}return list.OrderBy(x=>new Version(x.Version)).ToList();}
 public static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
 public static Dictionary<string,string> Fingerprints(PythonRuntime p,bool environment){
  string root=environment?p.Prefix:p.BasePrefix;Dictionary<string,string> result=new Dictionary<string,string>();
  List<string> files=new List<string>{p.Executable};
  if(!environment){files.AddRange(Directory.GetFiles(root,"python*.dll"));foreach(string file in new[]{"Lib\\os.py","Lib\\site.py"}){string path=Path.Combine(root,file);if(File.Exists(path))files.Add(path);}}
  else foreach(string file in new[]{"pyvenv.cfg",@"Lib\orig-prefix.txt"}){string path=Path.Combine(root,file);if(File.Exists(path))files.Add(path);}
  foreach(string file in files.Distinct(StringComparer.OrdinalIgnoreCase))result[Path.GetFullPath(file)]=Hash(file);return result;
 }
 static bool EqualHashes(Dictionary<string,string> a,Dictionary<string,string> b){return a!=null&&b!=null&&a.Count==b.Count&&a.All(x=>b.ContainsKey(x.Key)&&b[x.Key]==x.Value);}
 static ProcessStartInfo PipInfo(string exe,IEnumerable<string> arguments,string directory){
  List<string> args=new List<string>{"-s"};
  if(Inspect(exe).Version.StartsWith("2.")){
   // Restrict the legacy Windows encoding shim to pip; CTF scripts keep native Python semantics.
   args.Add("-c");args.Add("import sys,runpy; reload(sys); sys.setdefaultencoding(sys.getfilesystemencoding()); sys.argv=['pip']+sys.argv[1:]; runpy.run_module('pip',run_name='__main__')");
  }else{args.Add("-m");args.Add("pip");}
  args.Add("--isolated");args.Add("--disable-pip-version-check");args.AddRange(arguments);return ProcessInfo(exe,args,directory);
 }
 public static string Freeze(string exe){CommandResult r=Run(PipInfo(exe,new[]{"freeze","--all"},Path.GetDirectoryName(exe)),60000);if(r.ExitCode!=0)throw new InvalidOperationException("读取依赖失败："+r.Output);return NormalizePackages(r.StandardOutput);}
 static string NormalizePackages(string text){return String.Join("\n",text.Replace("\r","").Split('\n').Where(x=>!String.IsNullOrWhiteSpace(x)).OrderBy(x=>x,StringComparer.Ordinal));}
 static void EnsureProject(string folder){if(!Directory.Exists(folder))throw new DirectoryNotFoundException("请选择存在的题目文件夹。");}
 public static string Backup(string path){string directory=Path.Combine(DataRoot,"backups");Directory.CreateDirectory(directory);string file=Path.Combine(directory,Path.GetFileName(path)+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".bak");File.Copy(path,file);return file;}
 public static void SavePin(string folder,PythonRuntime basis,string exe,string[] options){
  EnsureProject(folder);basis=Inspect(basis.Executable);PythonRuntime environment=Inspect(exe);
  if(!environment.IsVirtual||!Same(environment.BasePrefix,basis.BasePrefix)||environment.Build!=basis.Build)throw new InvalidOperationException("虚拟环境与基础 Python 不一致，无法保存绑定。");
  if(!Same(environment.Prefix,Path.Combine(folder,EnvironmentName)))throw new InvalidOperationException("此环境不属于当前题目文件夹。");
  string target=Path.Combine(folder,PinName);string packages=Freeze(exe);
  ProjectPin pin=new ProjectPin{Format=1,BaseExecutable=basis.Executable,Executable=exe,Version=basis.Version,Build=basis.Build,Architecture=basis.Architecture,BasePrefix=basis.BasePrefix,BaseHashes=Fingerprints(basis,false),EnvironmentHashes=Fingerprints(environment,true),Packages=packages,Options=options??new string[0],UpdatedAt=DateTimeOffset.Now.ToString("o")};
  foreach(string file in new[]{target,Path.Combine(folder,LockName),Path.Combine(folder,".python-version")})if(File.Exists(file))Backup(file);
  Write(Path.Combine(folder,".python-version"),pin.Version+Environment.NewLine);Write(Path.Combine(folder,LockName),packages.Replace("\n",Environment.NewLine)+Environment.NewLine);Save(target,pin);Remember(folder);
 }
 public static ProjectPin LoadPin(string folder,bool packages=true){
  EnsureProject(folder);string file=Path.Combine(folder,PinName);if(!File.Exists(file))throw new InvalidOperationException("此题目还没有绑定。请选择基础 Python，点击“创建题目环境”。");
  ProjectPin pin=Read<ProjectPin>(file);if(pin==null||pin.Format!=1||pin.BaseHashes==null||pin.EnvironmentHashes==null)throw new InvalidDataException("题目绑定文件无效。");
  string expected=Path.Combine(folder,EnvironmentName,@"Scripts\python.exe");if(!Same(pin.Executable,expected))throw new InvalidOperationException("题目目录已移动或绑定路径不一致。虚拟环境不能直接搬家，请在原目录使用或重新创建环境。");
  PythonRuntime basis=Inspect(pin.BaseExecutable);PythonRuntime environment=Inspect(pin.Executable);
  if(basis.Build!=pin.Build||basis.Version!=pin.Version||basis.Architecture!=pin.Architecture||environment.Build!=pin.Build||!environment.IsVirtual||!Same(environment.BasePrefix,pin.BasePrefix)||!Same(environment.Prefix,Path.Combine(folder,EnvironmentName))||!EqualHashes(pin.BaseHashes,Fingerprints(basis,false))||!EqualHashes(pin.EnvironmentHashes,Fingerprints(environment,true)))throw new InvalidOperationException("绑定的 Python 或虚拟环境已改变，不会自动换版本。请检查基础环境。");
  string version=Path.Combine(folder,".python-version");if(!File.Exists(version)||File.ReadAllText(version).Trim()!=pin.Version)throw new InvalidOperationException("版本记录不一致，请重新保存当前题目记录。");
  if(packages&&(Freeze(pin.Executable)!=pin.Packages||!File.Exists(Path.Combine(folder,LockName))||NormalizePackages(File.ReadAllText(Path.Combine(folder,LockName)))!=pin.Packages))throw new InvalidOperationException("题目依赖与保存的清单不同。请检查后点击“更新依赖记录”；不会自动安装或替换依赖。");
  return pin;
 }
 public static void CreateProject(string folder,PythonRuntime basis,string[] options,Action<string> output=null){
  folder=Path.GetFullPath(folder);EnsureProject(folder);basis=Inspect(basis.Executable);if(basis.IsVirtual)throw new InvalidOperationException("请选择基础 Python。");
  string target=Path.Combine(folder,EnvironmentName);if(Directory.Exists(target)||File.Exists(target)||File.Exists(Path.Combine(folder,PinName)))throw new InvalidOperationException("此题目已有环境或绑定，未覆盖任何文件。请载入现有绑定，或为新题目选择新的文件夹。");
  List<string> args=new List<string>{"-E","-s"};
  string creator=basis.Executable;
  if(basis.Version.StartsWith("2.")){
   creator=Discover().FirstOrDefault(x=>Same(x,Path.Combine(Root,@"Python39\python.exe")));
   if(creator==null)throw new InvalidOperationException("创建 Python 2 环境需要现有的 Python 3.9 管理组件宿主；请保留 Python39 安装。");
   string tool=Path.Combine(AppRoot,@"tools\legacy-host");if(!File.Exists(Path.Combine(tool,@"virtualenv\__init__.py")))throw new InvalidOperationException("Python 2 虚拟环境管理组件缺失。");
   args.Add("-c");args.Add("import sys; sys.path.insert(0,sys.argv.pop(1)); import virtualenv; virtualenv.cli_run(sys.argv[1:])");args.Add(tool);args.Add("--python");args.Add(basis.Executable);args.Add("--no-download");args.Add("--no-periodic-update");args.Add("--no-vcs-ignore");args.Add("--app-data");args.Add(Path.Combine(DataRoot,"virtualenv-cache"));
  }else{args.Add("-m");args.Add("venv");}
  args.Add(target);CommandResult result=Run(ProcessInfo(creator,args,folder),180000,output);
  if(result.ExitCode!=0)throw new InvalidOperationException("创建环境失败，已有文件已保留：\n"+result.Output);
  SavePin(folder,basis,Path.Combine(target,@"Scripts\python.exe"),options);
 }
 public static void UpdatePin(string folder,string[] options){ProjectPin pin=LoadPin(folder,false);SavePin(folder,Inspect(pin.BaseExecutable),pin.Executable,options);}
 public static CommandResult Pip(string folder,IEnumerable<string> arguments,Action<string> output=null,Action<Process> started=null){
  ProjectPin pin=LoadPin(folder,true);
  CommandResult result=Run(PipInfo(pin.Executable,arguments,folder),600000,output,started);
  if(result.ExitCode==0)SavePin(folder,Inspect(pin.BaseExecutable),pin.Executable,pin.Options);return result;
 }
 public static ProcessStartInfo Script(string exe,string script,string directory,IEnumerable<string> options,IEnumerable<string> arguments){
  script=Path.GetFullPath(script);if(!File.Exists(script))throw new FileNotFoundException("找不到脚本文件。",script);
  List<string> args=new List<string>{"-s"};args.AddRange(options??new string[0]);args.Add(script);args.AddRange(arguments??new string[0]);return ProcessInfo(exe,args,directory);
 }
 public static Process OpenTerminal(string exe,string directory,bool powershell){
  // Build the environment using the selected interpreter, then launch a shell directly.
  ProcessStartInfo info=ProcessInfo(exe,new string[0],directory,true);info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),powershell?@"WindowsPowerShell\v1.0\powershell.exe":"cmd.exe");info.Arguments=Args(powershell?new[]{"-NoLogo","-NoProfile","-NoExit"}:new[]{"/D","/K"});return Process.Start(info);
 }
 public static string DefaultExecutable(){string path=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)+";"+Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User);foreach(string raw in path.Split(';')){if(String.IsNullOrWhiteSpace(raw))continue;string folder=Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));foreach(string suffix in new[]{".com",".exe",".bat",".cmd"}){string file=Path.Combine(folder,"python"+suffix);if(File.Exists(file))return file;}}return null;}
 public static void SetDefault(PythonRuntime selected){
  selected=Inspect(selected.Executable);if(selected.IsVirtual)throw new InvalidOperationException("日常默认必须是基础 Python。");
  string machine=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)??"";
  foreach(string entry in machine.Split(';').Where(x=>!String.IsNullOrWhiteSpace(x))){string home=Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));if(new[]{"python.exe","python.cmd","pip.exe","pip.cmd"}.Any(x=>File.Exists(Path.Combine(home,x))))throw new InvalidOperationException("系统 Path 中存在优先级更高的 Python，请先清理该项：\n"+home+"\n仍可使用本软件打开独立终端和题目环境。");}
  using(RegistryKey key=Registry.CurrentUser.CreateSubKey("Environment")){
   Dictionary<string,object> backup=new Dictionary<string,object>();foreach(string name in key.GetValueNames().Where(x=>x=="Path"||x.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase)))backup[name]=new{Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),Kind=key.GetValueKind(name).ToString()};Save(Path.Combine(DataRoot,"backups","environment-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),backup);
   string raw=Convert.ToString(key.GetValue("Path","",RegistryValueOptions.DoNotExpandEnvironmentNames));RegistryValueKind kind=key.GetValueNames().Contains("Path")?key.GetValueKind("Path"):RegistryValueKind.ExpandString;
   string home=Path.GetDirectoryName(selected.Executable);key.SetValue("Path",home+";"+Path.Combine(home,"Scripts")+";"+FilterPath(raw)+";"+Path.Combine(Root,"bin"),kind);
   // PYTHONHOME/PYTHONPATH can invalidate every venv; archive then remove user overrides.
   key.DeleteValue("PYTHONHOME",false);key.DeleteValue("PYTHONPATH",false);
  }
  Settings config=Config();config.DefaultExecutable=selected.Executable;SaveConfig(config);UIntPtr result;SendMessageTimeout(new IntPtr(0xffff),0x1a,UIntPtr.Zero,"Environment",2,2000,out result);
  if(!Same(DefaultExecutable(),selected.Executable))throw new InvalidOperationException("已保存设置，但检测到其他优先入口。请重新检查 Path。");
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageTimeout(IntPtr w,uint msg,UIntPtr p,string value,uint flags,uint timeout,out UIntPtr result);
}
}
