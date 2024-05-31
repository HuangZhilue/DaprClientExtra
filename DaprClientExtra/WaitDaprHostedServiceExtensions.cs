using Man.Dapr.Sidekick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaprClientExtra;

public static class WaitDaprHostedServiceExtensions
{
    /// <summary>
    /// 将 WaitDaprHostedService 添加到 IServiceCollection 中，并将其配置为托管服务运行。
    /// </summary>
    /// <param name="services">要添加服务的 IServiceCollection。</param>
    /// <param name="otherCode">
    /// 在 Dapr 服务准备就绪后要执行的操作。
    /// <br/>
    /// 例如：
    /// <br/>
    /// 使用Dapr密钥存储来配置数据库链接、
    /// <br/>
    /// 使用Dapr状态存储来缓存程序必须的基础数据、
    /// <br/>
    /// 运行一些需要在Dapr服务准备就绪后执行的后台任务。
    /// </param>
    /// <param name="configureOptions">
    /// 配置 WaitDaprHostedService 的选项。
    /// <br/>
    /// 默认值：
    /// <br/>
    /// <code>
    /// <see cref="WaitDaprHostedOptions.WaitForAppStartupTimeout"/> = <see cref="TimeSpan"/>.FromSeconds(30)
    /// <br/>
    /// <see cref="WaitDaprHostedOptions.WaitForDaprStartupDelayInterval"/> = <see cref="TimeSpan"/>.FromMilliseconds(100)
    /// </code>
    /// </param>
    /// <returns>修改后的 IServiceCollection。</returns>
    public static IServiceCollection AddWaitDaprHostedService(
        this IServiceCollection services,
        Action<WaitDaprHostedOptions>? configureOptions = null,
        Action<IServiceProvider, CancellationToken>? otherCode = null
    )
    {
        WaitDaprHostedOptions options = new();
        configureOptions?.Invoke(options);
        services.AddHostedService(provider =>
        {
            IHostApplicationLifetime lifetime =
                provider.GetRequiredService<IHostApplicationLifetime>();
            return new WaitDaprHostedService(provider, lifetime, options, otherCode);
        });
        return services;
    }

    public class WaitDaprHostedOptions
    {
        /// <summary>
        /// 等待应用程序启动的超时时间
        /// </summary>
        /// <remarks>默认值：30 秒</remarks>
        public TimeSpan WaitForAppStartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
        /// <summary>
        /// 等待 Dapr 启动时的轮询延迟
        /// </summary>
        /// <remarks>默认值：100 毫秒</remarks>
        public TimeSpan WaitForDaprStartupDelayInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    }

    private class WaitDaprHostedService(
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        WaitDaprHostedOptions options,
        Action<IServiceProvider, CancellationToken>? otherCode
    ) : BackgroundService
    {
        private ILogger<WaitDaprHostedService> Logger { get; } =
            services.GetRequiredService<ILogger<WaitDaprHostedService>>();
        private IDaprSidecarHost DaprSidecarHost { get; } =
            services.GetRequiredService<IDaprSidecarHost>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!await WaitForAppStartup(lifetime, options.WaitForAppStartupTimeout, stoppingToken))
            {
                return;
            }

            while (
                DaprSidecarHost is not null
                && !DaprSidecarHost.GetProcessInfo().IsRunning
                && !stoppingToken.IsCancellationRequested
            )
            {
                Logger.LogTrace("Wait 4 Dapr Starting ...");
                await Task.Delay(options.WaitForDaprStartupDelayInterval, stoppingToken)
                    .ConfigureAwait(false);
            }

            otherCode?.Invoke(services, stoppingToken);
            Logger.LogTrace("WaitDaprHostedService Finished");
        }

        private static async Task<bool> WaitForAppStartup(
            IHostApplicationLifetime lifetime,
            TimeSpan timeout,
            CancellationToken stoppingToken
        )
        {
            TaskCompletionSource<object?> tcs = new();
            using CancellationTokenRegistration ctr1 = lifetime.ApplicationStarted.Register(
                () => tcs.TrySetResult(null)
            );
            using CancellationTokenRegistration ctr2 = stoppingToken.Register(
                () => tcs.TrySetCanceled()
            );

            using Task completedTask = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(timeout, stoppingToken)
                )
                .ConfigureAwait(false);
            return completedTask == tcs.Task;
        }
    }
}
