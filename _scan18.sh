#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in 49/4916238efc431ecc9fb59edab5803612d1192bc4 a2/a285cbe7d3f46e64a3eb311d00670543e894e766 a7/a7745339c76b6ef5662f70d04f204e2a69b7ab0f d8/d81e64085f8e29bad44ddbfc038526ef394d72a4 f5/f584781810f664281edcbff4852d4ddb70ae477f f6/f6840a8049edd82bcb15a5929007927f2d53b39c; do
  echo "########## $f ##########"
  head -c 300 "$P/$f.svn-base" | tr -d '\0'
  echo
  echo "--- classes ---"
  grep -oE '(class|interface|enum) [A-Za-z0-9_]+' "$P/$f.svn-base" | head -5
  echo
done