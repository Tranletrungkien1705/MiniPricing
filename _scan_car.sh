#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in $(grep -rln 'class Mst_' "$P" 2>/dev/null); do
  echo "== $f"
  grep -oE 'class Mst_[A-Za-z0-9]+' "$f" | sort -u | head -8
done
