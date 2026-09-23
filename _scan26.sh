#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in $(grep -rl 'class MstProductTypeController' "$P" 2>/dev/null); do
  echo "########## $f ##########"
  cat "$f" | tr -d '\0'
  echo
done