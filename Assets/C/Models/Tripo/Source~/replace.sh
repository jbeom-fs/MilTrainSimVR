#!/bin/bash
# 승인된 후보로 원본 FBX · 텍스처를 같은 경로 · 같은 이름으로 덮어쓴다 (.meta · .mat · 씬은 그대로)
# 사용: replace.sh <종류> <후보 폴더 이름>   예) replace.sh TallShelf w1
set -euo pipefail
S="$(cd "$(dirname "$0")" && pwd)"
M="/c/Users/leesn/UnityProject/MilTrainSimVR/Assets/C/Models/Tripo"
kind="$1"; cand="$2"
case "$kind" in
  TallShelf)   base=industrial_shelving_3d_model; tex=industrial_shelving_3d_model.fbm/industrial_shelving_3d_model_basecolor.jpg ;;
  LowCabinet)  base=wooden_cabinet_3d_model;      tex=wooden_cabinet_3d_model.fbm/wooden_cabinet_3d_model_basecolor.jpg ;;
  ShopCounter) base=wooden_desk_3d_model_1;       tex=wooden_desk_3d_model_1.fbm/wooden_desk_3d_model_1_basecolor.jpg ;;
  OfficeDesk)  base=gray_office_desk_3d_model;    tex=gray_office_desk_3d_model.fbm/tripo_image_5be263ea-4bbd-46d4-9e69-4e7824e12c47_0_0.jpg ;;
  SandbagWall) base=sandbag_stack_3d_model;       tex=sandbag_stack_3d_model.fbm/sandbag_stack_3d_model_basecolor.JPEG ;;
  *) echo "unknown kind $kind"; exit 1 ;;
esac
src_fbx="$S/cand/$kind/$cand/$base.fbx"
src_tex="$S/cand/$kind/$cand/$(basename "$tex")"
dst_fbx="$M/$kind/$base.fbx"
dst_tex="$M/$kind/$tex"
for f in "$src_fbx" "$src_tex" "$dst_fbx" "$dst_tex" "$dst_fbx.meta" "$dst_tex.meta"; do [ -f "$f" ] || { echo "missing $f"; exit 1; }; done
mkdir -p "$S/backup/$kind"
[ -f "$S/backup/$kind/$base.fbx" ] || cp -p "$dst_fbx" "$S/backup/$kind/"
[ -f "$S/backup/$kind/$(basename "$tex")" ] || cp -p "$dst_tex" "$S/backup/$kind/"
before=$(md5sum "$dst_fbx.meta" "$dst_tex.meta" | awk '{print $1}' | tr '\n' ' ')
cp "$src_fbx" "$dst_fbx"
cp "$src_tex" "$dst_tex"
after=$(md5sum "$dst_fbx.meta" "$dst_tex.meta" | awk '{print $1}' | tr '\n' ' ')
[ "$before" = "$after" ] && meta=same || meta=CHANGED
echo "$kind <- $cand | fbx $(stat -c %s "$dst_fbx") B | tex $(stat -c %s "$dst_tex") B | meta $meta"
