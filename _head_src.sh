#!/bin/bash
# head a list of pristine files given as args (relative to pristine root)
ROOT=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in "$@"; do
  echo "=== $f ==="
  head -8 "$ROOT/$f.svn-base"
  echo
done
