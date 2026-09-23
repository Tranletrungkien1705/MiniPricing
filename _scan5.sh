#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for name in Mst_SpecType1 Mst_SpecType2 Mst_ProductGroup Mst_ProductType Mst_Area Mst_CustomerGroup; do
  echo "########## $name ##########"
  for f in $(grep -rln "class $name\b" "$SRC" 2>/dev/null); do
    echo "---- $f"
    head -c 1600 "$f" | tr -d '\0'
    echo
  done
done
