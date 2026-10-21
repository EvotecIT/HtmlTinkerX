#!/usr/bin/env bash
# Grant user namespaces only to this ephemeral CI job's installed Chromium binaries.
set -euo pipefail

mode="${1:-enable}"
[[ "${GITHUB_ACTIONS:-}" == "true" ]] || { echo "This helper is restricted to GitHub Actions." >&2; exit 1; }
[[ "${GITHUB_RUN_ID:-}" =~ ^[0-9]+$ && "${GITHUB_RUN_ATTEMPT:-}" =~ ^[0-9]+$ ]] || exit 1
profile_name="htmltinkerx-ci-chromium-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}"
profile_path="/etc/apparmor.d/${profile_name}"

if [[ "$mode" == "cleanup" ]]; then
    if [[ -f "$profile_path" ]]; then
        status=0
        sudo apparmor_parser -R "$profile_path" || status=$?
        sudo rm -- "$profile_path"
        exit "$status"
    fi
    exit 0
fi
[[ "$mode" == "enable" ]] || { echo "Expected enable or cleanup." >&2; exit 1; }
if [[ ! -r /proc/sys/kernel/apparmor_restrict_unprivileged_userns ]] ||
   [[ "$(cat /proc/sys/kernel/apparmor_restrict_unprivileged_userns)" != "1" ]]; then
    echo "No AppArmor user-namespace exception is required on this runner."
    exit 0
fi
command -v apparmor_parser >/dev/null
[[ ! -e "$profile_path" ]] || { echo "The job profile already exists; refusing to replace it." >&2; exit 1; }
browser_root="$(realpath -e "${PLAYWRIGHT_BROWSERS_PATH:-$HOME/.cache/ms-playwright}")"
temporary="$(mktemp "${RUNNER_TEMP:?}/htmltinkerx-chromium-profile.XXXXXX")"
trap 'rm -f -- "$temporary"' EXIT
printf 'abi <abi/4.0>,\ninclude <tunables/global>\n' > "$temporary"
count=0
while IFS= read -r -d '' executable; do
    resolved="$(realpath -e "$executable")"
    [[ "$resolved" == "$browser_root/"* && "$resolved" =~ ^[a-zA-Z0-9_./-]+$ ]] || exit 1
    [[ -x "$resolved" ]] || continue
    printf 'profile %s_%s "%s" flags=(unconfined) {\n  userns,\n}\n' \
        "$profile_name" "$count" "$resolved" >> "$temporary"
    count=$((count + 1))
done < <(find "$browser_root" -maxdepth 3 -type f \
    \( -path '*/chromium-*/chrome-linux*/chrome' \
       -o -path '*/chromium_headless_shell-*/chrome-headless-shell-linux*/chrome-headless-shell' \) -print0)
[[ "$count" -gt 0 ]] || { echo "No installed Chromium executables were found." >&2; exit 1; }
sudo install -m 0644 "$temporary" "$profile_path"
sudo apparmor_parser -r "$profile_path"
echo "Enabled sandbox user namespaces for $count exact Chromium executable paths in this job."
