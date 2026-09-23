#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files with 'WAS_Mst_ProductType_Create(' method def ==="
grep -rln 'WAS_Mst_ProductType_Create' "$P" 2>/dev/null
echo
echo "=== files with 'Mst_ProductType_CheckDB' method def ==="
grep -rln 'Mst_ProductType_CheckDB' "$P" 2>/dev/null