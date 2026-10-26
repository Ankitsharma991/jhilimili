using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UtsavNights.Core.Services;

namespace UtsavNights.Core.Runtime
{
    /// <summary>
    /// Lives in the tiny Boot scene (first in Build Settings). Core services already exist
    /// (<see cref="Game"/> auto-boots); this loads the main scene in the background behind the splash
    /// and announces <see cref="SceneReadyEvent"/> (PRD 6.1: splash under 2 s, F01 interactive in 6 s or less).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string mainScene = "Main";

        public float Progress { get; private set; }

        public void Configure(string mainSceneName) => mainScene = mainSceneName;

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(mainScene) || SceneManager.GetActiveScene().name == mainScene)
            {
                yield break;
            }

            // Survive the Single-mode load that unloads the Boot scene, then remove ourselves.
            DontDestroyOnLoad(gameObject);
            AsyncOperation load = SceneManager.LoadSceneAsync(mainScene, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"[Boot] Scene '{mainScene}' is not in Build Settings.");
                Destroy(gameObject);
                yield break;
            }

            while (!load.isDone)
            {
                Progress = load.progress;
                yield return null;
            }

            Progress = 1f;

            // One rendered frame of the main scene before declaring it interactive.
            yield return null;
            Game.Events.Publish(new SceneReadyEvent((float)Time.realtimeSinceStartupAsDouble));
            Destroy(gameObject);
        }
    }
}
