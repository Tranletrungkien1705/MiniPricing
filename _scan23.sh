#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "########## Mst_Product model ##########"
cat "$P/41/41a11a851593f1deac2d228311101e934e90eb40.svn-base" | tr -d '\0'
echo
echo "########## Mst_ProductType model ##########"
cat "$P/c8/c8b6fae4ea667d23b8a0d4e3c467935a849a2b3c.svn-base" | tr -d '\0'