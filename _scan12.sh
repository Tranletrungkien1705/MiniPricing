#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== files mentioning Mst_PriceList / PriceList ==="
grep -rl 'Mst_PriceList' "$P" 2>/dev/null | head
echo "=== files mentioning Mst_PriceItem ==="
grep -rl 'Mst_PriceItem' "$P" 2>/dev/null | head
echo "=== files mentioning Mst_SpecPriceGroup / PriceGroup ==="
grep -rl 'PriceGroup' "$P" 2>/dev/null | head
echo "=== files mentioning Mst_PriceByGroup ==="
grep -rl 'PriceByGroup' "$P" 2>/dev/null | head
echo "=== files mentioning Mst_PriceListHist ==="
grep -rl 'Mst_PriceListHist' "$P" 2>/dev/null | head
