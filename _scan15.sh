#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
for kw in PriceList PriceGroup PricePolicy PriceRule Promotion Promo PriceByGroup PriceByTime PriceByArea PriceByCustomer PriceByNetwork Mst_Price Mst_Promotion Mst_Policy; do
  c=$(grep -rl "$kw" "$P" 2>/dev/null | wc -l)
  echo "$kw : $c files"
done