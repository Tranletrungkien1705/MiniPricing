#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f="$P/3f/3f426e58637a07a3ea2cef05f42e560ac097a2c6.svn-base"
echo "=== method defs ==="
grep -n 'private void Mst_Product_CheckDB\|public DataSet WAS_Mst_Product_Create\|public DataSet WAS_Mst_Product_Update\|public DataSet WAS_Mst_Product_Delete\|public DataSet WAS_Mst_Product_Get\|private void Mst_Product_' "$f" | head -40