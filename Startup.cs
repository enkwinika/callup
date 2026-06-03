using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(CallUp.Startup))]

namespace CallUp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure SignalR
            app.MapSignalR();
            
            // Other configuration (Identity, etc.)
            // ConfigureAuth(app);
        }
    }
}
