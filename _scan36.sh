#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/6a/6a79ae98efa0135b852007c8e58247c0ed1c14e4.svn-base"
echo "=== method defs ==="
grep -n 'WAS_Mst_ProductType_Create\|WAS_Mst_ProductType_Update\|WAS_Mst_ProductType_Delete\|WAS_Mst_ProductType_Get\|Mst_ProductType_CheckDB\|private void Mst_ProductType' "$f" | head -40