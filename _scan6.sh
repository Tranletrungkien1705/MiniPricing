#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "===== MstSpecType1Controller ====="
for f in $(grep -rln 'class MstSpecType1Controller' "$SRC" 2>/dev/null); do
  cat "$f" | tr -d '\0'
done
