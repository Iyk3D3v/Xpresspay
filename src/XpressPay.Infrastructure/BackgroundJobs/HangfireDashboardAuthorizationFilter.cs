using Hangfire.Dashboard;

namespace XpressPay.Infrastructure.BackgroundJobs;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var ip = httpContext.Connection.RemoteIpAddress;
        if (ip == null || System.Net.IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
    }
}
