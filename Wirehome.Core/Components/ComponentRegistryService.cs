﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IronPython.Runtime;
using Microsoft.Extensions.Logging;
using Wirehome.Core.App;
using Wirehome.Core.Components.Adapters;
using Wirehome.Core.Components.Configuration;
using Wirehome.Core.Components.Exceptions;
using Wirehome.Core.Components.Logic;
using Wirehome.Core.Constants;
using Wirehome.Core.Contracts;
using Wirehome.Core.Diagnostics;
using Wirehome.Core.HTTP.Controllers;
using Wirehome.Core.MessageBus;
using Wirehome.Core.Packages;
using Wirehome.Core.Python;
using Wirehome.Core.Python.Models;
using Wirehome.Core.Storage;

namespace Wirehome.Core.Components;

public sealed class ComponentRegistryService : WirehomeCoreService
{
    const string ComponentsDirectory = "Components";

    readonly Dictionary<string, Component> _components = new();
    readonly ILogger _logger;

    readonly ComponentRegistryMessageBusAdapter _messageBusAdapter;
    readonly PackageManagerService _packageManagerService;

    readonly PythonScriptHostFactoryService _pythonScriptHostFactoryService;
    readonly ILogger<ScriptComponentAdapter> _scriptComponentAdapterLogger;
    readonly ILogger<ScriptComponentLogic> _scriptComponentLogicLogger;
    readonly StorageService _storageService;

    public ComponentRegistryService(StorageService storageService,
        SystemStatusService systemStatusService,
        MessageBusService messageBusService,
        AppService appService,
        PythonScriptHostFactoryService pythonScriptHostFactoryService,
        PackageManagerService packageManagerService,
        ILogger<ScriptComponentLogic> scriptComponentLogicLogger,
        ILogger<ScriptComponentAdapter> scriptComponentAdapterLogger,
        ILogger<ComponentRegistryService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _pythonScriptHostFactoryService = pythonScriptHostFactoryService ?? throw new ArgumentNullException(nameof(pythonScriptHostFactoryService));
        _packageManagerService = packageManagerService ?? throw new ArgumentNullException(nameof(packageManagerService));
        _scriptComponentLogicLogger = scriptComponentLogicLogger ?? throw new ArgumentNullException(nameof(scriptComponentLogicLogger));
        _scriptComponentAdapterLogger = scriptComponentAdapterLogger ?? throw new ArgumentNullException(nameof(scriptComponentAdapterLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (systemStatusService == null)
        {
            throw new ArgumentNullException(nameof(systemStatusService));
        }

        systemStatusService.Set("component_registry.count", () => _components.Count);

        if (messageBusService == null)
        {
            throw new ArgumentNullException(nameof(messageBusService));
        }

        _messageBusAdapter = new ComponentRegistryMessageBusAdapter(messageBusService);

        if (appService is null)
        {
            throw new ArgumentNullException(nameof(appService));
        }

        appService.RegisterStatusProvider("components", () => GetComponents().Select(ComponentsController.CreateComponentModel));
    }

    public event EventHandler<ComponentStatusChangedEventArgs> ComponentStatusChanged;

    public bool ComponentHasSetting(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        return component.TryGetSetting(uid, out _);
    }

    public bool ComponentHasStatusValue(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        return component.TryGetStatusValue(uid, out _);
    }

    public bool ComponentHasTag(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        return GetComponent(componentUid).HasTag(uid);
    }

    public void DeleteComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        _storageService.DeletePath(ComponentsDirectory, uid);
    }

    public IDictionary<object, object> DisableComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var result = ProcessComponentMessage(uid, new Dictionary<object, object>
        {
            ["type"] = WirehomeMessageType.Disable
        });

        _messageBusAdapter.PublishDisabledEvent(uid);

        return result;
    }

    public IDictionary<object, object> EnableComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var result = ProcessComponentMessage(uid, new Dictionary<object, object>
        {
            ["type"] = WirehomeMessageType.Enable
        });

        _messageBusAdapter.PublishEnabledEvent(uid);

