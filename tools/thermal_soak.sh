#!/usr/bin/env bash
# Logs battery temperature and thermal status every 30 s while you run "Soak 15 min" in the app.
# Usage: tools/thermal_soak.sh [minutes]
set -euo pipefail
MINUTES="${1:-15}"
OUT="captures/thermal-$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')-$(date +%Y%m%d-%H%M%S).csv"
mkdir -p captures
echo "elapsed_s,battery_temp_c,thermal_status" > "$OUT"
end=$(( $(date +%s) + MINUTES * 60 ))
start=$(date +%s)
while [ "$(date +%s)" -lt "$end" ]; do
  t=$(adb shell dumpsys battery | awk -F': ' '/temperature/ {print $2/10}' | tr -d '\r')
  s=$(adb shell dumpsys thermalservice 2>/dev/null | awk -F': ' '/Thermal Status/ {print $2; exit}' | tr -d '\r')
  echo "$(( $(date +%s) - start )),${t},${s:-n/a}" | tee -a "$OUT"
  sleep 30
done
echo "Saved $OUT. Thermal status 0 = none, 1 = light, 2 = moderate, 3+ = throttling."
