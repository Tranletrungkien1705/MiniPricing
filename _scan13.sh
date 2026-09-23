#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
# List all .svn-base files that are C# source (start with BOM + using/namespace)
for f in $(grep -rl 'namespace ProductCenter' "$P" 2>/dev/null); do
  # get the class/interface names
  names=$(grep -oE '(class|interface|enum) [A-Za-z0-9_]+' "$f" | head -3 | tr '\n' ' ')
  echo "$f :: $names"
done | head -80
