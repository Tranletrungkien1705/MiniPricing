#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "===== files containing WAS_Mst_SpecType1_Create definition ====="
grep -rln 'public DataSet WAS_Mst_SpecType1_Create\|DataSet WAS_Mst_SpecType1_Create' "$SRC" 2>/dev/null
echo "===== files containing Mst_SpecType1_CheckDB ====="
grep -rln 'Mst_SpecType1_CheckDB' "$SRC" 2>/dev/null
echo "===== error codes for SpecType1 ====="
grep -rhn 'Mst_SpecType1' "$SRC" 2>/dev/null | grep 'public const' | sort -u
