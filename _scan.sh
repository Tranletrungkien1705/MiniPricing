#!/bin/bash
BASE=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in "$@"; do
  p="$BASE/${f:0:2}/$f.svn-base"
  echo "=== $f ==="
  file "$p"
  echo
done
