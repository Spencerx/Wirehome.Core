﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Wirehome.Core.Components;
using Wirehome.Core.Components.Configuration;
using Wirehome.Core.Components.Exceptions;
using Wirehome.Core.Python;

namespace Wirehome.Core.HTTP.Controllers;

[ApiController]
public sealed class ComponentsController : Controller
{
    readonly ComponentRegistryService _componentRegistryService;

    public ComponentsController(ComponentRegistryService componentRegistryService)
    {
        _componentRegistryService = componentRegistryService ?? throw new ArgumentNullException(nameof(componentRegistryService));
    }

    [HttpPost]
    [Route("/api/v1/components/{componentUid}/tags/{tag}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void AddTag(string componentUid, string tag)
    {
        _componentRegistryService.SetComponentTag(componentUid, tag);
    }

    // TODO: Move to dedicated factory!
    public static object CreateComponentModel(Component component)
    {
        if (component is null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        return new
        {
            component.Uid,
            component.Hash,
            Status = component.GetStatus(),
            Settings = component.GetSettings(),
            Tags = component.GetTags(),
            Configuration = component.GetConfiguration()
        };
    }

    [HttpDelete]
    [Route("/api/v1/components/{uid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void DeleteComponent(string uid)
    {
        _componentRegistryService.DeleteComponent(uid);
    }

    [HttpDelete]
    [Route("/api/v1/components/{componentUid}/settings/{settingUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public object DeleteSetting(string componentUid, string settingUid)
    {
        return _componentRegistryService.RemoveComponentSetting(componentUid, settingUid);
    }

    [HttpDelete]
    [Route("/api/v1/components/{componentUid}/tags/{tag}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void DeleteTag(string componentUid, string tag)
    {
        _componentRegistryService.RemoveComponentTag(componentUid, tag);
    }

    [HttpGet]
    [Route("/api/v1/components/{uid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public object GetComponent(string uid)
    {
        return CreateComponentModel(_componentRegistryService.GetComponent(uid));
    }

    [HttpGet]
    [Route("api/v1/components")]
    [ApiExplorerSettings(GroupName = "v1")]
    public List<object> GetComponents()
    {
        return _componentRegistryService.GetComponents().Select(c => CreateComponentModel(c)).ToList();
    }

    [HttpGet]
    [Route("/api/v1/tags/{tag}/components")]
    [ApiExplorerSettings(GroupName = "v1")]
    public List<string> GetComponentsWithTag(string tag)
    {
        var components = _componentRegistryService.GetComponentsWithTag(tag);
        return components.Select(c => c.Uid).ToList();
    }

    [HttpGet]
    [Route("api/v1/components/uids")]
    [ApiExplorerSettings(GroupName = "v1")]
    public List<string> GetComponentUids()
    {
        return _componentRegistryService.GetComponentUids();
    }

    [HttpGet]
    [Route("api/v1/components/{uid}/configuration")]
    [ApiExplorerSettings(GroupName = "v1")]
    public ComponentConfiguration GetConfiguration(string uid)
    {
        try
        {
            return _componentRegistryService.ReadComponentConfiguration(uid);
        }
        catch (ComponentNotFoundException)
        {
            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return null;
        }
    }

    [HttpGet]
    [Route("/api/v1/components/{componentUid}/runtime_configuration/{configurationUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public object GetConfigurationValue(string componentUid, string configurationUid)
    {
        var component = _componentRegistryService.GetComponent(componentUid);

        if (!component.TryGetConfigurationValue(configurationUid, out var value))
        {
            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return null;
        }

        return value;
    }

    [HttpGet]
    [Route("/api/v1/components/{uid}/runtime_configuration")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IReadOnlyDictionary<string, object> GetConfigurationValues(string uid)
    {
        return _componentRegistryService.GetComponent(uid).GetConfiguration();
    }

    [HttpGet]
    [Route("/api/v1/components/{uid}/debug_information")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IDictionary<object, object> GetDebugInformation(string uid, [FromBody] IDictionary<object, object> parameters)
    {
        return _componentRegistryService.GetComponent(uid).GetDebugInformation(PythonConvert.ToPythonDictionary(parameters));
    }

    [HttpGet]
    [Route("/api/v1/components/{componentUid}/settings/{settingUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public object GetSettingValue(string componentUid, string settingUid)
    {
        return _componentRegistryService.GetComponentSetting(componentUid, settingUid);
    }

    [HttpGet]
    [Route("/api/v1/components/{uid}/settings")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IReadOnlyDictionary<string, object> GetSettingValues(string uid)
    {
        return _componentRegistryService.GetComponent(uid).GetSettings();
    }

    [HttpGet]
    [Route("/api/v1/components/{componentUid}/status/{statusUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public object GetStatusValue(string componentUid, string statusUid)
    {
        var component = _componentRegistryService.GetComponent(componentUid);

        if (!component.TryGetStatusValue(statusUid, out var value))
        {
            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return null;
        }

        return value;
    }

    [HttpGet]
    [Route("/api/v1/components/{uid}/status")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IReadOnlyDictionary<string, object> GetStatusValues(string uid)
    {
        return _componentRegistryService.GetComponent(uid).GetStatus();
    }

    [HttpGet]
    [Route("/api/v1/components/{componentUid}/tags/{tag}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public string GetTag(string componentUid, string tag)
    {
        var component = _componentRegistryService.GetComponent(componentUid);
        if (!component.HasTag(tag))
        {
            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return null;
        }

        return tag;
    }

    [HttpGet]
    [Route("/api/v1/components/{componentUid}/tags")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IReadOnlyList<string> GetTags(string componentUid)
    {
        var component = _componentRegistryService.GetComponent(componentUid);
        return component.GetTags();
    }

    [HttpPost]
    [Route("api/v1/components/{uid}/configuration")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void PostConfiguration(string uid, [FromBody] ComponentConfiguration configuration)
    {
        _componentRegistryService.WriteComponentConfiguration(uid, configuration);
    }

    [HttpPost]
    [Route("api/v1/components/{uid}/disable")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IDictionary<object, object> PostDisable(string uid)
    {
        return _componentRegistryService.DisableComponent(uid);
    }

    [HttpPost]
    [Route("api/v1/components/{uid}/enable")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IDictionary<object, object> PostEnable(string uid)
    {
        return _componentRegistryService.EnableComponent(uid);
    }

    [HttpPost]
    [Route("api/v1/components/{uid}/initialize")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void PostInitialize(string uid)
    {
        _componentRegistryService.InitializeComponent(uid);
    }

    [HttpPost]
    [Route("/api/v1/components/{uid}/process_message")]
    [ApiExplorerSettings(GroupName = "v1")]
    public IDictionary<object, object> PostProcessMessage(string uid, [FromBody] IDictionary<object, object> message, bool returnUpdatedComponent = true)
    {
        var result = _componentRegistryService.ProcessComponentMessage(uid, message);

        if (returnUpdatedComponent)
        {
            result["component"] = CreateComponentModel(_componentRegistryService.GetComponent(uid));
        }

        return result;
    }

    [HttpPost]
    [Route("/api/v1/components/{componentUid}/settings/{settingUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void PostSettingValue(string componentUid, string settingUid, [FromBody] object value)
    {
        _componentRegistryService.SetComponentSetting(componentUid, settingUid, value);
    }

    [HttpPost]
    [Route("/api/v1/components/{componentUid}/status/{statusUid}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public void PostStatusValue(string componentUid, string statusUid, [FromBody] object value)
    {
        _componentRegistryService.SetComponentStatusValue(componentUid, statusUid, value);
    }
}