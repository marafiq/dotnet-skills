#!/usr/bin/env bash
# install-codex.sh — link every skill in this repo into the user's Codex skills directory.
#
# Codex CLI discovers skills from $HOME/.agents/skills/ (personal scope), so the
# script creates one relative symlink per skill folder. Re-running the script is
# safe: existing symlinks pointing at the same target are skipped, broken links
# are replaced, and unrelated files in the target dir are left alone.
#
# Usage:
#   bash scripts/install-codex.sh             # link into ~/.agents/skills (default)
#   bash scripts/install-codex.sh --copy      # copy instead of symlink (Windows / restricted FS)
#   bash scripts/install-codex.sh --target DIR# link into a custom directory
#   bash scripts/install-codex.sh --dry-run   # print what would happen, change nothing
#   bash scripts/install-codex.sh --uninstall # remove only the links this repo would create
#
# The Codex CLI also discovers per-repo skills under <repo>/.agents/skills/. If
# you want this repo's skills in a *specific* working repo only, link them in
# that repo with:
#   bash scripts/install-codex.sh --target /path/to/repo/.agents/skills
set -euo pipefail

target="$HOME/.agents/skills"
mode="symlink"
dry_run=0
uninstall=0

while [ $# -gt 0 ]; do
  case "$1" in
    --copy) mode="copy"; shift ;;
    --target) target="${2:?--target needs a directory}"; shift 2 ;;
    --target=*) target="${1#--target=}"; shift ;;
    --dry-run) dry_run=1; shift ;;
    --uninstall) uninstall=1; shift ;;
    -h|--help)
      sed -n '2,18p' "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
src="$repo_root/skills"

if [ ! -d "$src" ]; then
  echo "no skills/ directory found at $src" >&2
  exit 1
fi

run() {
  if [ "$dry_run" -eq 1 ]; then
    printf 'DRY: %s\n' "$*"
  else
    "$@"
  fi
}

run mkdir -p "$target"

count=0
for dir in "$src"/*/; do
  [ -d "$dir" ] || continue
  name="$(basename "$dir")"
  dest="$target/$name"

  if [ "$uninstall" -eq 1 ]; then
    # Only remove a symlink that points back into this repo.
    if [ -L "$dest" ]; then
      link_target="$(readlink "$dest")"
      case "$link_target" in
        "$dir"|"${dir%/}") run rm -f "$dest"; count=$((count + 1)) ;;
        *) echo "skip: $dest -> $link_target (not from this repo)" ;;
      esac
    fi
    continue
  fi

  # If the destination already exists, decide how to handle it.
  if [ -L "$dest" ]; then
    link_target="$(readlink "$dest")"
    if [ "$link_target" = "${dir%/}" ] || [ "$link_target" = "$dir" ]; then
      echo "ok:   $name (already linked)"
      continue
    fi
    echo "warn: $dest is a symlink to $link_target — replacing"
    run rm -f "$dest"
  elif [ -e "$dest" ]; then
    echo "skip: $dest exists and is not a symlink — leaving untouched" >&2
    continue
  fi

  if [ "$mode" = "copy" ]; then
    run cp -R "$dir" "$dest"
    echo "copy: $name -> $dest"
  else
    run ln -s "${dir%/}" "$dest"
    echo "link: $name -> $dest"
  fi
  count=$((count + 1))
done

if [ "$uninstall" -eq 1 ]; then
  echo "removed $count link(s) from $target"
else
  echo "installed $count skill(s) into $target"
  echo "restart Codex if it does not pick them up automatically."
fi
