using Synups.Desktop.Integrations;

namespace Synups.Desktop;

public sealed partial class MainWindowViewModel
{
    private string loginStatus="Login registration is read only until you explicitly choose Enable/Remove.";
    private LoginRegistration? login;
    private bool loginCanEnable,loginCanRemove;
    public string LoginStatus {get=>loginStatus;private set=>Set(ref loginStatus,value);}
    public bool LoginCanEnable {get=>loginCanEnable;private set=>Set(ref loginCanEnable,value);}
    public bool LoginCanRemove {get=>loginCanRemove;private set=>Set(ref loginCanRemove,value);}
    public async Task RefreshLoginAsync()
    {
        try
        {
            login??=LoginRegistration.ForCurrentProcess(Path.GetDirectoryName(ConfigurationPath)!);
            var result=await login.ReadAsync();LoginStatus=result.Detail;LoginCanEnable=result.CanChange&&!result.Registered;LoginCanRemove=result.CanChange&&result.Registered;
        }
        catch(Exception ex){LoginStatus=$"Startup registration unavailable; no settings reset: {ex.Message}";LoginCanEnable=LoginCanRemove=false;}
    }
    public Task ChangeLoginAsync(bool enabled,bool confirmed)=>ExecuteAsync(async()=>
    {
        if(IsRunning || disposed)throw new InvalidOperationException("Stop monitoring before changing startup registration.");
        login??=LoginRegistration.ForCurrentProcess(Path.GetDirectoryName(ConfigurationPath)!);
        try{var result=await login.SetAsync(enabled,confirmed);Message=result.Detail;}
        finally{await RefreshLoginAsync();}
    });
}
