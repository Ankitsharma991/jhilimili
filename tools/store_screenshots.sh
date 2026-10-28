#!/usr/bin/env bash
# Captures Play Store screenshots from the connected phone or tablet (Phase 5 step 5, Docs/Phase5/STORE_LISTING.md).
# Usage: tools/store_screenshots.sh [label]
# Set up each shot on the device yourself, then press Enter here to capture it; type q and Enter to finish.
# Each capture is saved as a 24-bit PNG without alpha (Play rejects transparency) and checked against
# Play's size rules: 320 to 3840 px per side, long side at most twice the short side.
set -euo pipefail
LABEL="${1:-}"
command -v adb >/dev/null || { echo "adb not found"; exit 1; }
adb get-state >/dev/null || { echo "No device connected"; exit 1; }
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/store-${MODEL}-$(date +%Y%m%d-%H%M%S)${LABEL:+-$LABEL}"
mkdir -p "$OUT"
echo "Saving to $OUT. Use a release APK so the developer overlay is hidden (don't triple-tap the corner)."
n=0
while true; do
  read -r -p "Shot $((n + 1)): set it up on the device, then Enter (q to finish): " answer
  [ "$answer" = "q" ] && break
  n=$((n + 1))
  raw="$OUT/raw-$n.png"
  adb exec-out screencap -p > "$raw"
  python3 - "$raw" "$OUT/screenshot-$(printf %02d $n).png" <<'PY'
import sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
img = Image.open(src).convert("RGB")
w, h = img.size
img.save(dst, optimize=True)
problems = []
if min(w, h) < 320 or max(w, h) > 3840:
    problems.append("each side must be 320 to 3840 px")
if max(w, h) > 2 * min(w, h):
    problems.append("the long side must be at most twice the short side")
orientation = "portrait" if h >= w else "LANDSCAPE (R0 listing is portrait)"
print(f"  {dst}: {w}x{h} {orientation}" + ("" if not problems else "  PROBLEM: " + "; ".join(problems)))
PY
  rm -f "$raw"
done
echo "$n screenshot(s) in $OUT. Play needs 2 to 8 per device type; 4 or more at 1080 px or larger can be featured."
