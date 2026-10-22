using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Synups.Desktop.Integrations;

public enum LoginPlatform { Windows, MacOS, Linux, Unsupported }
public sealed record LoginPlan(LoginPlatform Platform,string Name,string? FileName,string Payload)
{
    public static LoginPlan Create(LoginPlatform platform,string executable,string dataDirectory,string home,string config)
    {
        foreach(var path in new[]{executable,dataDirectory,home,config})
            if(!Path.IsPathFullyQualified(path) || path.Any(c=>char.IsControl(c)))throw new ArgumentException("Login registration requires absolute paths without control characters.");
        var normalized=Path.GetFullPath(dataDirectory);if(platform==LoginPlatform.Windows)normalized=normalized.ToUpperInvariant();
        var id="com.synups.desktop."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16].ToLowerInvariant();
        static string QuoteWindows(string value)
        {
            var text=new StringBuilder("\"");var slashes=0;
            foreach(var c in value)
            {
                if(c=='\\'){slashes++;continue;}
                if(c=='"')text.Append('\\',slashes*2+1).Append(c);
                else text.Append('\\',slashes).Append(c);
                slashes=0;
            }
            return text.Append('\\',slashes*2).Append('"').ToString();
        }
        static string QuoteDesktop(string value)
        {
            // Desktop Entry string unescaping happens before Exec argument unquoting.
            var escaped=value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("`","\\`").Replace("$","\\$").Replace("%","%%");
            return "\""+escaped.Replace("\\","\\\\")+"\"";
        }
        if(platform==LoginPlatform.Linux && executable.Contains('='))throw new ArgumentException("The desktop-entry specification does not support '=' in the executable path. Move the app before enabling login registration.");
        return platform switch
        {
            LoginPlatform.Windows=>new(platform,id,null,$"{QuoteWindows(executable)} --minimized --data-directory {QuoteWindows(dataDirectory)}"),
            LoginPlatform.MacOS=>new(platform,id,Path.Combine(home,"Library","LaunchAgents",id+".plist"),
                new XDocument(new XDeclaration("1.0","utf-8",null),new XElement("plist",new XAttribute("version","1.0"),new XElement("dict",
                    new XElement("key","Label"),new XElement("string",id),new XElement("key","ProgramArguments"),
                    new XElement("array",new[]{executable,"--minimized","--data-directory",dataDirectory}.Select(s=>new XElement("string",s))),new XElement("key","RunAtLoad"),new XElement("true")))).ToString()),
            LoginPlatform.Linux=>new(platform,id,Path.Combine(config,"autostart",id+".desktop"),
                $"[Desktop Entry]\nType=Application\nName=Synups\nComment=Start the explicitly registered Synups workspace\nExec={QuoteDesktop(executable)} --minimized --data-directory {QuoteDesktop(dataDirectory)}\nTerminal=false\nX-GNOME-Autostart-enabled=true\n"),
            _=>new(platform,id,null,"")
        };
    }
}
public sealed record LoginStatus(bool Registered,bool CanChange,string Detail);
/// <summary>Only an explicitly confirmed action writes registration. Read/normal startup never installs or executes it.</summary>
public sealed class LoginRegistration(LoginPlan plan,bool packagedExecutable=true)
{
    public static LoginRegistration ForCurrentProcess(string dataDirectory)
    {
        var executable=Environment.ProcessPath??"";
        var platform=OperatingSystem.IsWindows()?LoginPlatform.Windows:OperatingSystem.IsMacOS()?LoginPlatform.MacOS:OperatingSystem.IsLinux()?LoginPlatform.Linux:LoginPlatform.Unsupported;
        var config=Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new(LoginPlan.Create(platform,executable,dataDirectory,home,Path.IsPathFullyQualified(config??"")?config!:Path.Combine(home,".config")),
            Path.GetFileNameWithoutExtension(executable).Equals("Synups",StringComparison.OrdinalIgnoreCase));
    }
    public Task<LoginStatus> ReadAsync()=>Task.Run(()=>Read());
    private LoginStatus Read()
    {
        if(!packagedExecutable || plan.Platform==LoginPlatform.Unsupported)
            return new(false,false,"Login registration requires the extracted self-contained Synups executable on a supported desktop. No startup settings were changed.");
        string? existing;
        if(plan.Platform==LoginPlatform.Windows)
        {
            if(!OperatingSystem.IsWindows())return new(false,false,"Windows startup registration is unavailable on this OS.");
            existing=ReadWindows(plan.Name);
        }
        else
        {
            if(plan.FileName is not { } file)throw new IOException("Missing registration filename.");
            if(File.Exists(file) && (new FileInfo(file).LinkTarget is not null || new FileInfo(file).Length>64*1024))
                return new(false,false,"Startup entry is a link or too large; preserved unchanged. Inspect it manually.");
            existing=File.Exists(file)?File.ReadAllText(file):null;
        }
        if(existing is null)return new(false,true,"Not registered. Enable explicitly to launch this workspace minimized at the next graphical login. Monitoring remains governed by its separate preference.");
        return existing==plan.Payload?new(true,true,"This exact workspace/executable is registered for next graphical login. OS/session policies can prevent execution; an actual login run is not certified."):
            new(false,false,"A different startup entry exists at this workspace's registration name. Preserved unchanged; inspect/remove the old entry manually before registering the moved executable.");
    }
    public Task<LoginStatus> SetAsync(bool enabled,bool confirmed)=>Task.Run(()=>
    {
        if(!confirmed)throw new InvalidOperationException("Explicit confirmation is required before changing OS startup registration.");
        var prior=Read();if(!prior.CanChange)throw new InvalidOperationException(prior.Detail);
        if(prior.Registered==enabled)return prior;
        if(plan.Platform==LoginPlatform.Windows)
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
            WriteWindows(plan.Name,enabled?plan.Payload:null);
        }
        else
        {
            var file=plan.FileName!;
            if(enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
                try { File.WriteAllText(temporary,plan.Payload,new UTF8Encoding(false));File.Move(temporary,file,false); }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            else File.Delete(file); // Read verified the exact owned payload; never remove an unrelated entry.
        }
        return Read();
    });
    [SupportedOSPlatform("windows")]
    private static string? ReadWindows(string name)
    {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return key?.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();}
    [SupportedOSPlatform("windows")]
    private static void WriteWindows(string name,string? payload)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        if(payload is null)key.DeleteValue(name,false);else key.SetValue(name,payload,RegistryValueKind.String);
    }
}
