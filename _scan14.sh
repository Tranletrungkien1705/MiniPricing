#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== Controllers with Price in name ==="
for f in $(grep -rl 'namespace ProductCenter' "$P" 2>/dev/null); do
  n=$(grep -oE 'class [A-Za-z0-9_]*Price[A-Za-z0-9_]*' "$f" | head -1)
  [ -n "$n" ] && echo "$f :: $n"
done
echo
echo "=== Entities (Mst_*) with Price ==="
for f in $(grep -rl 'namespace ProductCenter' "$P" 2>/dev/null); do
  n=$(grep -oE 'class Mst_[A-Za-z0-9_]*Price[A-Za-z0-9_]*' "$f" | head -1)
  [ -n "$n" ] && echo "$f :: $n"
done