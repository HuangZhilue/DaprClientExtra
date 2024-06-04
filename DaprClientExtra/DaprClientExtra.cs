using System.Diagnostics;
using System.Reflection;
using System.Text;
using Dapr.Client;
using Newtonsoft.Json;

namespace DaprClientExtra;

public static class DaprClientExtra
{
    /// <summary>
    /// 根据<paramref name="key"/>获取保存的状态列表
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="statename">状态存储组件名称</param>
    /// <param name="key">主键名</param>
    /// <returns>
    /// [ <typeparamref name="T"/>1, <typeparamref name="T"/>2, <typeparamref name="T"/>3... ]
    /// </returns>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法")]
    public static async Task<List<T>> GetStateListByKeyAsync<T>(
        this DaprClient daprClient,
        string statename,
        string key
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(statename))
            throw new ArgumentNullException(nameof(statename));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        List<string> keyList = await daprClient
            .GetStateAsync<List<string>>(statename, key)
            .ConfigureAwait(false);
        List<T>? result = default!;
        if (keyList?.Count > 0)
        {
            keyList = keyList.Select(k => $"{key}_{k}").ToList();
            IReadOnlyList<BulkStateItem> l = await daprClient
                .GetBulkStateAsync(statename, keyList, null)
                .ConfigureAwait(false);
            result = l.Select(s => JsonConvert.DeserializeObject<T>(s.Value) ?? default!).ToList(); //as T;
        }

