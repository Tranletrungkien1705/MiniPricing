#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f=$(grep -rl 'class BizProductCenter' "$P" 2>/dev/null | head -1)
echo "file: $f"
echo "=== WAS_Mst_ProductType methods ==="
grep -n 'WAS_Mst_ProductType' "$f" | head -40