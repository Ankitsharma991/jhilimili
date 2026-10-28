using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace UtsavNights.FirebaseSdk
{
    /// <summary>
    /// Checks Google Play services once and runs work that waited for Firebase. Until it is ready, calls
    /// queue (at most <see cref="MaxQueued"/>); if Firebase is unavailable, they are dropped and the game plays on.
    /// </summary>
    internal static class FirebaseStartup
    {
        private const int MaxQueued = 64;
        private static readonly Queue<Action> Waiting = new Queue<Action>();
        private static bool _started;

        public static bool Ready { get; private set; }

        public static bool Failed { get; private set; }

        public static void Run(Action action)
        {
            if (Ready)
            {
                action();
                return;
            }

            if (Failed)
            {
                return;
            }

            if (Waiting.Count < MaxQueued)
            {
                Waiting.Enqueue(action);
            }

            Start();
        }

        private static void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.Result != DependencyStatus.Available)
                {
                    Failed = true;
                    Waiting.Clear();
                    Debug.LogWarning($"[Firebase] unavailable ({(task.IsFaulted ? task.Exception?.GetBaseException().Message : task.Result.ToString())}); analytics stay off");
                    return;
                }

                Ready = true;
                Gameplay.StartupProbe.Mark("firebase");
                while (Waiting.Count > 0)
                {
                    Waiting.Dequeue()();
                }
            });
        }
    }
}
