#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== ErrProductCenter files ==="
grep -rl 'class ErrProductCenter' "$P" 2>/dev/null
echo
echo "=== error codes mentioning Product ==="
for f in $(grep -rl 'class ErrProductCenter' "$P" 2>/dev/null); do
  grep -oE 'Mst_Product[A-Za-z0-9_]*' "$f" | sort -u
done