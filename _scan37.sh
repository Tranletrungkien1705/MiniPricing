#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/6a/6a79ae98efa0135b852007c8e58247c0ed1c14e4.svn-base"
sed -n '1735,1810p' "$f" | tr -d '\0'