#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files mentioning Mst_ProductType ==="
grep -rl 'Mst_ProductType' "$P" 2>/dev/null
echo
echo "=== files mentioning MstProductTypeController ==="
grep -rl 'MstProductTypeController' "$P" 2>/dev/null