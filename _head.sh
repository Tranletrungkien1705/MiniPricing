#!/bin/bash
BASE=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
N="${1:-600}"
shift
for f in "$@"; do
  p="$BASE/${f:0:2}/$f.svn-base"
  echo "===== $f ====="
  head -c "$N" "$p"
  echo
  echo
done
