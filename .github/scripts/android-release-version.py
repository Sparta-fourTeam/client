"""Validate a release candidate against confirmed Play releases, not build history."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


def release_number(policy, requested_version, main_policy=None):
    version = policy['versionName']
    code = policy['versionCode']
    if not isinstance(version, str) or not re.fullmatch(r'\d+(?:\.\d+){1,2}', version):
        raise ValueError('Invalid candidate version name')
    if type(code) is not int or not 0 < code <= 2100000000:
        raise ValueError('Invalid candidate version code')
    if requested_version != version:
        raise ValueError('Requested version must match the committed release candidate')
    released = {}
    for ledger in [policy, main_policy or {}]:
        seen = set()
        for row in ledger.get('playReleases', []):
            number, name = row['versionCode'], row['versionName']
            if type(number) is not int or number <= 0 or not isinstance(name, str) or not re.fullmatch(r'\d+(?:\.\d+){1,2}', name):
                raise ValueError('Invalid confirmed Play release record')
            if number in seen:
                raise ValueError('Duplicate code in one confirmed Play release ledger')
            seen.add(number)
            if number in released and released[number] != name:
                raise ValueError('Conflicting confirmed Play release records')
            released[number] = name
    if sorted(released) != list(range(1, len(released) + 1)):
        raise ValueError('Confirmed Play release codes must be sequential from 1')
    if len(set(released.values())) != len(released):
        raise ValueError('A Play version name cannot consume multiple release codes')
    if code != len(released) + 1 or version in released.values():
        raise ValueError('Candidate must use the next unpublished release code and version name')
    if main_policy and code == main_policy['versionCode'] and version != main_policy['versionName']:
        raise ValueError('The same pending release code cannot name a different main candidate')
    return version, code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    args = parser.parse_args()
    policy = json.loads(Path('.github/android-release.json').read_text())
    # Checkout fetch-depth: 0 supplies origin/main. This also catches a stale branch
    # after a newer main commit records a confirmed Play release.
    subprocess.run(['git', 'rev-parse', '--verify', 'refs/remotes/origin/main'], check=True, capture_output=True)
    current = subprocess.run(['git', 'show', 'refs/remotes/origin/main:.github/android-release.json'],
                             text=True, capture_output=True)
    main_policy = json.loads(current.stdout) if current.returncode == 0 else None
    version, code = release_number(policy, args.version.removeprefix('v'), main_policy)
    tag = subprocess.run(['git', 'rev-parse', '--verify', '--quiet', 'refs/tags/v' + version + '^{tree}'],
                         text=True, capture_output=True)
    if tag.returncode == 0:
        tree = subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], text=True).strip()
        if tag.stdout.strip() != tree:
            raise ValueError('Existing version tag belongs to a different source tree')
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'version_name={version}\ncode={code}\n')
    print(f'Validated candidate {version}, code {code}; build-only history does not consume codes')


if __name__ == '__main__':
    main()
