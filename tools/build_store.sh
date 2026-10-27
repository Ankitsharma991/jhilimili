#!/usr/bin/env bash
# Builds the signed Google Play upload (AAB) after all tests pass (Phase 5 step 1, Docs/Phase5/RELEASE.md).
# Usage: tools/build_store.sh <internal|closed|production> <version name> <version code> [--preflight]
#   e.g. tools/build_store.sh internal 0.1.0 2
# --preflight only prints the Play upload checks: no tests, no build, no key needed.
# The key comes from ~/.utsav/upload.keystore (tools/make_upload_key.sh) or UTSAV_KEYSTORE. Passwords
# are asked for here, or taken from UTSAV_KEYSTORE_PASS / UTSAV_KEY_PASS; they are never written to disk.
set -euo pipefail
cd "$(dirname "$0")/.."
if [ $# -lt 3 ]; then sed -n '2,7p' "$0"; exit 2; fi
export UTSAV_TRACK="$1" UTSAV_VERSION_NAME="$2" UTSAV_VERSION_CODE="$3"
export UTSAV_KEYSTORE="${UTSAV_KEYSTORE:-$HOME/.utsav/upload.keystore}" UTSAV_KEY_ALIAS="${UTSAV_KEY_ALIAS:-upload}"
U="$HOME/Unity/Hub/Editor/$(sed -n 's/m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity"

if [ "${4:-}" = "--preflight" ]; then
  export UTSAV_PREFLIGHT_ONLY=1
else
  unset UTSAV_PREFLIGHT_ONLY
  if [ -z "${UTSAV_KEYSTORE_PASS:-}" ]; then
    read -r -s -p "Upload keystore password: " UTSAV_KEYSTORE_PASS; echo
  fi
  export UTSAV_KEYSTORE_PASS UTSAV_KEY_PASS="${UTSAV_KEY_PASS:-$UTSAV_KEYSTORE_PASS}"
  tools/run_all.sh --tests-only
fi

set +e
timeout 3000 "$U" -batchmode -nographics -projectPath . -buildTarget Android \
  -executeMethod UtsavNights.EditorTools.StoreBuild.CiStoreAab -logFile Logs/build_store.log
code=$?
set -e
grep -a "\[UTSAV_STORE\]" Logs/build_store.log | grep -av "UnityEngine\." | sed 's/^/  /' | sort -u
grep -a "UTSAV_BUILD\] kind=StoreAab" Logs/build_store.log | tail -1 | cut -c1-200
if [ "$code" != 0 ]; then echo "store build failed (exit $code); see Logs/build_store.log"; exit "$code"; fi
if [ -n "${UTSAV_PREFLIGHT_ONLY:-}" ]; then exit 0; fi

AAB="Builds/Android/store/UtsavNights-$2-$3-$1.aab"
echo "Upload $AAB in Play Console ($1 track). Then commit ProjectSettings and Docs/Phase5/store_builds.tsv."
if [ -n "${BUNDLETOOL:-}" ]; then
  # Download size for a typical arm64 phone, as Play will serve it.
  java -jar "$BUNDLETOOL" build-apks --bundle="$AAB" --output=/tmp/utsav-size.apks --overwrite \
    --ks="$UTSAV_KEYSTORE" --ks-key-alias="$UTSAV_KEY_ALIAS" --ks-pass=env:UTSAV_KEYSTORE_PASS --key-pass=env:UTSAV_KEY_PASS >/dev/null
  echo "Download size (bytes, min,max): $(java -jar "$BUNDLETOOL" get-size total --apks=/tmp/utsav-size.apks | tail -1)"
  rm -f /tmp/utsav-size.apks
fi
