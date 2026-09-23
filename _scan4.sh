#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for name in MstSpecType1 MstSpecType2 MstProductGroup MstProductType MstArea MstCustomerGroup; do
  echo "########## $name ##########"
  for f in $(grep -rln "class $name\b" "$SRC" 2>/dev/null); do
    echo "---- $f"
    head -c 1500 "$f" | tr -d '\0'
    echo
  done
done
