﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Wirehome.Core.Storage
{
    public class StorageService
    {
        private readonly JsonSerializerService _jsonSerializerService;
        private readonly ILogger _logger;

        public StorageService(JsonSerializerService jsonSerializerService, ILoggerFactory loggerFactory)
        {
            _jsonSerializerService = jsonSerializerService ?? throw new ArgumentNullException(nameof(jsonSerializerService));

            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<StorageService>();
        }

        public string BinPath { get; private set; }

        public string DataPath { get; private set; }

        public void Start()
        {
            BinPath = AppDomain.CurrentDomain.BaseDirectory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DataPath = Path.Combine(Environment.ExpandEnvironmentVariables("%appData%"), "Wirehome");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                DataPath = Path.Combine("/etc/wirehome");
            }
            else
            {
                throw new NotSupportedException();
            }

            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }

            _logger.Log(LogLevel.Information, $"Bin path  = {BinPath}");
            _logger.Log(LogLevel.Information, $"Data path = {DataPath}");
        }

        public List<string> EnumeratureDirectories(string pattern, params string[] path)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            if (path == null) throw new ArgumentNullException(nameof(path));

            var directory = Path.Combine(DataPath, Path.Combine(path));
            if (!Directory.Exists(directory))
            {
                return new List<string>();
            }

            var directories = Directory.EnumerateDirectories(directory, pattern, SearchOption.AllDirectories).ToList();
            for (var i = 0; i < directories.Count; i++)
            {
                directories[i] = directories[i].Replace(directory, string.Empty).TrimStart(Path.DirectorySeparatorChar);
            }

            return directories;
        }

        public List<string> EnumerateFiles(string pattern, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var relativePath = Path.Combine(path);
            var directory = Path.Combine(DataPath, relativePath);

            if (!Directory.Exists(directory))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).ToList();
            for (var i = 0; i < files.Count; i++)
            {
                files[i] = files[i].Replace(directory, string.Empty).TrimStart(Path.DirectorySeparatorChar);
            }

            return files;
        }

        public bool TryRead<TValue>(out TValue value, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".json";
            }

            if (!File.Exists(filename))
            {
                value = default(TValue);
                return false;
            }

            var json = File.ReadAllText(filename, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                value = default(TValue);
                return true;
            }

            value = _jsonSerializerService.Deserialize<TValue>(json);
            return true;
        }

        public bool TryReadText(out string value, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            if (!File.Exists(filename))
            {
                value = null;
                return false;
            }

            value = File.ReadAllText(filename, Encoding.UTF8);
            return true;
        }

        public bool TryReadRaw(out byte[] content, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            if (!File.Exists(filename))
            {
                content = null;
                return false;
            }

            content = File.ReadAllBytes(filename);
            return true;
        }

        public bool TryReadOrCreate<TValue>(out TValue value, params string[] path) where TValue : class, new()
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            if (!TryRead(out value, path))
            {
                value = new TValue();
                Write(value, path);
                return false;
            }

            return true;
        }

        public void Write(object value, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".json";
            }

            var directory = Path.GetDirectoryName(filename);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (value == null)
            {
                File.WriteAllBytes(filename, new byte[0]);
                return;
            }

            var json = _jsonSerializerService.Serialize(value);
            File.WriteAllText(filename, json, Encoding.UTF8);
        }

        public void WriteRaw(byte[] content, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            var directory = Path.GetDirectoryName(filename);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filename, content ?? new byte[0]);
        }

        public void WriteText(string value, params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var filename = Path.Combine(DataPath, Path.Combine(path));
            var directory = Path.GetDirectoryName(filename);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filename, value ?? string.Empty, Encoding.UTF8);
        }

        public void DeleteFile(params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var fullPath = Path.Combine(DataPath, Path.Combine(path));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public void DeleteDirectory(params string[] path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var fullPath = Path.Combine(DataPath, Path.Combine(path));
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }
    }
}
