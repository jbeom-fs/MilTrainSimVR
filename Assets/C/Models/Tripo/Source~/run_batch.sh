#!/bin/bash
# 사용: run_batch.sh 목록파일 — 스펙마다 make_lowpoly 실행, 로그는 스펙 옆에
BL="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
S="$(dirname "$0")"
while read -r spec; do
  [ -z "$spec" ] && continue
  "$BL" -b --factory-startup --python "$S/scripts/make_lowpoly.py" -- "$spec" > "${spec%.json}.log" 2>&1
  PYTHONIOENCODING=utf-8 python "$S/scripts/summ.py" "${spec%.json}.log" | sed 's/ | mat [^|]*//'
done < "$1"
echo BATCH_DONE
