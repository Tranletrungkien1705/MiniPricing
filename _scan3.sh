#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "===== Controllers ====="
grep -rl 'namespace ProductCenter.Biz.Web.Controllers' "$SRC" 2>/dev/null | while read f; do
  cls=$(grep -oE 'class [A-Za-z0-9_]+Controller' "$f" | head -1)
  [ -n "$cls" ] && echo "$cls  <- $f"
done | sort -u
