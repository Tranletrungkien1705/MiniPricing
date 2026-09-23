#!/bin/bash
SRC=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
f=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine/80/80ff70976aeaf9a1e877fb16ed02054209d4280d.svn-base
grep -n 'Mst_SpecType1' "$f" | head -80
