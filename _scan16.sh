#!/bin/bash
P=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine
echo "=== Mst_Price ==="
grep -rl 'Mst_Price' "$P" 2>/dev/null
echo "=== Promotion files ==="
grep -rl 'Promotion' "$P" 2>/dev/null
echo "=== Promo files ==="
grep -rl 'Promo' "$P" 2>/dev/null | head -30