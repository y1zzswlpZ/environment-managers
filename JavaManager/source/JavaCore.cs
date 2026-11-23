using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace JavaManager {
public sealed class Jdk {
    public string Name { get; set; }
    public string Home { get; set; }
    public string Version { get; set; }
    public string RuntimeVersion { get; set; }
    public string Vendor { get; set; }
    public string Architecture { get; set; }
    public bool JavaFx { get; set; }
    public override string ToString() { return Name + "  /  " + RuntimeVersion; }
}
public sealed class ProjectPin {
    public int Format { get; set; }
    public string Home { get; set; }
    public string Name { get; set; }
    public string RuntimeVersion { get; set; }
    public string Vendor { get; set; }
    public Dictionary<string,string> Fingerprints { get; set; }
    public string[] JvmOptions { get; set; }
    public string SavedAt { get; set; }
}
public sealed class AppSettings {
    public string DefaultHome { get; set; }
    public List<string> RecentProjects { get; set; }
}
public static class Core {
    public static readonly string JavaRoot = @"D:\code\Java";
    public static readonly string AppRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
    public static readonly string DataRoot = Path.Combine(AppRoot,"data");
    public static readonly string PinName = ".java-runtime.json";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static readonly string[] VolatileJavaVariables = {"JAVA_HOME","JRE_HOME","JDK_HOME","CLASSPATH","JAVA_TOOL_OPTIONS","_JAVA_OPTIONS","JDK_JAVA_OPTIONS"};
    public static string Serialize(object value) { return new JavaScriptSerializer().Serialize(value); }
    public static T ReadJson<T>(string path) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path,Encoding.UTF8)); }
    public static void SaveJson(string path,object value) { AtomicWrite(path,Serialize(value)); }
    public static void AtomicWrite(string path,string text) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temporary,text,new UTF8Encoding(false));
        try { if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    public static bool SamePath(string a,string b) {
        if(String.IsNullOrWhiteSpace(a)||String.IsNullOrWhiteSpace(b))return false;
        return String.Equals(Path.GetFullPath(a).TrimEnd('\\'),Path.GetFullPath(b).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);
    }
    public static string Quote(string value) {
        StringBuilder b=new StringBuilder("\""); int slashes=0;
        foreach(char c in value) { if(c=='\\'){slashes++;continue;} b.Append('\\',c=='"'?slashes*2+1:slashes);b.Append(c);slashes=0; }
        b.Append('\\',slashes*2);b.Append('"');return b.ToString();
    }
    public static string Arguments(IEnumerable<string> args) { return String.Join(" ",args.Select(Quote)); }
    public static bool ManagedJavaEntry(string entry) {
        string s=entry.Trim().Trim('"').Replace('/','\\').TrimEnd('\\');
        string[] oracle={@"C:\Program Files\Common Files\Oracle\Java\javapath",@"C:\Program Files (x86)\Common Files\Oracle\Java\java8path",@"C:\Program Files (x86)\Common Files\Oracle\Java\javapath"};
        if(oracle.Any(x=>String.Equals(x,s,StringComparison.OrdinalIgnoreCase)))return true;
        if(s.StartsWith(JavaRoot+"\\",StringComparison.OrdinalIgnoreCase)) {
            string relative=s.Substring(JavaRoot.Length+1); string first=relative.Split('\\')[0];
            return first.StartsWith("JDK",StringComparison.OrdinalIgnoreCase)||first.StartsWith("JRE",StringComparison.OrdinalIgnoreCase)||first=="JavaTools"||first=="JavaCurrent";
        }
        return new[]{"%JAVA_HOME%\\bin","%JRE_HOME%\\bin","%JDK_HOME%\\bin"}.Any(x=>String.Equals(x,s,StringComparison.OrdinalIgnoreCase));
    }
    public static string FilterPath(string path) { return String.Join(";",(path??"").Split(';').Where(x=>!ManagedJavaEntry(x))); }
    public static ProcessStartInfo EnvironmentFor(Jdk jdk,string exe,IEnumerable<string> args,string directory,bool console) {
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException("找不到工作目录："+directory);
        ProcessStartInfo info=new ProcessStartInfo(exe,Arguments(args));
        info.UseShellExecute=false;info.WorkingDirectory=directory;info.CreateNoWindow=!console;
        foreach(string key in VolatileJavaVariables)info.EnvironmentVariables.Remove(key);
        info.EnvironmentVariables["JAVA_HOME"]=jdk.Home;
        string machine=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)??"";
        string user=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User)??"";
        info.EnvironmentVariables["Path"]=Path.Combine(jdk.Home,"bin")+";"+FilterPath(machine+";"+user);
        return info;
    }
    public static string Capture(ProcessStartInfo info,out int code) {
        info.RedirectStandardOutput=true;info.RedirectStandardError=true;info.CreateNoWindow=true;
        using(Process p=new Process()) {
            p.StartInfo=info;p.Start();
            Task<string> output=p.StandardOutput.ReadToEndAsync();Task<string> error=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(20000)){try{p.Kill();}catch{}throw new TimeoutException("Java 检查超时。");}
            Task.WaitAll(output,error);code=p.ExitCode;return output.Result+error.Result;
        }
    }
    public static Jdk Inspect(string home) {
        home=Path.GetFullPath(home).TrimEnd('\\');
        if(!File.Exists(Path.Combine(home,"bin","java.exe"))||!File.Exists(Path.Combine(home,"bin","javac.exe")))throw new InvalidOperationException("请选择包含 java.exe 和 javac.exe 的 JDK 根目录。");
        Jdk j=new Jdk {Home=home,Name=Path.GetFileName(home)};
        int exit;string text=Capture(EnvironmentFor(j,Path.Combine(home,"bin","java.exe"),new[]{"-XshowSettings:properties","-version"},home,false),out exit);
        if(exit!=0)throw new InvalidOperationException("Java 无法启动："+home+"\n"+text);
        Dictionary<string,string> properties=new Dictionary<string,string>();
        foreach(string line in text.Split('\n')){int split=line.IndexOf(" = ",StringComparison.Ordinal);if(split>0)properties[line.Substring(0,split).Trim()]=line.Substring(split+3).Trim();}
        j.Version=properties.ContainsKey("java.version")?properties["java.version"]:"未知";
        j.RuntimeVersion=properties.ContainsKey("java.runtime.version")?properties["java.runtime.version"]:j.Version;
        j.Vendor=properties.ContainsKey("java.vendor")?properties["java.vendor"]:"未知";
        j.Architecture=properties.ContainsKey("os.arch")?properties["os.arch"]:"未知";
        j.JavaFx=File.Exists(Path.Combine(home,@"jre\lib\ext\jfxrt.jar"));
        if(!j.Version.StartsWith("1.")){string release=Path.Combine(home,"release");if(File.Exists(release))j.JavaFx=File.ReadAllText(release).Contains("javafx.controls");}
        return j;
    }
    public static List<Jdk> Scan(out List<string> errors) {
        errors=new List<string>();List<Jdk> result=new List<Jdk>();
        foreach(string folder in Directory.GetDirectories(JavaRoot).Where(x=>Path.GetFileName(x).StartsWith("JDK",StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)) {
            try{result.Add(Inspect(folder));}catch(Exception e){errors.Add(Path.GetFileName(folder)+"："+e.Message);}
        }
        return result;
    }
    public static string Hash(string path) { using(SHA256 hash=SHA256.Create())using(FileStream stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",""); }
    private static Dictionary<string,string> Fingerprints(Jdk j) {
        Dictionary<string,string> hashes=new Dictionary<string,string>();
        foreach(string relative in new[]{"release",@"bin\java.exe",@"bin\javac.exe",@"jre\lib\rt.jar",@"jre\bin\server\jvm.dll",@"lib\modules",@"bin\server\jvm.dll",@"jre\lib\ext\jfxrt.jar"}) {
            string path=Path.Combine(j.Home,relative);if(File.Exists(path))hashes.Add(relative,Hash(path));
        }
        return hashes;
    }
    public static void SaveProject(string directory,Jdk j,string[] options) {
        directory=Path.GetFullPath(directory);if(!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
        j=Inspect(j.Home);
        ProjectPin pin=new ProjectPin {Format=1,Home=j.Home,Name=j.Name,RuntimeVersion=j.RuntimeVersion,Vendor=j.Vendor,Fingerprints=Fingerprints(j),JvmOptions=options??new string[0],SavedAt=DateTimeOffset.Now.ToString("o")};
        string target=Path.Combine(directory,PinName);
        if(File.Exists(target))BackupFile(target,"project-binding");
        AtomicWrite(Path.Combine(directory,".java-version"),j.RuntimeVersion+Environment.NewLine);
        SaveJson(target,pin);RememberProject(directory);
    }
    public static ProjectPin LoadProject(string directory,bool validate) {
        string file=Path.Combine(directory,PinName);
        if(!File.Exists(file))throw new InvalidOperationException("该题目还未绑定 Java。请先选择版本并保存绑定。");
        ProjectPin pin=ReadJson<ProjectPin>(file);
        if(pin==null||pin.Format!=1||String.IsNullOrWhiteSpace(pin.Home)||pin.Fingerprints==null||pin.Fingerprints.Count==0)throw new InvalidDataException("题目绑定文件无效，请重新绑定。");
        if(validate) {
            Jdk j=Inspect(pin.Home);
            if(j.RuntimeVersion!=pin.RuntimeVersion||j.Vendor!=pin.Vendor)throw new InvalidOperationException("题目绑定的 JDK 已更换版本，请检查后重新绑定。不会自动换用其他 Java。");
            Dictionary<string,string> actual=Fingerprints(j);
            if(actual.Count!=pin.Fingerprints.Count||actual.Any(pair=>!pin.Fingerprints.ContainsKey(pair.Key)||pin.Fingerprints[pair.Key]!=pair.Value))throw new InvalidOperationException("题目绑定的 JDK 文件已变化，请检查后重新绑定。不会自动换用其他 Java。");
            string versionFile=Path.Combine(directory,".java-version");
            if(!File.Exists(versionFile)||File.ReadAllText(versionFile).Trim()!=pin.RuntimeVersion)throw new InvalidOperationException("两个版本记录不一致，请在软件中重新保存绑定。");
        }
        return pin;
    }
    public static AppSettings Settings() {
        string path=Path.Combine(DataRoot,"settings.json");
        AppSettings settings=File.Exists(path)?ReadJson<AppSettings>(path):new AppSettings();
        if(settings.RecentProjects==null)settings.RecentProjects=new List<string>();return settings;
    }
    public static void RememberProject(string folder) {
        AppSettings settings=Settings();settings.RecentProjects.RemoveAll(x=>SamePath(x,folder));settings.RecentProjects.Insert(0,folder);settings.RecentProjects=settings.RecentProjects.Take(20).ToList();SaveJson(Path.Combine(DataRoot,"settings.json"),settings);
    }
    public static string BackupFile(string path,string prefix) {
        string directory=Path.Combine(DataRoot,"backups");Directory.CreateDirectory(directory);
        string dest=Path.Combine(directory,prefix+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");File.Copy(path,dest);return dest;
    }
    public static bool MachineConflicts() { return (Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.Machine)??"").Split(';').Any(ManagedJavaEntry); }
    public static bool IsAdmin() { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
    public static void CleanMachinePath() {
        if(!IsAdmin())throw new UnauthorizedAccessException("清理系统中的 Oracle Java 入口需要 Windows 管理员确认。");
        const string keyPath=@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        using(RegistryKey key=Registry.LocalMachine.OpenSubKey(keyPath,true)) {
            string raw=(string)key.GetValue("Path","",RegistryValueOptions.DoNotExpandEnvironmentNames);
            string filtered=FilterPath(raw);
            if(raw!=filtered){
                string backup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"JavaManager","backups");Directory.CreateDirectory(backup);
                SaveJson(Path.Combine(backup,"machine-path-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),new {OriginalPath=raw,Kind=key.GetValueKind("Path").ToString()});
                key.SetValue("Path",filtered,key.GetValueKind("Path"));
            }
        }
        Notify();
    }
    public static void ApplyUserDefault(Jdk j) {
        j=Inspect(j.Home);
        using(RegistryKey key=Registry.CurrentUser.CreateSubKey("Environment")) {
            Dictionary<string,object> backup=new Dictionary<string,object>();
            foreach(string name in key.GetValueNames().Where(x=>x=="Path"||x.StartsWith("JAVA",StringComparison.OrdinalIgnoreCase)||x.StartsWith("JRE",StringComparison.OrdinalIgnoreCase)||x=="JDK_HOME"))backup[name]=new{Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),Kind=key.GetValueKind(name).ToString()};
            string file=Path.Combine(DataRoot,"backups","user-environment-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");SaveJson(file,backup);
            string raw=(string)key.GetValue("Path","",RegistryValueOptions.DoNotExpandEnvironmentNames);
            RegistryValueKind kind=key.GetValueNames().Contains("Path")?key.GetValueKind("Path"):RegistryValueKind.ExpandString;
            foreach(string name in key.GetValueNames().Where(x=>x.StartsWith("JAVA",StringComparison.OrdinalIgnoreCase)||x.StartsWith("JRE",StringComparison.OrdinalIgnoreCase)).ToArray()) {
                if(String.IsNullOrEmpty(Convert.ToString(key.GetValue(name))))key.DeleteValue(name,false);
            }
            key.SetValue("JAVA_HOME",j.Home,RegistryValueKind.String);
            key.DeleteValue("JRE_HOME",false);key.DeleteValue("JDK_HOME",false);
            key.SetValue("Path",Path.Combine(j.Home,"bin")+";"+FilterPath(raw),kind);
        }
        AppSettings settings=Settings();settings.DefaultHome=j.Home;SaveJson(Path.Combine(DataRoot,"settings.json"),settings);Notify();
    }
    public static bool ApplyDefault(Jdk j,bool elevate) {
        if(MachineConflicts()) {
            if(IsAdmin())CleanMachinePath();
            else if(elevate) {
                ProcessStartInfo info=new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"--clean-machine-path");info.UseShellExecute=true;info.Verb="runas";
                using(Process p=Process.Start(info)){if(!p.WaitForExit(120000))throw new TimeoutException("管理员确认尚未完成，请稍后重试。");if(p.ExitCode!=0||MachineConflicts())throw new InvalidOperationException("系统 Java 入口未清理，默认切换尚未完成。");}
            } else return false;
        }
        ApplyUserDefault(j);return true;
    }
    public static Process OpenTerminal(Jdk j,string directory,bool powershell) {
        string exe=powershell?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe");
        return Process.Start(EnvironmentFor(j,exe,powershell?new[]{"-NoLogo","-NoProfile","-NoExit"}:new[]{"/D","/K"},directory,true));
    }
    public static ProcessStartInfo JarInfo(Jdk j,string jar,string directory,string[] options,string[] args) {
        jar=Path.GetFullPath(jar);if(!File.Exists(jar))throw new FileNotFoundException("找不到 JAR 文件。",jar);
        List<string> list=new List<string>(options??new string[0]);list.Add("-jar");list.Add(jar);list.AddRange(args??new string[0]);
        return EnvironmentFor(j,Path.Combine(j.Home,"bin","java.exe"),list,directory,false);
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SendMessageTimeout(IntPtr h,uint m,UIntPtr p,string t,uint f,uint timeout,out UIntPtr result);
    public static void Notify(){UIntPtr value;SendMessageTimeout(new IntPtr(0xffff),0x001a,UIntPtr.Zero,"Environment",2,2000,out value);}
}
}
