using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UtsavNights.Core.Packs;

namespace UtsavNights.Gameplay
{
    /// <summary>Packs on the phone's storage (Application.persistentDataPath). Directory moves are renames: atomic on one volume.</summary>
    public sealed class FilePackStorage : IPackStorage
    {
        private readonly string _root;

        public FilePackStorage(string root) => _root = root;

        public bool Exists(string path) => File.Exists(Full(path));

        public byte[] Read(string path) => File.Exists(Full(path)) ? File.ReadAllBytes(Full(path)) : null;

        public void Write(string path, byte[] data)
        {
            string full = Full(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            AtomicFile.WriteAllBytes(full, data);
        }

        public void Delete(string path)
        {
            if (File.Exists(Full(path)))
            {
                File.Delete(Full(path));
            }
        }

        public bool DirectoryExists(string dir) => Directory.Exists(Full(dir));

        public void DeleteDirectory(string dir)
        {
            if (Directory.Exists(Full(dir)))
            {
                Directory.Delete(Full(dir), true);
            }
        }

        public void MoveDirectory(string from, string to)
        {
            string target = Full(to);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            Directory.Move(Full(from), target);
        }

        private string Full(string path) => Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>Manifests, catalogs and the index as JSON (JsonUtility, safe off the main thread).</summary>
    public sealed class JsonPackJson : IPackJson
    {
        public string Write(object value) => JsonUtility.ToJson(value, true);

        public PackManifest ReadManifest(string json) => Read<PackManifest>(json);

        public PackIndex ReadIndex(string json) => Read<PackIndex>(json);

        public PackCatalog ReadCatalog(string json) => Read<PackCatalog>(json);

        private static T Read<T>(string json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>Fetches pack files over HTTPS (PRD 16: HTTPS only). Runs on a worker thread.</summary>
    public sealed class HttpPackTransport : IPackTransport
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly string _base;

        public HttpPackTransport(string baseUrl) => _base = baseUrl.TrimEnd('/') + "/";

        public byte[] Fetch(string path)
        {
            if (!_base.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new PackTransportException("packs are only fetched over HTTPS");
            }

            try
            {
                HttpResponseMessage response = Client.GetAsync(_base + path).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    throw new PackTransportException($"{(int)response.StatusCode} for {path}");
                }

                return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                throw new PackTransportException(e.Message);
            }
            catch (TaskCanceledException e)
            {
                throw new PackTransportException("timed out: " + e.Message);
            }
        }
    }

    /// <summary>Fetches pack files from a folder in the hosting layout (the editor and tests: Docs/Phase9/sample_packs).</summary>
    public sealed class FolderPackTransport : IPackTransport
    {
        private readonly string _root;

        public FolderPackTransport(string root) => _root = root;

        public byte[] Fetch(string path)
        {
            string full = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                throw new PackTransportException("missing " + path);
            }

            return File.ReadAllBytes(full);
        }
    }
}
