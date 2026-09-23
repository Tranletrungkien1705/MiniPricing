#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "===== WAS_Mst_SpecType1 business ====="
for f in $(grep -rln 'WAS_Mst_SpecType1_Create' "$SRC" 2>/dev/null); do
  echo "---- $f"
  cat "$f" | tr -d '\0'
done
