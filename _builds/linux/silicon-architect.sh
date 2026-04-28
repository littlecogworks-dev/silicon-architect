#!/bin/sh
printf '\033c\033]0;%s\a' silicon-architect
base_path="$(dirname "$(realpath "$0")")"
"$base_path/silicon-architect.x86_64" "$@"
