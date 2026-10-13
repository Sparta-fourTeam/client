"""Validate a release candidate against confirmed Play uploads, not build history."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


def release_number(policy, requested_version, main_policy=None, apk_build=False):
    version = policy['versionName']
    code = policy['versionCode']
    if not isinstance(version, str) or not re.fullmatch(r'\d+(?:\.\d+){1,2}', version):
        raise ValueError('Invalid candidate version name')
    if type(code) is not int or not 0 < code <= 2100000000:
        raise ValueError('Invalid candidate version code')
    if requested_version != version:
        raise ValueError('Requested version must match the committed release candidate')
    consumed = {}
    for ledger in [policy, main_policy or {}]:
        seen = set()
        for row in ledger.get('playUploads', []):
            number, name = row['versionCode'], row['versionName']
            if type(number) is not int or number <= 0 or not isinstance(name, str) or not re.fullmatch(r'\d+(?:\.\d+){1,2}', name):
                raise ValueError('Invalid confirmed Play upload record')
            if number in seen:
                raise ValueError('Duplicate code in one confirmed Play upload ledger')
            seen.add(number)
            if number in consumed and consumed[number] != name:
                raise ValueError('Conflicting confirmed Play upload records')
            consumed[number] = name
        if sorted(seen) != list(range(1, len(seen) + 1)):
            raise ValueError('Each confirmed Play upload ledger must be sequential from 1')
    if sorted(consumed) != list(range(1, len(consumed) + 1)):
        raise ValueError('Confirmed Play upload codes must be sequential from 1')
    if len(set(consumed.values())) != len(consumed):
        raise ValueError('A Play version name cannot consume multiple upload codes')
    legacy = [row['versionCode'] for ledger in [policy, main_policy or {}] for row in ledger.get('legacyDraftUploads', [])]
    if code in legacy:
        raise ValueError('Candidate code has already been uploaded as a legacy draft')
    historical_apk = apk_build and consumed.get(code) == version
    if not historical_apk and (code != len(consumed) + 1 or version in consumed.values()):
        raise ValueError('Candidate must use the next unused code and version name')
    return version, code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    parser.add_argument('--apk-build', action='store_true', help='Allow an exact recorded code/version for an APK rebuild')
    args = parser.parse_args()
    policy = json.loads(Path('.github/android-release.json').read_text())
    # Checkout fetch-depth: 0 supplies origin/main. This also catches a stale branch
    # after a newer main commit records a confirmed Play upload.
    subprocess.run(['git', 'rev-parse', '--verify', 'refs/remotes/origin/main'], check=True, capture_output=True)
    current = subprocess.run(['git', 'show', 'refs/remotes/origin/main:.github/android-release.json'],
                             text=True, capture_output=True)
    main_policy = json.loads(current.stdout) if current.returncode == 0 else None
    version, code = release_number(policy, args.version.removeprefix('v'), main_policy, args.apk_build)
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
