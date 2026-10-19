# Phase 1 setup (Ubuntu 24.04)

Tick each box as you go. Commands that need `sudo` are yours to run.

## 1. Unity Hub

Either the portable AppImage (no sudo; already downloaded to `~/Applications/UnityHub.AppImage` by the
setup session) or the apt package, from the official Unity docs (docs.unity.com/en-us/hub/install-hub-linux):

```bash
sudo install -d /etc/apt/keyrings
curl -fsSL https://hub.unity3d.com/linux/keys/public | sudo gpg --dearmor -o /etc/apt/keyrings/unityhub.gpg
sudo tee /etc/apt/sources.list.d/unityhub.sources > /dev/null << 'SRC'
Types: deb
URIs: https://hub.unity3d.com/linux/repos/deb
Suites: stable
Components: main
Signed-By: /etc/apt/keyrings/unityhub.gpg
SRC
sudo apt update && sudo apt install unityhub
```

- [ ] Hub installed, signed in, Personal licence activated (Hub > Preferences > Licenses).

## 2. Unity editor 6000.3.26f1 with Android support

Open this link in a browser while Hub is running; it opens the installer for the exact version:

```
unityhub://6000.3.26f1/de8167e7440c
```

In the module list tick **Android Build Support** and both children: **OpenJDK** and
**Android SDK & NDK Tools**. About 8 to 10 GB on disk. Use Unity's bundled SDK and NDK rather than the
ones in `~/Android/Sdk`, because Unity expects a specific NDK version.

- [ ] Editor and Android modules installed.

## 3. Tools already handled

- Git LFS 3.8.0 installed to `~/.local/bin` (checksum verified). Repo has `git lfs install --local`.
- `adb` is available at `/usr/bin/adb`.
- Optional: install the GitHub CLI (`sudo apt install gh`) to manage CI secrets from the terminal.

## 4. Open the project

1. Hub > Projects > Add > Add project from disk > choose `~/Documents/jhilimili`.
2. The first import takes several minutes (packages download, Library builds).
3. If a dialog asks to enable the new Input System backend, choose **Yes** (the editor restarts).
4. Run **Utsav > Setup > Run All Phase 1 Setup**. Check the Console for `[Utsav setup]` lines.
5. Restart the editor once if the setup log says the input handler changed.
6. Window > General > Test Runner > EditMode > Run All. All tests should pass.
7. Commit the generated `.meta` files, `ProjectSettings/` and generated assets.

## 5. Phone

1. Settings > About phone > tap Build number 7 times; then Developer options > USB debugging on.
2. Connect USB, accept the RSA prompt, check with `adb devices`.

## 6. Unity Learn (about 1 week alongside setup)

Follow these on learn.unity.com, in order. Skip anything you already know.

1. Unity Essentials pathway: editor, scenes, GameObjects, components.
2. Junior Programmer pathway, units 1 to 3: C# scripting, MonoBehaviour lifecycle.
3. Creative Core pathway, VFX unit: Particle System modules.
4. A Unity 6 "Build for Android" tutorial: player settings, build and run, Profiler over USB.

Then read the spike code in `Assets/_Game/Scripts/Spikes` with the editor open; every class has a
summary comment pointing to the PRD section it serves.
