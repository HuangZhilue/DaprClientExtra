# DaprClientExtra

> [!WARNING]  
> 不建议在生产环境中使用 <br/> It is not recommended to use it in a production environment
> 
针对[Dapr SDK for .NET](https://github.com/dapr/dotnet-sdk)和[Dapr Sidekick for .NET](https://github.com/man-group/dapr-sidekick-dotnet)的简单扩展
<br/>Simple extension for [Dapr SDK for .NET](https://github.com/dapr/dotnet-sdk) and [Dapr Sidekick for .NET](https://github.com/man-group/dapr-sidekick-dotnet)

## 使用示例 / Examples

> 完整使用示例可查看[DaprClientExtraTests](DaprClientExtraTests)（mini web api）项目的[Program.cs](DaprClientExtraTests/Program.cs)
> <br/>
> For a complete usage example, see [Program.cs](DaprClientExtraTests/Program.cs) in the [DaprClientExtraTests](DaprClientExtraTests) (mini web api) project.

- 添加Dapr相关的Nuget包 / Add Dapr related Nuget packages
    ``` XML
    <PackageReference Include="Dapr.Client" Version="1.13.1" />
    <PackageReference Include="Man.Dapr.Sidekick" Version="2.0.0-rc01" />
    <PackageReference Include="Man.Dapr.Sidekick.AspNetCore" Version="2.0.0-rc01" />
    ```
    - 也别忘记了添加 DaprClientExtra包 / Also don't forget to add the DaprClientExtra package

- 注册Dapr相关的服务 / Register for Dapr related services
    ``` C#
    
        // Add services to the container.

        // Add Dapr services to the container.
        builder.Services.AddControllers().AddDapr();
        // Add Dapr Sidekick services to the container.
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
                Console.WriteLine("Dapr相关服务已经完全启动，现在可以运行一些需要用到Dapr的任务了，比如调用Dapr密钥来注册数据库链接，调用Dapr状态存储来初始化基础数据等");
                Console.WriteLine("Dapr related services have been started. Now you can run some tasks that require Dapr, such as calling Dapr secret stores to register database connection, calling Dapr state store to initialize basic data");

                DaprClient daprClient = services.GetRequiredService<DaprClient>();
                // Do something with DaprClient
            });
    ```

- 额外的扩展方法 / Extra extension methods

    1. 使用状态存储的简易锁 / Simple lock using state storage
        ``` C#
        // Lock
        public static async Task<SimpleTryLockResponse> SimpleLock(
            this DaprClient daprClient,
            string storeName,
            string resourceId,
            string lockOwner,
            int expiryInSeconds,
            CancellationToken cancellationToken = default
        )

        // UnLock
        public static async Task<UnlockResponse> SimpleUnlock(
            this DaprClient daprClient,
            string storeName,
            string resourceId,
            string lockOwner,
            CancellationToken cancellationToken = default
        )
        ```

    2. 针对列表数据的状态存储 / State storage for list data
        > 具体的列表数据保存情况，可以自行使用相关工具查看，比如redis的可以使用rdm工具查看
        > <br/> For the list data storage situation, you can use the relevant tools to view it, such as redis, you can use the rdm tool to view it
        ``` C#
        // 保存列表数据 / Save list data
        public static async Task SaveStateListByKeyAsync<T>(this DaprClient daprClient, string statename, string key,  List<T> list, string keyField, int expiryInSeconds = -1)
            
        // 获取通过 SaveStateListByKeyAsync 方法保存的列表数据 / Get the list data that was saved by SaveStateListByKeyAsync
        public static async Task<List<T>> GetStateListByKeyAsync<T>(this DaprClient daprClient,  string statename, string key)

        // 删除状态存储中与list中的对象的keyField属性的值相同的数据 / Delete the data in the state store that has the same value as the keyField attribute of the object in the list
        public static async Task DeleteStateListByKeyAsync<T>(this DaprClient daprClient, string statename, string key, List<T> list, string keyField)

        // 删除通过 SaveStateListByKeyAsync 方法保存的相同Key值的所有列表数据 / Delete all list data with the same key value saved by the SaveStateListByKeyAsync method
        public static async Task DeleteStateListAsync(this DaprClient daprClient, string statename, string key)
        ```
    3. 使用特定配置文件（daprSidekick.json）来初始化Dapr Sidekick / Initialize Dapr Sidekick using a specific configuration file (daprSidekick.json)
        ``` C#
        // 使用配置文件 daprSidekick.json 和 daprSidekick.{ASPNETCORE_ENVIRONMENT}.json，将 DaprSidekick 服务添加到服务集合中。
        // Add the DaprSidekick service to the services collection using the configuration files daprSidekick.json and daprSidekick.{ASPNETCORE_ENVIRONMENT}.json.
        public static void AddDaprSidekick(this IServiceCollection services)
        ```

    4. Dapr启动后 / After Dapr starts
        ``` C#
        // 将 WaitDaprHostedService 添加到 IServiceCollection 中，并将其配置为托管服务运行。 / Add WaitDaprHostedService to the IServiceCollection and configure it to run as a hosted service.
        // 可以通过 configureOptions 参数自定义 WaitDaprHostedService 的选项。 / You can customize the options of WaitDaprHostedService through the configureOptions parameter.
        // 可以通过 otherCode 参数在 Dapr 服务准备就绪后执行其他操作。 / The otherCode parameter can be used to perform other operations after the Dapr service is ready.
        public static IServiceCollection AddWaitDaprHostedService(this IServiceCollection services, Action<WaitDaprHostedOptions>? configureOptions = null, Action<IServiceProvider, CancellationToken>? otherCode = null)
        ```
