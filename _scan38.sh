#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files with 'WAS_Mst_ProductType_Create(' as method (with paren) ==="
grep -rln 'WAS_Mst_ProductType_Create(' "$P" 2>/dev/null
echo "=== files with 'WAS_Mst_ProductType_Update(' ==="
grep -rln 'WAS_Mst_ProductType_Update(' "$P" 2>/dev/null
echo "=== files with 'WAS_Mst_ProductType_Delete(' ==="
grep -rln 'WAS_Mst_ProductType_Delete(' "$P" 2>/dev/null