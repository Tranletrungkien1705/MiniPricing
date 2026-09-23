#!/bin/bash
f=D:/idocNet/2019.4.ProductCenter/Dev/V20/.svn/pristine/80/80ff70976aeaf9a1e877fb16ed02054209d4280d.svn-base
sed -n '25358,25430p' "$f" | tr -d '\0'
echo "==================== CREATE ===================="
sed -n '25730,25845p' "$f" | tr -d '\0'
echo "==================== UPDATE ===================="
sed -n '25920,26020p' "$f" | tr -d '\0'
echo "==================== DELETE ===================="
sed -n '26020,26120p' "$f" | tr -d '\0'
