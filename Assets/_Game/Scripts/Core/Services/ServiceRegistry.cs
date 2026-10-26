using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Services
{
    /// <summary>
    /// Minimal service locator (PRD 13.2 Core). Services register once at boot; gameplay asks by type.
    /// Lookups are dictionary reads with no allocation; cache the result if called every frame.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (_services.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"Service {typeof(T).Name} is already registered.");
            }

            _services.Add(typeof(T), service);
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out object service))
            {
                return (T)service;
            }

            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered. Was the Boot scene loaded?");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public bool Unregister<T>() where T : class => _services.Remove(typeof(T));

        public bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        public int Count => _services.Count;

        public void Clear() => _services.Clear();
    }
}
