﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wirehome.Core.Constants;
using Wirehome.Core.Contracts;
using Wirehome.Core.Diagnostics;
using Wirehome.Core.MessageBus;
using Wirehome.Core.Notifications;

namespace Wirehome.Core.System;

public sealed class SystemService : WirehomeCoreService
{
    readonly DateTime _creationTimestamp;

    readonly ILogger _logger;
    readonly MessageBusService _messageBusService;
    readonly NotificationsService _notificationsService;
    readonly SystemCancellationToken _systemCancellationToken;
    readonly SystemLaunchArguments _systemLaunchArguments;
    readonly SystemStatusService _systemStatusService;

    public SystemService(SystemStatusService systemStatusService,
        SystemLaunchArguments systemLaunchArguments,
        SystemCancellationToken systemCancellationToken,
        NotificationsService notificationsService,
        MessageBusService messageBusService,
        ILogger<SystemService> logger)
    {
        _systemStatusService = systemStatusService ?? throw new ArgumentNullException(nameof(systemStatusService));
        _systemLaunchArguments = systemLaunchArguments ?? throw new ArgumentNullException(nameof(systemLaunchArguments));
        _systemCancellationToken = systemCancellationToken ?? throw new ArgumentNullException(nameof(systemCancellationToken));
        _notificationsService = notificationsService ?? throw new ArgumentNullException(nameof(notificationsService));
        _messageBusService = messageBusService ?? throw new ArgumentNullException(nameof(messageBusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _creationTimestamp = DateTime.Now;
    }

    public event EventHandler ConfigurationLoaded;

    public event EventHandler ServicesInitialized;

    public event EventHandler StartupCompleted;

    public void OnConfigurationLoaded()
    {
        ConfigurationLoaded?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("Configuration loaded.");
    }

    public void OnServicesInitialized()
    {
        ServicesInitialized?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("Service startup completed.");
    }

    public void OnStartupCompleted()
    {
        _systemStatusService.Set("startup.duration", DateTime.Now - _creationTimestamp);

        PublishBootedNotification();

        StartupCompleted?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("Startup completed.");
    }

    public void Reboot(int waitTime)
    {
        _logger.LogInformation("Reboot initiated.");

        _notificationsService.PublishFromResource(new PublishFromResourceParameters
        {
            Type = NotificationType.Warning,
            ResourceUid = NotificationResourceUids.RebootInitiated,
            Parameters = new Dictionary<object, object>
            {
                ["wait_time"] = 0 // TODO: Add to event args.
            }
        });

        _messageBusService.Publish(new Dictionary<object, object>
        {
            ["type"] = "system.reboot_initiated"
        });

        Task.Run(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(waitTime));
            Process.Start("shutdown", " -r now");
        }, CancellationToken.None);
    }

    public void RunGarbageCollector()
    {
        _logger.LogInformation("Performing garbage collection.");

        GC.Collect(3, GCCollectionMode.Forced, true, true);
    }

    protected override void OnStart()
    {
        _systemStatusService.Set("startup.timestamp", _creationTimestamp);
        _systemStatusService.Set("startup.duration", null);

        _systemStatusService.Set("framework.description", RuntimeInformation.FrameworkDescription);

        _systemStatusService.Set("process.architecture", RuntimeInformation.ProcessArchitecture);
        _systemStatusService.Set("process.id", Process.GetCurrentProcess().Id);

        _systemStatusService.Set("system.date_time", () => DateTime.Now);
        _systemStatusService.Set("system.processor_count", Environment.ProcessorCount);

        _systemStatusService.Set("up_time", () => DateTime.Now - _creationTimestamp);

        _systemStatusService.Set("arguments", string.Join(" ", _systemLaunchArguments.Values));

        _systemStatusService.Set("wirehome.core.version", WirehomeCoreVersion.Version);

        _systemStatusService.RegisterProvider(target =>
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                target.Add("process.pid", currentProcess.Id);
                target.Add("process.threads_count", currentProcess.Threads.Count);

                ////foreach (ProcessThread thread in currentProcess.Threads)
                ////{
                ////    using (thread)
                ////    {
                ////        target.Add($"process.threads.{thread.Id}.priority_level", thread.PriorityLevel);
                ////        target.Add($"process.threads.{thread.Id}.total_processor_time", thread.TotalProcessorTime);
                ////    }
                ////}

                target.Add("process.working_set", currentProcess.WorkingSet64);
                target.Add("process.max_working_set", currentProcess.MaxWorkingSet.ToInt64());
                target.Add("process.peak_working_set", currentProcess.PeakWorkingSet64);

                target.Add("process.private_memory", currentProcess.PrivateMemorySize64);

                target.Add("process.virtual_memory", currentProcess.VirtualMemorySize64);
                target.Add("process.peak_virtual_memory", currentProcess.PeakVirtualMemorySize64);
            }
        });

        AddOSInformation();
        AddThreadPoolInformation();
    }

    void AddOSInformation()
    {
        _systemStatusService.Set("os.description", RuntimeInformation.OSDescription);
        _systemStatusService.Set("os.architecture", RuntimeInformation.OSArchitecture);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _systemStatusService.Set("os.platform", "linux");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _systemStatusService.Set("os.platform", "windows");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _systemStatusService.Set("os.platform", "osx");
        }
    }

    void AddThreadPoolInformation()
    {
        _systemStatusService.Set("thread_pool.max_worker_threads", () =>
        {
            ThreadPool.GetMaxThreads(out var x, out _);
            return x;
        });

        _systemStatusService.Set("thread_pool.max_completion_port_threads", () =>
        {
            ThreadPool.GetMaxThreads(out _, out var x);
            return x;
        });

        _systemStatusService.Set("thread_pool.min_worker_threads", () =>
        {
            ThreadPool.GetMinThreads(out var x, out _);
            return x;
        });

        _systemStatusService.Set("thread_pool.min_completion_port_threads", () =>
        {
            ThreadPool.GetMinThreads(out _, out var x);
            return x;
        });

        _systemStatusService.Set("thread_pool.available_worker_threads", () =>
        {
            ThreadPool.GetAvailableThreads(out var x, out _);
            return x;
        });

        _systemStatusService.Set("thread_pool.available_completion_port_threads", () =>
        {
            ThreadPool.GetAvailableThreads(out _, out var x);
            return x;
        });
    }

    void PublishBootedNotification()
    {
        _messageBusService.Publish(new Dictionary<object, object>
        {
            ["type"] = "system.booted"
        });

        _notificationsService.PublishFromResource(new PublishFromResourceParameters
        {
            Type = NotificationType.Information,
            ResourceUid = NotificationResourceUids.Booted
        });
    }
}