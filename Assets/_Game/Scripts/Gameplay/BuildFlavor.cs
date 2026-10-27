namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Whether this player is a Google Play store build (Phase 5 step 1). The store build script compiles
    /// with UTSAV_STORE; every other build (editor, release APK for device tests) keeps the developer
    /// overlay, its measurement tools and the local playtest log.
    /// </summary>
    public static class BuildFlavor
    {
#if UTSAV_STORE
        private const bool CompiledAsStore = true;
#else
        private const bool CompiledAsStore = false;
#endif

        public static bool IsStore { get; private set; } = CompiledAsStore;

        /// <summary>Play Mode tests only: behave like a store build until the returned scope is disposed.</summary>
        public static System.IDisposable OverrideForTests(bool isStore)
        {
            bool previous = IsStore;
            IsStore = isStore;
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly bool _previous;

            public Restore(bool previous) => _previous = previous;

            public void Dispose() => IsStore = _previous;
        }
    }
}