        return result;
    }

    public Component GetComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        lock (_components)
        {
            if (!_components.TryGetValue(uid, out var component))
            {
                throw new ComponentNotFoundException(uid);
            }

            return component;
        }
    }

    public object GetComponentConfigurationValue(string componentUid, string uid, object defaultValue = null)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (component.TryGetConfigurationValue(uid, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public List<Component> GetComponents()
    {
        lock (_components)
        {
            return new List<Component>(_components.Values);
        }
    }

    public object GetComponentSetting(string componentUid, string uid, object defaultValue = null)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (component.TryGetSetting(uid, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public object GetComponentStatusValue(string componentUid, string uid, object defaultValue = null)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (component.TryGetStatusValue(uid, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public List<Component> GetComponentsWithTag(string tag)
    {
        if (tag == null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        lock (_components)
        {
            return _components.Values.Where(c => c.HasTag(tag)).ToList();
        }
    }

    public List<string> GetComponentUids()
    {
        return _storageService.EnumerateDirectories("*", ComponentsDirectory);
    }

    public void InitializeComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        try
        {
            if (!_storageService.TryReadSerializedValue(out ComponentConfiguration configuration, ComponentsDirectory, uid, DefaultFileNames.Configuration))
            {
                throw new ComponentNotFoundException(uid);
            }

            if (!configuration.IsEnabled)
            {
                _logger.LogWarning($"Component disabled: [Component={uid}].");
                return;
            }

            var component = new Component(uid);

            if (_storageService.TryReadRawText(out var script, ComponentsDirectory, uid, DefaultFileNames.Script))
            {
                configuration.Script = script;
            }

            if (_storageService.TryReadSerializedValue(out IDictionary<string, object> settings, ComponentsDirectory, uid, DefaultFileNames.Settings))
            {
                foreach (var setting in settings)
                {
                    component.SetSetting(setting.Key, setting.Value);
                }
            }

            if (_storageService.TryReadSerializedValue(out HashSet<string> tags, ComponentsDirectory, uid, DefaultFileNames.Tags))
            {
                foreach (var tag in tags)
                {
                    component.SetTag(tag);
                }
            }

            lock (_components)
            {
                if (_components.TryGetValue(uid, out var existingComponent))
                {
                    existingComponent.ProcessMessage(new PythonDictionary
                    {
                        ["type"] = WirehomeMessageType.Destroy
                    });
                }

                _components[uid] = component;
            }

            new ComponentInitializer(this, _pythonScriptHostFactoryService, _packageManagerService, _scriptComponentLogicLogger, _scriptComponentAdapterLogger).InitializeComponent(
                component, configuration);
            component.ProcessMessage(new PythonDictionary
            {
                ["type"] = WirehomeMessageType.Initialize
            });

            _logger.LogInformation($"Component initialized: [Component={component.Uid}].");
        }
        catch
        {
            lock (_components)
            {
                _components.Remove(uid, out _);
            }

            throw;
        }
    }

    public IDictionary<object, object> ProcessComponentMessage(string componentUid, IDictionary<object, object> message)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var component = GetComponent(componentUid);

        try
        {
            return component.ProcessMessage(PythonConvert.ToPythonDictionary(message));
        }
        catch (Exception exception)
        {
            return new ExceptionPythonModel(exception).ToDictionary();
        }
    }

    public ComponentConfiguration ReadComponentConfiguration(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        if (!_storageService.TryReadSerializedValue(out ComponentConfiguration configuration, ComponentsDirectory, uid, DefaultFileNames.Configuration))
        {
            throw new ComponentNotFoundException(uid);
        }

        return configuration;
    }

    public void RegisterComponentSetting(string componentUid, string uid, object value)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (component.TryGetSetting(uid, out _))
        {
            return;
        }

        SetComponentSetting(componentUid, uid, value);
    }

    public object RemoveComponentSetting(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (!component.RemoveSetting(uid, out var value))
        {
            return null;
        }

        _logger.LogDebug("Component setting removed: [Component={0}] [Setting UID={1}].", component.Uid, uid);

        _storageService.WriteSerializedValue(component.GetSettings(), ComponentsDirectory, component.Uid, DefaultFileNames.Settings);
        _messageBusAdapter.PublishSettingRemovedEvent(component.Uid, uid, value);

        return value;
    }

    public bool RemoveComponentTag(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid is null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (!component.RemoveTag(uid))
        {
            return false;
        }

        _logger.LogDebug("Component tag removed: [Component={0}] [Tag UID={1}].", component.Uid, uid);

        _storageService.WriteSerializedValue(component.GetTags(), ComponentsDirectory, component.Uid, DefaultFileNames.Tags);
        _messageBusAdapter.PublishTagRemovedEvent(component.Uid, uid);

        return true;
    }

    public void SetComponentConfigurationValue(string componentUid, string uid, object value)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        GetComponent(componentUid)?.SetConfigurationValue(uid, value);
    }

    public void SetComponentSetting(string componentUid, string uid, object value)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var oldValueString = "<unset>";
        var newValueString = Convert.ToString(value, CultureInfo.InvariantCulture);

        var component = GetComponent(componentUid);
        if (!component.TryGetSetting(uid, out var oldValue))
        {
            oldValueString = Convert.ToString(oldValue, CultureInfo.InvariantCulture);
            var hasChanged = !string.Equals(oldValueString, newValueString, StringComparison.Ordinal);

            if (!hasChanged)
            {
                return;
            }
        }

        component.SetSetting(uid, value);

        _logger.LogDebug("Component setting changed: [Component={0}] [Setting UID={1}] [Value={2} -> {3}].", component.Uid, uid, oldValueString, newValueString);

        _storageService.WriteSerializedValue(component.GetSettings(), ComponentsDirectory, component.Uid, DefaultFileNames.Settings);
        _messageBusAdapter.PublishSettingChangedEvent(component.Uid, uid, oldValue, value);
    }

    public void SetComponentStatusValue(string componentUid, string uid, object value)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);

        var setStatusValueResult = component.SetStatusValue(uid, value);
        var oldValue = setStatusValueResult.OldValue;
        var isAdd = setStatusValueResult.IsNewValue;

        var oldValueString = Convert.ToString(oldValue, CultureInfo.InvariantCulture);
        var newValueString = Convert.ToString(value, CultureInfo.InvariantCulture);
        var hasChanged = isAdd || !string.Equals(oldValueString, newValueString, StringComparison.Ordinal);

        if (!hasChanged)
        {
            return;
        }

        var statusChangingEventArgs = new ComponentStatusChangingEventArgs
        {
            Timestamp = DateTime.Now,
            Component = component,
            StatusUid = uid,
            OldValue = oldValue,
            NewValue = value
        };

        _messageBusAdapter.PublishStatusChangingEvent(statusChangingEventArgs);

        _logger.LogDebug("Component status value changed: [Component={0}] [Status UID={1}] [Value={2} -> {3}].", component.Uid, uid, oldValueString, newValueString);

        var componentStatusChangedEventArgs = new ComponentStatusChangedEventArgs
        {
            Timestamp = DateTime.Now,
            Component = component,
            StatusUid = uid,
            OldValue = oldValue,
            NewValue = value
        };

        ComponentStatusChanged?.Invoke(this, componentStatusChangedEventArgs);

        _messageBusAdapter.PublishStatusChangedEvent(componentStatusChangedEventArgs);
    }

    public bool SetComponentTag(string componentUid, string uid)
    {
        if (componentUid == null)
        {
            throw new ArgumentNullException(nameof(componentUid));
        }

        if (uid is null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        var component = GetComponent(componentUid);
        if (!component.SetTag(uid))
        {
            return false;
        }

        _logger.LogDebug("Component tag set: [Component={0}] [Tag UID={1}].", component.Uid, uid);

        _storageService.WriteSerializedValue(component.GetTags(), ComponentsDirectory, component.Uid, DefaultFileNames.Tags);
        _messageBusAdapter.PublishTagAddedEvent(component.Uid, uid);

        return true;
    }

    public bool TryGetComponent(string uid, out Component component)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        lock (_components)
        {
            return _components.TryGetValue(uid, out component);
        }
    }

    public void TryInitializeComponent(string uid)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        try
        {
            InitializeComponent(uid);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Component initialization failed: [Component={uid}].");
        }
    }

    public void WriteComponentConfiguration(string uid, ComponentConfiguration configuration)
    {
        if (uid == null)
        {
            throw new ArgumentNullException(nameof(uid));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _storageService.WriteSerializedValue(configuration, ComponentsDirectory, uid, DefaultFileNames.Configuration);
    }

    protected override void OnStart()
    {
        var componentConfigurations = ReadComponentConfigurations();
        var initializationPhases = componentConfigurations.GroupBy(i => i.Value.InitializationPhase).OrderBy(p => p.Key);

        foreach (var initializationPhase in initializationPhases)
        {
            foreach (var componentUid in initializationPhase)
            {
                TryInitializeComponent(componentUid.Key);
            }
        }
    }

    Dictionary<string, ComponentConfiguration> ReadComponentConfigurations()
    {
        var componentConfigurations = new Dictionary<string, ComponentConfiguration>();
        foreach (var componentUid in GetComponentUids())
        {
            if (_storageService.TryReadSerializedValue(out ComponentConfiguration componentConfiguration, ComponentsDirectory, componentUid, DefaultFileNames.Configuration))
            {
                componentConfigurations.Add(componentUid, componentConfiguration);
            }
        }

        return componentConfigurations;
    }
}