#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for f in 3f/3f426e58637a07a3ea2cef05f42e560ac097a2c6 5f/5ff63b5eef98c59404a22dbcdc78cd865e86a5b8 6a/6a79ae98efa0135b852007c8e58247c0ed1c14e4 93/9341dd6ff167576ee32681d739af4f9bf4499955 96/96806605a05f41c4b2e47d9a66e7795103a4bf84; do
  echo "########## $f ##########"
  head -c 200 "$P/$f.svn-base" | tr -d '\0'
  echo
  grep -n 'WAS_Mst_ProductType_Create\|WAS_Mst_ProductType_Update\|WAS_Mst_ProductType_Delete\|Mst_ProductType_CheckDB' "$P/$f.svn-base" | head -20
  echo
done