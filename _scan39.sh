#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files with 'WAS_Mst_Product_Create(' ==="
grep -rln 'WAS_Mst_Product_Create(' "$P" 2>/dev/null
echo "=== files with 'Mst_Product_CheckDB(' ==="
grep -rln 'Mst_Product_CheckDB(' "$P" 2>/dev/null
echo "=== files with 'WAS_Mst_Product_Update(' ==="
grep -rln 'WAS_Mst_Product_Update(' "$P" 2>/dev/null