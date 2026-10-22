using System.Xml.Linq;
using Synups.Desktop.Integrations;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Xunit;
using System.Diagnostics;
using System.Text.Json;

namespace Synups.Tests;

public class LoginRegistrationTests
{
    [Fact]
    public async Task NativeLinuxDesktopLauncherPreservesQuotedArgumentsWithoutInstallingStartupEntry()
    {
        if(!OperatingSystem.IsLinux())Assert.Skip("Native Gio desktop launcher verification requires Linux.");
        var root=Path.Combine(Path.GetTempPath(),"Synups-launch-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var output=Path.Combine(root,"argv.json");var executable=Path.Combine(root,"Synups space$quote\"percent%.py");
            var data=Path.Combine(root,"data `quote\" dollar$ backslash\\ percent% space");
            await File.WriteAllTextAsync(executable,"#!/usr/bin/python3\nimport json,sys\nfrom pathlib import Path\nPath("+JsonSerializer.Serialize(output)+").write_text(json.dumps(sys.argv[1:]))\n",TestContext.Current.CancellationToken);
            if(OperatingSystem.IsLinux())File.SetUnixFileMode(executable,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            var plan=LoginPlan.Create(LoginPlatform.Linux,executable,data,root,root);
            // Isolated fixture only: never write the real XDG autostart directory.
            Directory.CreateDirectory(Path.GetDirectoryName(plan.FileName!)!);await File.WriteAllTextAsync(plan.FileName!,plan.Payload,TestContext.Current.CancellationToken);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true};
            start.ArgumentList.Add("-c");start.ArgumentList.Add("import sys; from gi.repository import Gio; app=Gio.DesktopAppInfo.new_from_filename(sys.argv[1]); assert app is not None; assert app.launch([], None)");start.ArgumentList.Add(plan.FileName!);
            using var process=Process.Start(start)!;
            try
            {
                var errors=process.StandardError.ReadToEndAsync(timeout.Token);var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);await stdout;Assert.True(process.ExitCode==0,await errors);
                await TestConfig.UntilAsync(()=>File.Exists(output));
                Assert.Equal(new[]{"--minimized","--data-directory",data},JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(output,timeout.Token)));
            }
            finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync(TestContext.Current.CancellationToken);}}
        }
        finally{Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(LoginPlatform.MacOS)]
    [InlineData(LoginPlatform.Linux)]
    public async Task ExplicitRegistrationIsReversibleAndPreservesConflicts(LoginPlatform platform)
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-login-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var exe=Path.Combine(root,"app space & percent%.app","Synups");var data=Path.Combine(root,"data space");
            var plan=LoginPlan.Create(platform,exe,data,root,Path.Combine(root,"config"));var registration=new LoginRegistration(plan);
            Assert.False((await registration.ReadAsync()).Registered);Assert.False(File.Exists(plan.FileName));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>registration.SetAsync(true,false));Assert.False(File.Exists(plan.FileName));
            Assert.True((await registration.SetAsync(true,true)).Registered);Assert.Equal(plan.Payload,await File.ReadAllTextAsync(plan.FileName!,TestContext.Current.CancellationToken));
            Assert.True((await new LoginRegistration(plan).ReadAsync()).Registered);
            if(platform==LoginPlatform.MacOS)
            {
                var xml=XDocument.Parse(plan.Payload);Assert.Equal(new[]{exe,"--minimized","--data-directory",data},xml.Descendants("array").Elements("string").Select(s=>s.Value));
            }
            else{Assert.Contains("percent%%",plan.Payload);Assert.Contains("--minimized",plan.Payload);}
            Assert.False((await registration.SetAsync(false,true)).Registered);Assert.False(File.Exists(plan.FileName));
            await File.WriteAllTextAsync(plan.FileName!,"Unrelated entry",TestContext.Current.CancellationToken);
            Assert.False((await registration.ReadAsync()).CanChange);await Assert.ThrowsAsync<InvalidOperationException>(()=>registration.SetAsync(false,true));
            Assert.Equal("Unrelated entry",await File.ReadAllTextAsync(plan.FileName!,TestContext.Current.CancellationToken));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void PlansUsePerWorkspaceNamesAndBoundedQuotedArguments()
    {
        var root=Path.GetTempPath();var executable=Path.Combine(root,"Synups.exe");var data=Path.Combine(root,"workspace space");
        var a=LoginPlan.Create(LoginPlatform.Windows,executable,data,root,root);var b=LoginPlan.Create(LoginPlatform.Windows,executable,data+"other",root,root);
        Assert.NotEqual(a.Name,b.Name);Assert.Contains("\" --minimized --data-directory \"",a.Payload);
        Assert.Throws<ArgumentException>(()=>LoginPlan.Create(LoginPlatform.Linux,"relative",data,root,root));
        Assert.Throws<ArgumentException>(()=>LoginPlan.Create(LoginPlatform.Linux,executable+"\n",data,root,root));
    }
    [Fact]
    public async Task DatedLogsRemainStableUniqueUtcAndDefaultNamingCompatible()
    {
        var root=Path.Combine(Path.GetTempPath(),"Synups-log-name-"+Guid.NewGuid());var utc=new DateTimeOffset(2026,10,22,12,0,0,TimeSpan.FromHours(2));var id=Guid.NewGuid();
        var archive=new HistoryState([],[],[new SessionRecord(id,utc,utc.AddMinutes(1),utc,TimeSpan.FromMinutes(1),false,false)],0);
        try
        {
            await AutomaticLogExporter.WriteAsync(root,archive);Assert.True(File.Exists(Path.Combine(root,$"Synups-{id}.json")));
            await AutomaticLogExporter.WriteAsync(root,archive,true);var name=Path.Combine(root,$"Synups-20261022T100000Z-{id}.json");Assert.True(File.Exists(name));
            var before=await File.ReadAllBytesAsync(name,TestContext.Current.CancellationToken);await AutomaticLogExporter.WriteAsync(root,archive,true);
            Assert.Equal(before,await File.ReadAllBytesAsync(name,TestContext.Current.CancellationToken));Assert.Equal(4,Directory.GetFiles(root).Length);
        }
        finally{Directory.Delete(root,true);}
    }
}
