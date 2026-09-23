#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== find Mst_Product model class ==="
grep -rl 'public class Mst_Product$' "$P" 2>/dev/null
grep -rl 'class Mst_Product\b' "$P" 2>/dev/null | while read f; do
  if grep -q 'namespace ProductCenter.Common.Models' "$f"; then echo "MODEL: $f"; fi
done
echo "=== find Mst_ProductType model class ==="
grep -rl 'class Mst_ProductType\b' "$P" 2>/dev/null | while read f; do
  if grep -q 'namespace ProductCenter.Common.Models' "$f"; then echo "MODEL: $f"; fi
done