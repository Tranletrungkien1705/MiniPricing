#!/bin/bash
BASE=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
PAT="$1"
grep -rliE "$PAT" "$BASE" 2>/dev/null | while read -r f; do
  t=$(file -b "$f")
  case "$t" in
    *text*) echo "$f :: $t" ;;
  esac
done
