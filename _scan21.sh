#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for cls in Mst_Product Mst_ProductType Mst_Attribute Mst_SpecCustomField; do
  echo "########## $cls ##########"
  f=$(grep -rl "class $cls" "$P" 2>/dev/null | head -1)
  echo "file: $f"
  cat "$f" | tr -d '\0'
  echo
done