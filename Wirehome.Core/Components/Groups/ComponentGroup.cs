﻿using System;
using System.Collections.Generic;
using System.Threading;
using Wirehome.Core.Foundation;

namespace Wirehome.Core.Components.Groups;

public sealed class ComponentGroup
{
    readonly Dictionary<string, object> _settings = new();
    readonly Dictionary<string, object> _status = new();
    readonly HashSet<string> _tags = new();

    long _hash;

    public ComponentGroup(string uid)
    {
        Uid = uid ?? throw new ArgumentNullException(nameof(uid));
    }

    public ThreadSafeDictionary<string, ComponentGroupAssociation> Components { get; } = new();

    public long Hash => Interlocked.Read(ref _hash);

    public ThreadSafeDictionary<string, ComponentGroupAssociation> Macros { get; } = new();

    public string Uid { get; }

    public Dictionary<string, object> GetSettings()
    {
        lock (_settings)
        {
            // Create a copy of the internal dictionary because the result only reflects the current
            // status and will not change when the real status is changing. Also changes to that dictionary
            // should not affect the internal state.
            return new Dictionary<string, object>(_settings);
        }
    }

    public Dictionary<string, object> GetStatus()
    {
        lock (_status)
        {
            // Create a copy of the internal dictionary because the result only reflects the current
            // status and will not change when the real status is changing. Also changes to that dictionary
            // should not affect the internal state.
            return new Dictionary<string, object>(_status);
        }
    }

    public List<string> GetTags()
    {
        lock (_tags)
        {
            return new List<string>(_tags);
        }
    }

    public bool HasTag(string tag)
    {
        lock (_tags)
        {
            return _tags.Contains(tag);
        }
    }

    public bool RemoveSetting(string uid, out object oldValue)
    {
        lock (_settings)
        {
            if (_settings.TryGetValue(uid, out oldValue))
            {
                _settings.Remove(uid);
                IncrementHash();

                return true;
            }

            return false;
        }
    }

    public bool RemoveTag(string tag)
    {
        lock (_tags)
        {
            if (_tags.Remove(tag))
            {
                IncrementHash();
                return true;
            }

            return false;
        }
    }

    public SetValueResult SetSetting(string uid, object value)
    {
        lock (_settings)
        {
            var isExistingValue = _settings.TryGetValue(uid, out var oldValue);

            _settings[uid] = value;
            IncrementHash();

            return new SetValueResult
            {
                OldValue = oldValue,
                IsNewValue = !isExistingValue
            };
        }
    }

    public SetValueResult SetStatusValue(string uid, object value)
    {
        lock (_status)
        {
            var isExistingValue = _status.TryGetValue(uid, out var oldValue);

            _status[uid] = value;
            IncrementHash();

            return new SetValueResult
            {
                OldValue = oldValue,
                IsNewValue = !isExistingValue
            };
        }
    }

    public bool SetTag(string tag)
    {
        lock (_tags)
        {
            if (_tags.Add(tag))
            {
                IncrementHash();
                return true;
            }

            return false;
        }
    }

    public bool TryGetSetting(string uid, out object value)
    {
        lock (_settings)
        {
            return _settings.TryGetValue(uid, out value);
        }
    }

    public bool TryGetStatusValue(string uid, out object value)
    {
        lock (_status)
        {
            return _status.TryGetValue(uid, out value);
        }
    }

    void IncrementHash()
    {
        Interlocked.Increment(ref _hash);
    }
}