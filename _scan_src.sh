#!/bin/bash
# scan source pristine for text files matching a keyword
KW="$1"
for f in $(grep -rln "$KW" D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine 2>/dev/null); do
  t=$(file -b "$f")
  case "$t" in
    *text*) echo "$f :: $t" ;;
  esac
done
