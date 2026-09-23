#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files with WAS_Mst_ProductType ==="
grep -rl 'WAS_Mst_ProductType' "$P" 2>/dev/null
echo
echo "=== files with Mst_ProductType_Create_InvalidProductType ==="
grep -rl 'Mst_ProductType_Create_InvalidProductType' "$P" 2>/dev/null