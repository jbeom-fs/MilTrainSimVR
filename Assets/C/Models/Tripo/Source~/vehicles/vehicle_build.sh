#!/bin/bash
# Tripo 가 준 FBX 한 개를 게임용(10만 삼각형 · 4K 텍스처)으로 만든다: 원본 보관 → 텍스처 꺼내기 → spec → 줄이기 · 굽기
# 쓰는 법: vehicle_build.sh <소문자 이름(van)> <이름(Van)> <받은 model.fbx> <작업 폴더(결과 · 로그가 놓일 곳)>
LC="$1"; NAME="$2"; FBX="$3"; WORK="$4"
HERE="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"
case "$HERE" in *$'\n'*) echo "bad script folder path"; exit 9;; esac
SRC="$(dirname "$HERE")"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
D="$HERE/$LC"; OUT="$WORK/${LC}_100k"; SPEC="$WORK/spec_${LC}100k.json"
mkdir -p "$D" "$OUT"
START=$(date +%s)
cp "$FBX" "$D/${NAME}_orig.fbx" || { echo "$LC: copy failed"; exit 1; }
"$B" -b --factory-startup --python "$HERE/inspect_extract.py" -- "$D/${NAME}_orig.fbx" "$D" > "$OUT/extract.log" 2>&1
echo "$LC extract exit=$?"
PYTHONIOENCODING=utf-8 python "$HERE/make_vehicle_spec.py" "$D" "${NAME}100k" "$OUT" "$SPEC" > "$OUT/spec.log" 2>&1; rc=$?
cut -c1-900 "$OUT/spec.log"
if [ $rc -ne 0 ]; then echo "$LC: spec failed (exit $rc), nothing baked"; exit 3; fi
cp "$SPEC" "$HERE/spec_${LC}100k.json"
timeout 2700 "$B" -b --factory-startup --python "$HERE/run_vehicle_lowpoly.py" -- "$SPEC" "$SRC" "$HERE" > "$OUT/blender.log" 2>&1
echo "$LC lowpoly exit=$? seconds=$(( $(date +%s) - START ))"
grep -a -E "PROP_DONE|Error|Traceback" "$OUT/blender.log" | cut -c1-1500 | tail -n 5
ls -la "$OUT" | tail -n 9
