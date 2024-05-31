using System.Reflection;
using Dapr.Client;
using DaprClientExtra;
using Google.Api;
using Man.Dapr.Sidekick;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DaprClientExtraTests;

[TestClass()]
public class ExtensionsTests
{
    [TestMethod()]
    public void AddWaitDaprHostedServiceTest()
    {
        // TODO 确保项目中包含的“daprSidekick.json”文件中的设置项被正确设置，例如“RuntimeDirectory”、“InitialDirectory”、“ComponentsDirectory”、“ResourcesDirectory”、“ConfigFile”等，被正确设置
        // TODO Make sure that the settings in the "daprSidekick.json" file are correctly set, such as "RuntimeDirectory", "InitialDirectory", "ComponentsDirectory", "ResourcesDirectory", "ConfigFile", etc.

        // TODO 确保项目中包含的“daprSidekick.json”文件属性被设置成“始终复制到输出目录”或者“较新时复制到输出目录”
        // TODO Make sure that the "daprSidekick.json" file property is set to "Always copy to output directory" or "Copy to output directory if newer"

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddDapr();
        builder.Services.AddDaprSidekick();
        builder.Services.AddWaitDaprHostedService(
            options =>
            {
                options.WaitForDaprStartupDelayInterval = TimeSpan.FromMilliseconds(100);
                options.WaitForAppStartupTimeout = TimeSpan.FromSeconds(30);
            },
            async (services, cancellationToken) =>
            {
                Console.WriteLine("Dapr Hosted Service Started...");
                Console.WriteLine("Start Action after 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                DaprClient daprClient = services.GetRequiredService<DaprClient>();
#pragma warning disable CS0618 // 类型或成员已过时
                // save something
                await daprClient
                    .SaveStateListByKeyAsync(
                        "statestore",
                        "ThisIsKey",
                        [
                            new { A = "1", B = "2" },
                            new { A = "3", B = "4" },
                            new { A = "5", B = "6" }
                        ],
                        "A"
                    )
                    .ConfigureAwait(false);

                // try to update the list
                await daprClient
                    .SaveStateListByKeyAsync(
                        "statestore",
                        "ThisIsKey",
                        [new { A = "7", B = "8" }],
                        "A"
                    )
                    .ConfigureAwait(false);

                // try to get something & print something
                await TryAndPrintListFromDaprStateStoreAsync(daprClient).ConfigureAwait(false);

                // delete something ( delete A="1" )
                await daprClient
                    .DeleteStateListByKeyAsync("statestore", "ThisIsKey", [new { A = "1" }], "A")
                    .ConfigureAwait(false);

                // try to get something & print something
                await TryAndPrintListFromDaprStateStoreAsync(daprClient).ConfigureAwait(false);



#pragma warning restore CS0618 // 类型或成员已过时
            }
        );

        WebApplication app = builder.Build();
        // 使用 8011 端口
        app.Urls.Add("http://*:8011");

        app.MapGet(
            "/",
            ([FromServices] IDaprSidecarHost? DaprSidecarHost) =>
            {
                return new
                {
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                    PID = Environment.ProcessId,
                    process = DaprSidecarHost?.GetProcessInfo(), // Information about the sidecar process such as if it is running
                    options = DaprSidecarHost?.GetProcessOptions() // The sidecar options if running, including ports and locations
                };
            }
        );

        app.Run();
    }

    private static async Task TryAndPrintListFromDaprStateStoreAsync(DaprClient daprClient, string statename = "statestore", string key = "ThisIsKey")
    {
#pragma warning disable CS0618 // 类型或成员已过时
        // try to get something
        List<IDictionary<string, string>> savedList = await daprClient
            .GetStateListByKeyAsync<IDictionary<string, string>>(statename,key)
            .ConfigureAwait(false);

        // print something
        savedList.ForEach(dic =>
            dic.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"))
        );
#pragma warning restore CS0618 // 类型或成员已过时
    }
}
