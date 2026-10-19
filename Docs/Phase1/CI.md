# CI setup (GitHub Actions + GameCI)

The workflow is `.github/workflows/ci.yml`. It runs EditMode tests on every push and pull request, then
builds a release AAB and uploads it as an artifact.

## One-time secrets

1. Activate Unity Personal in Unity Hub on this computer.
2. Copy the licence file contents: `cat ~/.local/share/unity3d/Unity/Unity_lic.ulf`.
3. In GitHub, open the repository, then Settings > Secrets and variables > Actions, and add:
   - `UNITY_LICENSE`: the full file contents
   - `UNITY_EMAIL` and `UNITY_PASSWORD`: your Unity account
4. Push. The first run takes long because Library is not cached yet.

Notes:

- GameCI publishes editor images per version; if 6000.3.26f1 is missing, set `unityVersion` in the
  workflow to the closest available 6000.3 patch [Verify on game.ci/docs].
- Git LFS bandwidth on free GitHub accounts is limited; keep large art out of the repo until Phase 3.
