#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
framework_repo="git@github.com:Jin8868/AlloyFramework.git"
framework_branch="main"
framework_path="$repo_root/AlloyFramework"
unity_project="$repo_root/UnityProj"

command -v git >/dev/null 2>&1 || { echo "Git was not found." >&2; exit 1; }
test -f "$unity_project/ProjectSettings/ProjectVersion.txt" || {
  echo "Unity project is missing: $unity_project" >&2
  exit 1
}

if [[ ! -e "$framework_path" ]]; then
  git clone --branch "$framework_branch" --single-branch "$framework_repo" "$framework_path"
elif [[ ! -d "$framework_path/.git" ]]; then
  echo "Framework path exists but is not a Git repository: $framework_path" >&2
  exit 1
else
  origin_url="$(git -C "$framework_path" remote get-url origin)"
  [[ "$origin_url" == "$framework_repo" ]] || {
    echo "Unexpected Alloy Framework origin: $origin_url" >&2
    exit 1
  }
  git -C "$framework_path" fetch --prune origin
  current_branch="$(git -C "$framework_path" branch --show-current)"
  if [[ -n "$(git -C "$framework_path" status --porcelain)" ]]; then
    echo "Alloy Framework has local changes; automatic pull skipped."
  elif [[ "$current_branch" != "$framework_branch" ]]; then
    echo "Alloy Framework is on $current_branch; automatic pull skipped."
  else
    git -C "$framework_path" pull --rebase origin "$framework_branch"
  fi
fi

for git_repo in "$repo_root" "$framework_path"; do
  [[ -d "$git_repo/.git" ]] || continue
  git -C "$git_repo" config --local pull.rebase true
  git -C "$git_repo" config --local fetch.prune true
done

echo "Setup completed."
echo "Unity project: $unity_project"
echo "Framework:     $framework_path"
