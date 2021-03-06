﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Wirehome.Core.Constants;
using Wirehome.Core.Model;
using Wirehome.Core.Python;
using Wirehome.Core.Python.Proxies;

namespace Wirehome.Core.FunctionPool
{
    public class FunctionPoolService
    {
        private readonly Dictionary<string, Func<WirehomeDictionary, WirehomeDictionary>> _functions = new Dictionary<string, Func<WirehomeDictionary, WirehomeDictionary>>();

        private readonly ILogger<FunctionPoolService> _logger;

        public FunctionPoolService(PythonEngineService pythonEngineService, ILoggerFactory loggerFactory)
        {
            if (pythonEngineService == null) throw new ArgumentNullException(nameof(pythonEngineService));
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _logger = loggerFactory.CreateLogger<FunctionPoolService>();

            pythonEngineService.RegisterSingletonProxy(new FunctionPoolPythonProxy(this));
        }

        public void Start()
        {
        }

        public List<string> GetRegisteredFunctions()
        {
            lock (_functions)
            {
                return _functions.Keys.ToList();
            }
        }

        public void RegisterFunction(string uid, Func<WirehomeDictionary, WirehomeDictionary> function)
        {
            if (uid == null) throw new ArgumentNullException(nameof(uid));
            if (function == null) throw new ArgumentNullException(nameof(function));

            lock (_functions)
            {
                _functions[uid] = function;
            }

            _logger.LogDebug($"Function '{uid}' registered.");
        }

        public bool FunctionRegistered(string uid)
        {
            lock (_functions)
            {
                return _functions.ContainsKey(uid);
            }
        }

        public WirehomeDictionary InvokeFunction(string uid, WirehomeDictionary parameters)
        {
            Func<WirehomeDictionary, WirehomeDictionary> function;
            lock (_functions)
            {
                if (!_functions.TryGetValue(uid, out function))
                {
                    throw new NotSupportedException();
                }
            }

            try
            {
                return function(parameters ?? new WirehomeDictionary());
            }
            catch (Exception exception)
            {
                return new WirehomeDictionary()
                    .WithType(ControlType.Exception)
                    .WithValue("exception.type", exception.GetType().Name)
                    .WithValue("exception.message", exception.Message)
                    .WithValue("exception.stack_trace", exception.StackTrace);
            }
        }
    }
}