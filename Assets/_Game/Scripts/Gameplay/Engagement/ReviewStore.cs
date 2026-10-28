using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Engagement;

namespace UtsavNights.Gameplay
{
    /// <summary>review.json: launches counted and when the rating prompt was last asked for. Corrupt files start over.</summary>
    public sealed class ReviewStore
    {
        public const string FileName = "review.json";

        public ReviewStore(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
        }

        public string Path { get; }

        public ReviewState Load()
        {
            try
            {
                return File.Exists(Path) ? JsonUtility.FromJson<ReviewState>(File.ReadAllText(Path)) ?? new ReviewState() : new ReviewState();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Review] {Path} unreadable, starting over: {e.Message}");
                return new ReviewState();
            }
        }

        public void Save(ReviewState state)
        {
            try
            {
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(state));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Review] could not save {Path}: {e.Message}");
            }
        }
    }
}
