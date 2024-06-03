// TODO 确保项目中包含的“daprSidekick.json”文件中的设置项被正确设置，例如“RuntimeDirectory”、“InitialDirectory”、“ComponentsDirectory”、“ResourcesDirectory”、“ConfigFile”等，被正确设置
// TODO Make sure that the settings in the "daprSidekick.json" file are correctly set, such as "RuntimeDirectory", "InitialDirectory", "ComponentsDirectory", "ResourcesDirectory", "ConfigFile", etc.

// TODO 确保项目中包含的“daprSidekick.json”文件属性被设置成“始终复制到输出目录”或者“较新时复制到输出目录”
// TODO Make sure that the "daprSidekick.json" file property is set to "Always copy to output directory" or "Copy to output directory if newer"

// TODO 本测试项目使用的是名称为statestore的状态存储组件，确保Dapr组件目录中包含有最基础的statestore组件，例如statestore.yaml
// TODO This test project uses the statestore component with the name "statestore". Make sure that the Dapr component directory contains the basic statestore component, such as statestore.yaml

using System.Reflection;
using Dapr.Client;
using DaprClientExtra;
using Man.Dapr.Sidekick;
using Microsoft.AspNetCore.Mvc;

internal class Program
{
    const string StateStoreName = "statestore";

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

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
                Console.WriteLine(
                    "Dapr相关服务已经完全启动，现在可以运行一些需要用到Dapr的任务了，比如调用Dapr密钥来注册数据库链接，调用Dapr状态存储来初始化基础数据等"
                );
                Console.WriteLine(
                    "Dapr related services have been started. Now you can run some tasks that require Dapr, such as calling Dapr secret stores to register database connection, calling Dapr state store to initialize basic data"
                );

                DaprClient daprClient = services.GetRequiredService<DaprClient>();
#pragma warning disable CS0618 // 类型或成员已过时
                string resourceId = "DaprClientExtraTests";
                string lockOwner = "DaprClientExtraTests" + Environment.ProcessId;

                await using SimpleTryLockResponse mySimpleLock = await daprClient
                    .SimpleLock(StateStoreName, resourceId, lockOwner, 60, cancellationToken)
                    .ConfigureAwait(false);
                if (mySimpleLock is null || !mySimpleLock.Success)
                {
                    throw new Exception(
                        "Lock 4 " + resourceId + " Failed! Maybe it had lock & running..."
                    );
                }
                Console.WriteLine("({0}) Lock 4 {1} Success!", lockOwner, resourceId);

                // save something
                await daprClient
                    .SaveStateListByKeyAsync(
                        StateStoreName,
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
                        StateStoreName,
                        "ThisIsKey",
                        [new { A = "7", B = "8" }, new { A = "3", B = "4444444" }],
                        "A"
                    )
                    .ConfigureAwait(false);

                // try to get something & print something
                await TryGetAndPrintListFromDaprStateStoreAsync(daprClient).ConfigureAwait(false);

                // delete something ( delete A="1" )
                await daprClient
                    .DeleteStateListByKeyAsync(StateStoreName, "ThisIsKey", [new { A = "1" }], "A")
                    .ConfigureAwait(false);

                // try to get something & print something
                await TryGetAndPrintListFromDaprStateStoreAsync(daprClient).ConfigureAwait(false);

                UnlockResponse unLock = await daprClient
                    .SimpleUnlock(StateStoreName, resourceId, lockOwner, cancellationToken)
                    .ConfigureAwait(false);
                if (unLock is not null && unLock.status == LockStatus.Success)
                {
                    Console.WriteLine("({0}) UnLock 4 {1} Success!", lockOwner, resourceId);
                }

#pragma warning restore CS0618 // 类型或成员已过时
            }
        );

        var app = builder.Build();

        // 使用 8011 端口
        // override by appsettings.json Kestrel.Endpoints.Http.Url
        //app.Urls.Add("http://*:8011");

        app.MapGet(
            "/",
            async (
                [FromServices] IDaprSidecarHost? DaprSidecarHost,
                [FromServices] DaprClient daprClient
            ) =>
            {
                object? result = null;
                try
                {
                    List<IDictionary<string, string>> list =
                        await TryGetAndPrintListFromDaprStateStoreAsync(daprClient)
                            .ConfigureAwait(false);

                    result = list;
                }
                catch (Exception ex)
                {
                    result = ex;
                }

                return new
                {
                    Result = result,
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                    PID = Environment.ProcessId,
                    process = DaprSidecarHost?.GetProcessInfo(), // Information about the sidecar process such as if it is running
                    options = DaprSidecarHost?.GetProcessOptions() // The sidecar options if running, including ports and locations
                };
            }
        );

        app.MapGet(
            "/delete",
            async ([FromServices] DaprClient daprClient) =>
            {
#pragma warning disable CS0618 // 类型或成员已过时
                await daprClient
                    .DeleteStateListAsync(StateStoreName, "ThisIsKey")
                    .ConfigureAwait(false);

                List<IDictionary<string, string>> list =
                    await TryGetAndPrintListFromDaprStateStoreAsync(daprClient)
                        .ConfigureAwait(false);
#pragma warning restore CS0618 // 类型或成员已过时
                return new { Result = list };
            }
        );

        app.Run();
    }

    static async Task<List<IDictionary<string, string>>> TryGetAndPrintListFromDaprStateStoreAsync(
        DaprClient daprClient,
        string statename = StateStoreName,
        string key = "ThisIsKey"
    )
    {
#pragma warning disable CS0618 // 类型或成员已过时
        // try to get something
        List<IDictionary<string, string>> savedList = await daprClient
            .GetStateListByKeyAsync<IDictionary<string, string>>(statename, key)
            .ConfigureAwait(false);

        // print something
        savedList.ForEach(dic =>
        {
            Console.WriteLine();
            dic.ToList().ForEach(x => Console.Write($"{x.Key} = {x.Value}\t"));
            Console.WriteLine();
        });
#pragma warning restore CS0618 // 类型或成员已过时
        return savedList;
    }
}
