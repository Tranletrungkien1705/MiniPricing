#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f=$(grep -rl 'class MstProductController' "$P" 2>/dev/null | head -1)
echo "file: $f"
cat "$f" | tr -d '\0' | head -200