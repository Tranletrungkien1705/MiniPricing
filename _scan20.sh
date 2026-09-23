#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== All Mst_* entity classes (namespace ProductCenter.Common.Models) ==="
for f in $(grep -rl 'namespace ProductCenter.Common.Models' "$P" 2>/dev/null); do
  grep -oE 'class Mst_[A-Za-z0-9_]+' "$f" | head -1
done | sort -u