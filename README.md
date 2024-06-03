# DaprClientExtra

> [!WARNING]  
> ������������������ʹ�� <br/> It is not recommended to use it in a production environment

����ʹ�õ����[Dapr SDK for .NET](https://github.com/dapr/dotnet-sdk)��[Dapr Sidekick for .NET](https://github.com/man-group/dapr-sidekick-dotnet)�ļ���չ

## ʹ��ʾ�� / Examples

> ����ʹ��ʾ���ɲ鿴[DaprClientExtraTests](DaprClientExtraTests)��mini web api����Ŀ��[Program.cs](DaprClientExtraTests/Program.cs)
> <br/>
> For a complete usage example, see [Program.cs](DaprClientExtraTests/Program.cs) in the [DaprClientExtraTests](DaprClientExtraTests) (mini web api) project.

- ���Dapr��ص�Nuget�� / Add Dapr related Nuget packages
    ``` XML
    <PackageReference Include="Dapr.Client" Version="1.13.1" />
    <PackageReference Include="Man.Dapr.Sidekick" Version="2.0.0-rc01" />
    <PackageReference Include="Man.Dapr.Sidekick.AspNetCore" Version="2.0.0-rc01" />
    ```
    - Ҳ����������� DaprClientExtra�� / Also don't forget to add the DaprClientExtra package

- ע��Dapr��صķ��� / Register for Dapr related services
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
                Console.WriteLine("Dapr��ط����Ѿ���ȫ���������ڿ�������һЩ��Ҫ�õ�Dapr�������ˣ��������Dapr��Կ��ע�����ݿ����ӣ�����Dapr״̬�洢����ʼ���������ݵ�");
                Console.WriteLine("Dapr related services have been started. Now you can run some tasks that require Dapr, such as calling Dapr secret stores to register database connection, calling Dapr state store to initialize basic data");

                DaprClient daprClient = services.GetRequiredService<DaprClient>();
                // Do something with DaprClient
            });
    ```

- �������չ���� / Extra extension methods

    > [!WARNING]  
    > ������������������ʹ�� <br/> It is not recommended to use it in a production environment

    1. ʹ��״̬�洢�ļ����� / Simple lock using state storage
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

    2. ����б����ݵ�״̬�洢 / State storage for list data
        > ������б����ݱ����������������ʹ����ع��߲鿴������redis�Ŀ���ʹ��rdm���߲鿴
        > <br/> For the list data storage situation, you can use the relevant tools to view it, such as redis, you can use the rdm tool to view it
        ``` C#
        // �����б����� / Save list data
        public static async Task SaveStateListByKeyAsync<T>(this DaprClient daprClient, string statename, string key,  List<T> list, string keyField)
            
        // ��ȡͨ�� SaveStateListByKeyAsync ����������б����� / Get the list data that was saved by SaveStateListByKeyAsync
        public static async Task<List<T>> GetStateListByKeyAsync<T>(this DaprClient daprClient,  string statename, string key)

        // ɾ��״̬�洢����list�еĶ����keyField���Ե�ֵ��ͬ������ / Delete the data in the state store that has the same value as the keyField attribute of the object in the list
        public static async Task DeleteStateListByKeyAsync<T>(this DaprClient daprClient, string statename, string key, List<T> list, string keyField)

        // ɾ��ͨ�� SaveStateListByKeyAsync �����������ͬKeyֵ�������б����� / Delete all list data with the same key value saved by the SaveStateListByKeyAsync method
        public static async Task DeleteStateListAsync(this DaprClient daprClient, string statename, string key)
        ```
    3. ʹ���ض������ļ���daprSidekick.json������ʼ��Dapr Sidekick / Initialize Dapr Sidekick using a specific configuration file (daprSidekick.json)
        ``` C#
        // ʹ�������ļ� daprSidekick.json �� daprSidekick.{ASPNETCORE_ENVIRONMENT}.json���� DaprSidekick ������ӵ����񼯺��С�
        // Add the DaprSidekick service to the services collection using the configuration files daprSidekick.json and daprSidekick.{ASPNETCORE_ENVIRONMENT}.json.
        public static void AddDaprSidekick(this IServiceCollection services)
        ```

    4. Dapr������ / After Dapr starts
        ``` C#
        // �� WaitDaprHostedService ��ӵ� IServiceCollection �У�����������Ϊ�йܷ������С� / Add WaitDaprHostedService to the IServiceCollection and configure it to run as a hosted service.
        // ����ͨ�� configureOptions �����Զ��� WaitDaprHostedService ��ѡ� / You can customize the options of WaitDaprHostedService through the configureOptions parameter.
        // ����ͨ�� otherCode ������ Dapr ����׼��������ִ������������ / The otherCode parameter can be used to perform other operations after the Dapr service is ready.
        public static IServiceCollection AddWaitDaprHostedService(this IServiceCollection services, Action<WaitDaprHostedOptions>? configureOptions = null, Action<IServiceProvider, CancellationToken>? otherCode = null)
        ```