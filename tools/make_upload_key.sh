#!/usr/bin/env bash
# Creates the Google Play upload key, once, outside the repository (Phase 5 step 1, Docs/Phase5/RELEASE.md).
# Usage: tools/make_upload_key.sh
# The key goes to ~/.utsav/upload.keystore (alias "upload"). keytool asks for the password and your
# name. Keep a backup of the file and the password somewhere safe and offline: with Play App Signing a
# lost upload key can be reset through Play support, but that takes days.
set -euo pipefail
DIR="$HOME/.utsav"
KEY="$DIR/upload.keystore"
U="$HOME/Unity/Hub/Editor/$(sed -n 's/m_EditorVersion: //p' "$(dirname "$0")/../ProjectSettings/ProjectVersion.txt")/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool"
KEYTOOL="$(command -v keytool || echo "$U")"
if [ -e "$KEY" ]; then
  echo "$KEY already exists; not overwriting it."
  exit 1
fi
mkdir -p "$DIR" && chmod 700 "$DIR"
"$KEYTOOL" -genkeypair -v -keystore "$KEY" -storetype PKCS12 -alias upload -keyalg RSA -keysize 2048 -validity 10000
chmod 600 "$KEY"
echo
echo "Created $KEY (alias upload). Back it up, with its password, now."
echo "Upload certificate fingerprint (Play Console shows the same one under App integrity):"
"$KEYTOOL" -list -keystore "$KEY" -alias upload | grep -i "fingerprint" || true
