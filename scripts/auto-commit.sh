#!/usr/bin/env bash
# Commit and push local changes using the current git remote credentials.
# Used by GitHub Actions (GITHUB_TOKEN), Cloud Agents, and local operators.
set -euo pipefail

COMMIT_MESSAGE="${COMMIT_MESSAGE:-chore: auto-commit generated changes}"
FILE_PATTERN="${FILE_PATTERN:-.}"

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Not a git repository." >&2
  exit 1
fi

if [[ -z "$(git config --get user.name || true)" ]]; then
  git config user.name "github-actions[bot]"
fi
if [[ -z "$(git config --get user.email || true)" ]]; then
  git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
fi

# shellcheck disable=SC2086
git add -- ${FILE_PATTERN}

if git diff --cached --quiet; then
  echo "No changes to commit."
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    echo "committed=false" >> "${GITHUB_OUTPUT}"
  fi
  exit 0
fi

git commit -m "${COMMIT_MESSAGE}"
git push origin HEAD
echo "Pushed: ${COMMIT_MESSAGE}"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "committed=true" >> "${GITHUB_OUTPUT}"
fi
