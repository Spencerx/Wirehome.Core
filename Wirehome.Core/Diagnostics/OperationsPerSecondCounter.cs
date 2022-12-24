﻿using System;
using System.Threading;

namespace Wirehome.Core.Diagnostics;

public class OperationsPerSecondCounter
{
    int _current;

    public OperationsPerSecondCounter(string uid)
    {
        Uid = uid ?? throw new ArgumentNullException(nameof(uid));
    }

    public int Count { get; private set; }

    public string Uid { get; }

    public void Increment()
    {
        Interlocked.Increment(ref _current);
    }

    public void Reset()
    {
        Count = Interlocked.Exchange(ref _current, 0);
    }
}