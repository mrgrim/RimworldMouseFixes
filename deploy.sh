#!/usr/bin/env bash

echo "Deploying to: ${1}/Mods/MouseFixes"

[ ! -d "${1}/Mods/MouseFixes" ] && exit 1

cp -R LICENSE.txt Assemblies Source Languages Defs About "${1}/Mods/MouseFixes"