        return result ?? [];
    }

    /// <summary>
    /// 根据<paramref name="key"/> + <paramref name="keyField"/>保存的状态列表
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="statename">状态存储组件名称</param>
    /// <param name="key">主键名</param>
    /// <param name="list">数据列表</param>
    /// <param name="keyField">数据中用来做主键的字段</param>
    /// <remarks>
    /// 以Redis为例，在Redis中，数据存储方式如下
    /// <br/>
    /// [1] keyPrefix || <paramref name="key"/> = [ <paramref name="keyField"/>1.value, <paramref name="keyField"/>2.value... ]
    /// <br/>
    /// [2] keyPrefix || <paramref name="key"/>_<paramref name="keyField"/>1.value = Type of <typeparamref name="T"/> { <paramref name="keyField"/>1: value, otherField: value }
    /// <br/>
    /// [3] keyPrefix || <paramref name="key"/>_<paramref name="keyField"/>2.value = Type of <typeparamref name="T"/> { <paramref name="keyField"/>2: value, otherField: value }
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法")]
    public static async Task SaveStateListByKeyAsync<T>(
        this DaprClient daprClient,
        string statename,
        string key,
        List<T> list,
        string keyField,
        int expiryInSeconds = -1
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(statename))
            throw new ArgumentNullException(nameof(statename));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        ArgumentNullException.ThrowIfNull(list);
        if (string.IsNullOrWhiteSpace(keyField))
            throw new ArgumentNullException(nameof(keyField));

        List<string> keyList =
            await daprClient.GetStateAsync<List<string>>(statename, key).ConfigureAwait(false)
            ?? [];

        List<StateTransactionRequest> stateList = [];
        bool needUpdateKeyList = false;

        for (int i = 0; i < list?.Count; i++)
        {
            T data = list[i];
            string keyString = GetPropValue(data, keyField)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(keyString))
                continue;

            if (!keyList.Any(k => k == keyString))
            {
                needUpdateKeyList = true;
                keyList.Add(keyString);
            }

            stateList.Add(
                new StateTransactionRequest(
                    $"{key}_{keyString}",
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)),
                    StateOperationType.Upsert,
                    metadata: new Dictionary<string, string>()
                    {
                        { "ttlInSeconds", expiryInSeconds.ToString() }
                    }
                )
            );
        }

        if (needUpdateKeyList)
        {
            keyList = keyList.Distinct().ToList();
            stateList.Add(
                new StateTransactionRequest(
                    key,
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(keyList)),
                    StateOperationType.Upsert
                )
            );
        }

        if (stateList.Count != 0)
        {
            await daprClient
                .ExecuteStateTransactionAsync(statename, stateList)
                .ConfigureAwait(false);
        }

        return;
    }

    /// <summary>
    /// 根据<paramref name="list"/>列表组合出的<paramref name="key"/> + <paramref name="keyField"/>来删除保存的状态
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="statename">状态存储组件名称</param>
    /// <param name="key">主键名</param>
    /// <param name="list">要删除的数据列表</param>
    /// <param name="keyField">数据中用来做主键的字段</param>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法")]
    public static async Task DeleteStateListByKeyAsync<T>(
        this DaprClient daprClient,
        string statename,
        string key,
        List<T> list,
        string keyField
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(statename))
            throw new ArgumentNullException(nameof(statename));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        ArgumentNullException.ThrowIfNull(list);
        if (string.IsNullOrWhiteSpace(keyField))
            throw new ArgumentNullException(nameof(keyField));

        if (list is null || list.Count == 0)
            return;
        List<string> keyList =
            await daprClient.GetStateAsync<List<string>>(statename, key).ConfigureAwait(false)
            ?? [];

        for (int i = 0; i < list?.Count; i++)
        {
            T data = list[i];
            string keyString = GetPropValue(data, keyField)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(keyString))
                continue;

            keyList.RemoveAll(k => k == keyString);

            _ = daprClient.DeleteStateAsync(statename, $"{key}_{keyString}");
        }

        keyList = keyList.Distinct().ToList();

        if (keyList.Count != 0)
        {
            await daprClient.SaveStateAsync(statename, key, keyList).ConfigureAwait(false);
        }
        else
        {
            await daprClient.DeleteStateAsync(statename, key).ConfigureAwait(false);
        }

        return;
    }

    /// <summary>
    /// 根据<paramref name="key"/>删除所有保存的状态
    /// </summary>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="statename">状态存储组件名称</param>
    /// <param name="key">主键名</param>
    /// <remarks>
    /// 以Redis为例，在Redis中，方法操作步骤如下
    /// <br/>
    /// 通过<paramref name="key"/>获取保存了所有 keyField.value 的状态列表
    /// <br/>
    /// keyPrefix || <paramref name="key"/> = [ keyField1.value, keyField2.value... ]
    /// <br/>
    /// 删除 keyPrefix || <paramref name="key"/>_keyField1.value 的数据
    /// <br/>
    /// 删除 keyPrefix || <paramref name="key"/>_keyField2.value 的数据
    /// <br/>
    /// 删除 keyPrefix || <paramref name="key"/>
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法")]
    public static async Task DeleteStateListAsync(
        this DaprClient daprClient,
        string statename,
        string key
    )
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(statename))
            throw new ArgumentNullException(nameof(statename));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        List<string> keyList =
            await daprClient.GetStateAsync<List<string>>(statename, key).ConfigureAwait(false)
            ?? [];

        for (int i = 0; i < keyList?.Count; i++)
        {
            string keyString = keyList[i];
            _ = daprClient.DeleteStateAsync(statename, $"{key}_{keyString}");
        }

        await daprClient.DeleteStateAsync(statename, key).ConfigureAwait(false);

        return;
    }

    private static object? GetPropValue(object src, string propName)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (string.IsNullOrWhiteSpace(propName))
            throw new ArgumentNullException(nameof(propName));

        return src.GetType()?.GetProperty(propName)?.GetValue(src, null);
    }

    /// <summary>
    /// 使用 State状态存储的简单锁（要求使用State状态存储配置，而不是Lock锁配置）
    /// </summary>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="storeName">必须是 状态存储组件名称</param>
    /// <param name="resourceId">资源ID</param>
    /// <param name="lockOwner">锁的拥有者</param>
    /// <param name="expiryInSeconds">锁的过期时间</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>上锁的响应情况</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法，还请尽量使用DaprClient.Lock/DaprClient.Unlock方法")]
    public static async Task<SimpleTryLockResponse> SimpleLock(
        this DaprClient daprClient,
        string storeName,
        string resourceId,
        string lockOwner,
        int expiryInSeconds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentNullException(nameof(storeName));
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentNullException(nameof(resourceId));
        if (string.IsNullOrWhiteSpace(lockOwner))
            throw new ArgumentNullException(nameof(lockOwner));
        if (expiryInSeconds <= 0)
            throw new ArgumentNullException(
                "The value cannot be zero or less than zero: " + expiryInSeconds
            );

        string key =
            $"{nameof(SimpleLock)}|{Assembly.GetEntryAssembly()?.GetName()?.Name ?? "DAPR"}|{resourceId}";

        string lockValue = await daprClient
            .GetStateAsync<string>(storeName, key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(lockValue) && lockValue != lockOwner)
            return new()
            {
                LockOwner = lockValue,
                ResourceId = resourceId,
                StoreName = storeName,
                Success = false
            };

        await daprClient
            .SaveStateAsync(
                storeName,
                key,
                lockOwner,
                metadata: new Dictionary<string, string>()
                {
                    { /*"metadata.ttlInSeconds"*/
                        "ttlInSeconds",
                        expiryInSeconds.ToString()
                    }
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        lockValue = await daprClient
            .GetStateAsync<string>(storeName, key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new()
        {
            StoreName = storeName,
            ResourceId = resourceId,
            LockOwner = lockValue,
            Success = lockValue is not null && lockValue == lockOwner
        };
    }

    /// <summary>
    /// 使用 State状态存储的简单锁（要求使用State状态存储配置，而不是Lock锁配置）
    /// </summary>
    /// <param name="daprClient">DaprClient</param>
    /// <param name="storeName">必须是 状态存储组件名称</param>
    /// <param name="resourceId">资源ID</param>
    /// <param name="lockOwner">锁的拥有者</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>解锁的响应情况</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete("稳定性存疑，请尽量不要在生产环境使用此方法，还请尽量使用DaprClient.Lock/DaprClient.Unlock方法")]
    public static async Task<UnlockResponse> SimpleUnlock(
        this DaprClient daprClient,
        string storeName,
        string resourceId,
        string lockOwner,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentNullException(nameof(storeName));
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentNullException(nameof(resourceId));
        if (string.IsNullOrWhiteSpace(lockOwner))
            throw new ArgumentNullException(nameof(lockOwner));

        string key =
            $"{nameof(SimpleLock)}|{Assembly.GetEntryAssembly()?.GetName()?.Name ?? "DAPR"}|{resourceId}";

        try
        {
            string lockValue = await daprClient
                .GetStateAsync<string>(storeName, key, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(lockValue))
                return new UnlockResponse(LockStatus.LockDoesNotExist);
            if (!string.IsNullOrWhiteSpace(lockValue) && lockOwner != lockValue)
                return new UnlockResponse(LockStatus.LockBelongsToOthers);
            if (!string.IsNullOrWhiteSpace(lockValue) && lockOwner == lockValue)
            {
                await daprClient
                    .DeleteStateAsync(storeName, key, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new(LockStatus.Success);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return new(LockStatus.InternalError);
    }
}

[Obsolete("稳定性存疑，请尽量不要在生产环境使用此类")]
public sealed class SimpleTryLockResponse : IAsyncDisposable
{
    /// <summary>
    /// The success value of the tryLock API call
    /// </summary>
    public bool Success { get; init; } = false;

    /// <summary>
    /// The resourceId required to unlock the lock
    /// </summary>
    public string ResourceId { get; init; } = null!;

    /// <summary>
    /// The LockOwner required to unlock the lock
    /// </summary>
    public string LockOwner { get; init; } = null!;

    /// <summary>
    /// The StoreName required to unlock the lock
    /// </summary>
    public string StoreName { get; init; } = null!;

    /// <summary>
    /// Constructor for a TryLockResponse.
    /// </summary>
    public SimpleTryLockResponse() { }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        using DaprClient client = new DaprClientBuilder().Build();
        if (Success)
        {
            await client.SimpleUnlock(StoreName, ResourceId, LockOwner);
        }
    }
}
