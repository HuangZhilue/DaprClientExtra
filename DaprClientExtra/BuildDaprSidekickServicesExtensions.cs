using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DaprClientExtra;

public static class BuildDaprSidekickServicesExtensions
{
    /// <summary>
    /// 将 DaprSidekick 服务添加到服务集合中。
    /// </summary>
    /// <remarks>
    /// 使用配置文件 daprSidekick.json 和 daprSidekick.{ASPNETCORE_ENVIRONMENT}.json
    /// </remarks>
    /// <param name="services">要添加服务的服务集合。</param>
    public static void AddDaprSidekick(this IServiceCollection services)
    {
        try
        {
            IConfigurationRoot DaprSidekickConfiguration = new ConfigurationBuilder()
                .AddJsonFile("daprSidekick.json", true, true)
                .AddJsonFile(
                    $"daprSidekick.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
                    true,
                    true
                )
                .Build();

            // Add DaprSidekick services to the service collection using the configuration we just built.
            services.AddDaprSidekick(DaprSidekickConfiguration);
        }
        catch (Exception ex)
        {
            // If there's an exception while adding the services, throw a new exception with a descriptive message.
            throw new("DaprSidekick Configuration Exception", ex);
        }
    }
}
