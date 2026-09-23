#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in $(grep -rl 'Mst_SpecPrice' "$P" 2>/dev/null); do
  echo "== $f"
  head -c 160 "$f" | tr -d '\0'
  echo
done
