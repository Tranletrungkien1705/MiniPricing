#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in $(grep -rln 'Mst_SpecPrice' "$SRC" 2>/dev/null); do
  echo "== $f"
  head -c 400 "$f" | tr -d '\0'
  echo
done | head -120
