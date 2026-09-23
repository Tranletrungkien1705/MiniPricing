#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/9b/9b81b409820ee23a9d674177b3d673ef1ca6e14c.svn-base"
echo "=== head ==="
head -c 200 "$f" | tr -d '\0'
echo
echo "=== method defs ==="
grep -n 'WAS_Mst_ProductType_Create\|WAS_Mst_ProductType_Update\|WAS_Mst_ProductType_Delete\|Mst_ProductType_CheckDB' "$f" | head -40