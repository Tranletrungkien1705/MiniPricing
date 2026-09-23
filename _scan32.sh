#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/43/439a010d286c696b2957e144014af5795f2857a7.svn-base"
echo "=== head ==="
head -c 200 "$f" | tr -d '\0'
echo
echo "=== WAS_Mst_ProductType method lines ==="
grep -n 'WAS_Mst_ProductType\|Mst_ProductType_' "$f" | head -60