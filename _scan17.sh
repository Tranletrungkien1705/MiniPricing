#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "########## Mst_Price file ##########"
cat "$P/84/84a64537255216bcda26ad78a2a1bffe73de61f8.svn-base" | tr -d '\0' | head -120