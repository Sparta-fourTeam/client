"""Allocate Actions AAB codes or validate a historical APK against the release ledger."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


# This namespace is bound to the existing repository/workflow counter. Changing
# the ID or base requires a new range above every previously allocated code.
AUTOMATIC_CODE_BASE = 300000000
AUTOMATIC_WORKFLOW_ID = 378016847
REPOSITORY = 'Sparta-fourTeam/client'
MAX_VERSION_CODE = 2100000000


def automatic_release_number(policy, requested_version, main_policy, run, pages, env, expected_code=None):
    """Allocate only on a new, authenticated dispatch of the pinned workflow."""
    if main_policy is None:
        raise ValueError('Current main release ledger is required')
    if not re.fullmatch(r'[0-9]+(?:\.[0-9]+){1,2}', requested_version):
        raise ValueError('Invalid release version; enter a version such as 1.0.3 without v')
    if (env.get('GITHUB_REPOSITORY') != REPOSITORY or
            run.get('repository', {}).get('full_name') != REPOSITORY or
            run.get('workflow_id') != AUTOMATIC_WORKFLOW_ID or
            run.get('event') != 'workflow_dispatch' or
            run.get('id') != int(env['GITHUB_RUN_ID']) or
            run.get('run_number') != int(env['GITHUB_RUN_NUMBER']) or
            run.get('head_sha') != env['GITHUB_SHA']):
        raise ValueError('Release counter identity mismatch; workflow migration requires review')
    if run.get('run_attempt') != 1 or env.get('GITHUB_RUN_ATTEMPT') != '1':
        raise ValueError('Re-run would reuse a versionCode; use Run workflow for a new dispatch')
    number = run['run_number']
    if type(number) is not int or number <= 0:
        raise ValueError('Invalid workflow run number')
    runs = [row for page in pages for row in page['workflow_runs']]
    if not any(row['id'] == run['id'] for row in runs):
        raise ValueError('Current run missing from workflow history')
    if any(row['workflow_id'] != AUTOMATIC_WORKFLOW_ID or
           type(row['run_number']) is not int or row['run_number'] <= 0 for row in runs):
        raise ValueError('Invalid workflow history')
    # Concurrency groups are not FIFO. A smaller queued run must not publish
    # after a larger run, even if the newer run failed or only built an AAB.
    if max(row['run_number'] for row in runs) != number:
        raise ValueError('A newer dispatch exists; use the newest Run workflow instead')
    code = AUTOMATIC_CODE_BASE + number
    if code > MAX_VERSION_CODE:
        raise ValueError('Automatic versionCode exceeds the Google Play limit')
    for ledger in [policy, main_policy or {}]:
        records = [ledger] + ledger.get('playUploads', []) + ledger.get('legacyDraftUploads', [])
        for row in records:
            used = row['versionCode']
            if type(used) is not int or not 0 < used <= MAX_VERSION_CODE:
                raise ValueError('Invalid recorded version code')
            if code <= used:
                raise ValueError('Automatic versionCode must exceed every recorded code')
        if requested_version in [row['versionName'] for row in ledger.get('playUploads', [])]:
            raise ValueError('Version name is already recorded as uploaded')
    if len(f'{requested_version}({code})') > 50:
        raise ValueError('Play release name exceeds 50 characters')
    if expected_code is not None and code != expected_code:
        raise ValueError('Built artifact code does not match this dispatch')
    return requested_version, code


def github_json(endpoint, paginate=False):
    command = ['gh', 'api', endpoint]
    if paginate:
        command += ['--paginate', '--slurp']
    return json.loads(subprocess.check_output(command, text=True))


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
            if type(number) is not int or not 0 < number <= MAX_VERSION_CODE or not isinstance(name, str) or not re.fullmatch(r'\d+(?:\.\d+){1,2}', name):
                raise ValueError('Invalid confirmed Play upload record')
            if number in seen:
                raise ValueError('Duplicate code in one confirmed Play upload ledger')
            seen.add(number)
            if number in consumed and consumed[number] != name:
                raise ValueError('Conflicting confirmed Play upload records')
            consumed[number] = name
        # Keep historical small-code integrity checks. Confirmed automatic AAB
        # receipts may have gaps because every dispatch consumes a run number.
        small = {number for number in seen if number <= AUTOMATIC_CODE_BASE}
        if sorted(small) != list(range(1, len(small) + 1)):
            raise ValueError('Each confirmed Play upload ledger must be sequential from 1')
    small = {number for number in consumed if number <= AUTOMATIC_CODE_BASE}
    if sorted(small) != list(range(1, len(small) + 1)):
        raise ValueError('Confirmed Play upload codes must be sequential from 1')
    if len(set(consumed.values())) != len(consumed):
        raise ValueError('A Play version name cannot consume multiple upload codes')
    legacy = [row['versionCode'] for ledger in [policy, main_policy or {}] for row in ledger.get('legacyDraftUploads', [])]
    if code in legacy:
        raise ValueError('Candidate code has already been uploaded as a legacy draft')
    historical_apk = apk_build and consumed.get(code) == version
    if not historical_apk and (code != len(small) + 1 or version in consumed.values()):
        raise ValueError('Candidate must use the next unused code and version name')
    return version, code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--apk-build', action='store_true', help='Allow an exact recorded code/version for an APK rebuild')
    mode.add_argument('--automatic', action='store_true', help='Use the pinned Actions dispatch counter for a Play AAB')
    parser.add_argument('--expected-code', type=int, help='Verify the AAB code before publishing; never allocate a second code')
    args = parser.parse_args()
    if args.expected_code is not None and not args.automatic:
        parser.error('--expected-code requires --automatic')
    policy = json.loads(Path('.github/android-release.json').read_text())
    # Checkout fetch-depth: 0 supplies origin/main. This also catches a stale branch
    # after a newer main commit records a confirmed Play upload.
    subprocess.run(['git', 'rev-parse', '--verify', 'refs/remotes/origin/main'], check=True, capture_output=True)
    current = subprocess.run(['git', 'show', 'refs/remotes/origin/main:.github/android-release.json'],
                             text=True, capture_output=True)
    main_policy = json.loads(current.stdout) if current.returncode == 0 else None
    if args.automatic:
        run = github_json(f'repos/{REPOSITORY}/actions/runs/{int(os.environ["GITHUB_RUN_ID"])}')
        pages = github_json(f'repos/{REPOSITORY}/actions/workflows/{AUTOMATIC_WORKFLOW_ID}/runs?per_page=100', paginate=True)
        version, code = automatic_release_number(policy, args.version, main_policy, run, pages, os.environ, args.expected_code)
    else:
        version, code = release_number(policy, args.version.removeprefix('v'), main_policy, args.apk_build)
    tag = subprocess.run(['git', 'rev-parse', '--verify', '--quiet', 'refs/tags/v' + version + '^{tree}'],
                         text=True, capture_output=True)
    if tag.returncode == 0:
        tree = subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], text=True).strip()
        if tag.stdout.strip() != tree:
            raise ValueError('Existing version tag belongs to a different source tree')
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'version_name={version}\ncode={code}\n')
    print(f'Validated candidate {version}, code {code}')
    if args.automatic and 'GITHUB_STEP_SUMMARY' in os.environ:
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
            summary.write(f'### Android release candidate\n\nVersion: `{version}`  \n'
                          f'versionCode: `{code}` = {AUTOMATIC_CODE_BASE} + run {run["run_number"]}  \n'
                          f'Source: `{os.environ["GITHUB_SHA"]}`\n\n'
                          'This is a build candidate; it is not proof of a Play upload.\n')


if __name__ == '__main__':
    main()
