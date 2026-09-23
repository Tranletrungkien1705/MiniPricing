#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/3f/3f426e58637a07a3ea2cef05f42e560ac097a2c6.svn-base"
echo "=== Mst_Product_DeleteX (6566-6800) ==="
sed -n '6566,6800p' "$f" | tr -d '\0'