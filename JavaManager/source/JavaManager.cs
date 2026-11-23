using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JavaManager {
static class Program {
    [STAThread]static int Main(string[] args) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0&&args[0]=="--clean-machine-path"){Core.CleanMachinePath();return 0;}
            if(args.Length==3&&args[0]=="--set-default") {
                Jdk j=Core.Inspect(args[1]);bool applied=Core.ApplyDefault(j,true);Core.SaveJson(args[2],new {Applied=applied,Home=j.Home,MachineConflicts=Core.MachineConflicts()});return applied?0:2;
            }
            Application.Run(new ManagerForm());return 0;
        }catch(Exception e){MessageBox.Show(e.Message,"Java 环境管家",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
sealed class ManagerForm:Form {
    List<Jdk> jdks=new List<Jdk>();ListView list;ComboBox projectJava,runJava,recent;
    TextBox projectFolder,projectOptions,jarFile,runOptions,runArgs,output;CheckBox useProject;
    Label defaultLabel,notice,projectStatus,runHint;TabControl tabs;Button runButton;Process running;bool activeRun;
    readonly Color blue=Color.FromArgb(35,91,200);
    public ManagerForm() {
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text="Java 环境管家";Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(1060,780);MinimumSize=new Size(970,760);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(245,247,251);
        TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=4};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
        Label heading=new Label{Text="Java 环境管家",Font=new Font(Font.FontFamily,21,FontStyle.Bold),Dock=DockStyle.Fill,ForeColor=Color.FromArgb(25,40,64)};layout.Controls.Add(heading);
        defaultLabel=new Label{Dock=DockStyle.Fill,AutoEllipsis=true};layout.Controls.Add(defaultLabel);
        tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(20,8)};layout.Controls.Add(tabs);
        BuildRuntimes();BuildProjects();BuildRunner();
        notice=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(60,80,110)};layout.Controls.Add(notice);
        Shown+=async delegate{await RefreshRuntimes();};
        Activated+=delegate{if(jdks.Count>0)UpdateDefault();};
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(activeRun){e.Cancel=true;MessageBox.Show(this,"有 JAR 正在运行。请先结束程序，或使用“停止本次运行”。","程序仍在运行");}};
    }
    Button Button(string text,Action action,bool primary=false) {
        Button b=new Button{Text=text,AutoSize=true,Height=38,MinimumSize=new Size(110,38),Padding=new Padding(10,4,10,4),Margin=new Padding(0,4,10,4),FlatStyle=FlatStyle.Flat,BackColor=primary?blue:Color.White,ForeColor=primary?Color.White:Color.FromArgb(30,45,65)};b.FlatAppearance.BorderColor=Color.FromArgb(210,219,234);b.Click+=delegate{Safe(action);};return b;
    }
    void Safe(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    async Task Work(Func<Task> action){UseWaitCursor=true;try{await action();}catch(Exception e){MessageBox.Show(this,e.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{UseWaitCursor=false;}}
    Label Label(string text){return new Label{Text=text,AutoSize=true,Margin=new Padding(0,3,0,3)};}
    TextBox TextBox(bool multi=false){return new TextBox{Dock=DockStyle.Fill,Multiline=multi,ScrollBars=multi?ScrollBars.Vertical:ScrollBars.None,Margin=new Padding(0,3,10,3)};}
    FlowLayoutPanel Row(){return new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoSize=true,Margin=new Padding(0)};}
    TabPage Page(string name){TabPage p=new TabPage(name){BackColor=Color.White,Padding=new Padding(18)};tabs.TabPages.Add(p);return p;}
    TableLayoutPanel Grid(TabPage page,int rows){TableLayoutPanel g=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows};page.Controls.Add(g);return g;}
    void BuildRuntimes(){
        var page=Page("Java 版本");var grid=Grid(page,4);grid.RowStyles.Add(new RowStyle(SizeType.Absolute,48));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,54));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
        grid.Controls.Add(Label("选择一个版本，设为日常默认，或打开只使用该版本的独立终端。"));
        list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.FixedSingle};list.Columns.Add("JDK",125);list.Columns.Add("完整运行版本",220);list.Columns.Add("架构",85);list.Columns.Add("JavaFX",85);list.Columns.Add("安装目录",390);grid.Controls.Add(list);
        var buttons=Row();var set=Button("设为日常默认",delegate{},true);set.Click+=async delegate{await Work(async delegate{Jdk j=Selected();notice.Text=Core.MachineConflicts()?"首次设置需要清理系统 Java 入口，请在 Windows 管理员确认窗口中允许。":"正在保存默认环境……";bool ok=await Task.Run(()=>Core.ApplyDefault(j,true));if(ok){UpdateDefault();notice.Text="已设为 "+j.Name+"。新终端生效；已打开的终端和题目绑定保持原版本。";}});};buttons.Controls.Add(set);
        buttons.Controls.Add(Button("打开 CMD",()=>Core.OpenTerminal(Selected(),Core.JavaRoot,false)));buttons.Controls.Add(Button("打开 PowerShell",()=>Core.OpenTerminal(Selected(),Core.JavaRoot,true)));var refresh=Button("重新扫描",delegate{});refresh.Click+=async delegate{await RefreshRuntimes();};buttons.Controls.Add(refresh);grid.Controls.Add(buttons);
        grid.Controls.Add(Label("独立终端之间互不影响。首次设置默认版本，Windows 可能要求管理员确认。\nJDK8u451 用作日常默认；8u65 / 8u181 可按 CTF 题目要求选择。"));
    }
    void BuildProjects(){
        var page=Page("CTF 题目固定版本");var grid=Grid(page,10);foreach(float h in new[]{32,44,34,48,34,42,112,62,50,58})grid.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        grid.Controls.Add(Label("最近打开的题目"));recent=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};recent.SelectedIndexChanged+=delegate{if(recent.SelectedItem!=null){projectFolder.Text=recent.SelectedItem.ToString();Safe(LoadBinding);}};grid.Controls.Add(recent);
        grid.Controls.Add(Label("题目文件夹"));var folderRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));projectFolder=TextBox();projectFolder.AccessibleName="题目文件夹";folderRow.Controls.Add(projectFolder);folderRow.Controls.Add(Button("选择文件夹",BrowseProject));grid.Controls.Add(folderRow);
        grid.Controls.Add(Label("绑定的 JDK（包含完整更新号和构建号）"));projectJava=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};grid.Controls.Add(projectJava);
        var options=new GroupBox{Text="该题目的 JVM 参数（可留空，每行一个；用于下方 JAR 运行功能）",Dock=DockStyle.Fill,Padding=new Padding(10,22,10,9)};projectOptions=TextBox(true);projectOptions.AccessibleName="题目 JVM 参数";options.Controls.Add(projectOptions);grid.Controls.Add(options);
        projectStatus=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Color.FromArgb(75,90,110)};grid.Controls.Add(projectStatus);
        var buttons=Row();var save=Button("保存题目绑定",delegate{},true);save.Click+=async delegate{await Work(async delegate{string folder=ProjectFolder();Jdk j=(Jdk)projectJava.SelectedItem;if(j==null)throw new InvalidOperationException("请选择题目使用的 JDK。");string[] opts=Lines(projectOptions);await Task.Run(()=>Core.SaveProject(folder,j,opts));LoadBinding();RefreshRecent(folder);notice.Text="题目已固定为 "+j.RuntimeVersion+"，以后切换默认 Java 不会影响此题。";});};buttons.Controls.Add(save);
        buttons.Controls.Add(Button("题目 CMD",()=>OpenProjectTerminal(false)));buttons.Controls.Add(Button("题目 PowerShell",()=>OpenProjectTerminal(true)));buttons.Controls.Add(Button("用绑定版本运行 JAR",()=>{Core.LoadProject(ProjectFolder(),true);useProject.Checked=true;tabs.SelectedIndex=2;}));grid.Controls.Add(buttons);
        grid.Controls.Add(Label("绑定会记录完整版本、厂商和核心文件指纹。JDK 被替换或文件缺失时会停止并提示重新绑定。\n请从这里打开题目终端；IDE 内的项目 SDK 和 Maven / Gradle JVM 仍需选择同一版本。"));
    }
    void BuildRunner(){
        var page=Page("运行 JAR");var grid=Grid(page,10);foreach(float h in new[]{36,42,34,48,36,114,50})grid.RowStyles.Add(new RowStyle(SizeType.Absolute,h));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,30));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
        useProject=new CheckBox{Text="使用“CTF 题目”页已保存的绑定（含 JVM 参数，工作目录为题目文件夹）",Dock=DockStyle.Fill};useProject.CheckedChanged+=delegate{runJava.Enabled=!useProject.Checked;runOptions.Enabled=!useProject.Checked;};grid.Controls.Add(useProject);
        runJava=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};grid.Controls.Add(runJava);grid.Controls.Add(Label("JAR 文件"));var fileRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));jarFile=TextBox();jarFile.AccessibleName="JAR 文件";jarFile.AllowDrop=true;jarFile.DragEnter+=delegate(object sender,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};jarFile.DragDrop+=delegate(object sender,DragEventArgs e){jarFile.Text=((string[])e.Data.GetData(DataFormats.FileDrop))[0];};fileRow.Controls.Add(jarFile);fileRow.Controls.Add(Button("选择 JAR",delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="Java 程序 (*.jar)|*.jar",CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK)jarFile.Text=d.FileName;}));grid.Controls.Add(fileRow);
        grid.Controls.Add(Label("参数每行一个，无需额外添加引号。未使用题目绑定时，工作目录为 JAR 所在文件夹。"));
        var argsGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};argsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));argsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));var jvm=new GroupBox{Text="JVM 参数（可留空）",Dock=DockStyle.Fill,Padding=new Padding(10,24,10,8)};runOptions=TextBox(true);runOptions.AccessibleName="运行 JVM 参数";jvm.Controls.Add(runOptions);var app=new GroupBox{Text="程序参数（可留空）",Dock=DockStyle.Fill,Padding=new Padding(10,24,10,8)};runArgs=TextBox(true);runArgs.AccessibleName="程序参数";app.Controls.Add(runArgs);argsGrid.Controls.Add(jvm);argsGrid.Controls.Add(app);grid.Controls.Add(argsGrid);
        var buttons=Row();runButton=Button("运行 JAR",delegate{},true);runButton.Click+=async delegate{await RunJar();};buttons.Controls.Add(runButton);buttons.Controls.Add(Button("停止本次运行",delegate{if(running!=null&&!running.HasExited)running.Kill();}));buttons.Controls.Add(Button("打开日志文件夹",delegate{Directory.CreateDirectory(Path.Combine(Core.DataRoot,"logs"));Process.Start("explorer.exe",Core.Quote(Path.Combine(Core.DataRoot,"logs")));}));grid.Controls.Add(buttons);
        output=TextBox(true);output.ReadOnly=true;output.BackColor=Color.FromArgb(247,249,252);output.Font=new Font("Consolas",10);grid.Controls.Add(output);runHint=new Label{Dock=DockStyle.Fill,Text="控制台输入型程序请在题目终端运行；这里显示输出并自动保存日志。",AutoEllipsis=true};grid.Controls.Add(runHint);
    }
    Jdk Selected(){if(list.SelectedItems.Count!=1)throw new InvalidOperationException("请先在列表中选择一个 JDK。");return (Jdk)list.SelectedItems[0].Tag;}
    string ProjectFolder(){string folder=Path.GetFullPath(projectFolder.Text.Trim());if(!Directory.Exists(folder))throw new DirectoryNotFoundException("请选择存在的题目文件夹。");return folder;}
    string[] Lines(TextBox box){return box.Lines.Where(x=>!String.IsNullOrWhiteSpace(x)).ToArray();}
    void BrowseProject(){using(FolderBrowserDialog d=new FolderBrowserDialog{Description="选择 CTF 题目文件夹"}){if(Directory.Exists(projectFolder.Text))d.SelectedPath=projectFolder.Text;if(d.ShowDialog(this)==DialogResult.OK){projectFolder.Text=d.SelectedPath;LoadBinding();}}}
    void LoadBinding(){
        string folder=ProjectFolder();string path=Path.Combine(folder,Core.PinName);
        if(!File.Exists(path)){projectStatus.Text="此题目尚未绑定。请选择版本，再点击“保存题目绑定”。";projectOptions.Clear();return;}
        ProjectPin pin=Core.LoadProject(folder,false);projectOptions.Lines=pin.JvmOptions??new string[0];var j=jdks.FirstOrDefault(x=>Core.SamePath(x.Home,pin.Home));projectJava.SelectedItem=j;
        projectStatus.Text="已保存："+pin.Name+" / "+pin.RuntimeVersion+" / "+pin.Vendor+"\n每次打开题目终端或运行 JAR 前，都会验证版本与文件指纹。";
    }
    void RefreshRecent(string keep=null){recent.Items.Clear();foreach(string folder in Core.Settings().RecentProjects)recent.Items.Add(folder);if(keep!=null)recent.SelectedItem=keep;}
    void OpenProjectTerminal(bool ps){string folder=ProjectFolder();ProjectPin pin=Core.LoadProject(folder,true);Core.OpenTerminal(Core.Inspect(pin.Home),folder,ps);Core.RememberProject(folder);notice.Text="已打开题目独立终端："+pin.RuntimeVersion+"。题目 JVM 参数保留在绑定中，由运行 JAR 功能应用。";}
    void UpdateDefault(){string home=Environment.GetEnvironmentVariable("JAVA_HOME",EnvironmentVariableTarget.User);Jdk j=jdks.FirstOrDefault(x=>Core.SamePath(x.Home,home));defaultLabel.Text="日常默认："+(j==null?"尚未配置":j.Name+" / "+j.RuntimeVersion)+(Core.MachineConflicts()?"    · 系统 Java 入口存在冲突，设置默认时会处理":"");}
    async Task RefreshRuntimes(){await Work(async delegate{List<string> errors=null;jdks=await Task.Run(()=>Core.Scan(out errors));list.Items.Clear();projectJava.Items.Clear();runJava.Items.Clear();foreach(Jdk j in jdks){list.Items.Add(new ListViewItem(new[]{j.Name,j.RuntimeVersion,j.Architecture,j.JavaFx?"有":"无",j.Home}){Tag=j});projectJava.Items.Add(j);runJava.Items.Add(j);}Jdk preferred=jdks.FirstOrDefault(j=>j.Name=="JDK8u451")??jdks.FirstOrDefault();if(preferred!=null){list.Items[jdks.IndexOf(preferred)].Selected=true;projectJava.SelectedItem=preferred;runJava.SelectedItem=preferred;}RefreshRecent();UpdateDefault();notice.Text="已识别 "+jdks.Count+" 套 JDK。"+(errors.Count>0?" 扫描提示："+String.Join("；",errors):"选择版本即可操作，无需记命令。");});}
    void Append(string text){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<string>(Append),text);return;}if(output.TextLength>180000)output.Text=output.Text.Substring(output.TextLength-100000);output.AppendText(text);}
    async Task RunJar(){
        if(activeRun)return;
        string jar=jarFile.Text.Trim();string folder=null;Jdk j=null;string[] opts=null;
        try{if(useProject.Checked){folder=ProjectFolder();ProjectPin pin=Core.LoadProject(folder,true);j=Core.Inspect(pin.Home);opts=pin.JvmOptions??new string[0];}else{j=(Jdk)runJava.SelectedItem;if(j==null)throw new InvalidOperationException("请选择 Java 版本。");j=Core.Inspect(j.Home);folder=Path.GetDirectoryName(Path.GetFullPath(jar));opts=Lines(runOptions);}ProcessStartInfo info=Core.JarInfo(j,jar,folder,opts,Lines(runArgs));info.RedirectStandardOutput=true;info.RedirectStandardError=true;info.RedirectStandardInput=true;
            Directory.CreateDirectory(Path.Combine(Core.DataRoot,"logs"));string log=Path.Combine(Core.DataRoot,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");
            output.Clear();Append("Java："+j.RuntimeVersion+"\r\n目录："+folder+"\r\n日志："+log+"\r\n\r\n");activeRun=true;runButton.Enabled=false;useProject.Enabled=false;running=new Process{StartInfo=info};
            using(StreamWriter writer=new StreamWriter(log,false,new UTF8Encoding(false))){writer.AutoFlush=true;writer.WriteLine("Java="+j.Home+"; Runtime="+j.RuntimeVersion);writer.WriteLine("WorkingDirectory="+folder);writer.WriteLine("Arguments="+info.Arguments);object logLock=new object();DataReceivedEventHandler handler=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(logLock)writer.WriteLine(e.Data);Append(e.Data+Environment.NewLine);};running.OutputDataReceived+=handler;running.ErrorDataReceived+=handler;running.Start();running.StandardInput.Close();running.BeginOutputReadLine();running.BeginErrorReadLine();await Task.Run(()=>running.WaitForExit());writer.WriteLine("ExitCode="+running.ExitCode);Append("\r\n程序已结束，退出代码："+running.ExitCode+"\r\n");notice.Text="本次运行完成；默认 Java 和其他终端未改变。";}
        }catch(Exception e){MessageBox.Show(this,e.Message,"JAR 未能运行",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{if(running!=null){running.Dispose();running=null;}activeRun=false;runButton.Enabled=true;useProject.Enabled=true;}
    }
}
}
