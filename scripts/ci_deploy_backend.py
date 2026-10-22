#!/usr/bin/env python3
"""Test an image before deployment; restore the previous image on failed rollout."""
import argparse
import json
import os
import random
import re
import sys
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request


def command(*args):
    return subprocess.check_output(list(args), text=True).strip()


def az(*args):
    # Retrying the same image/env update is idempotent. Never retry arbitrary mutations.
    retryable_command = args[:2] in [('containerapp', 'show'), ('containerapp', 'update')]
    invocation = ['az', *args, '--only-show-errors', '--output', 'json']
    for attempt in range(5):
        result = subprocess.run(invocation, capture_output=True, text=True)
        if result.returncode == 0:
            return json.loads(result.stdout or 'null')
        diagnostic = (result.stderr or '') + '\n' + (result.stdout or '')
        transient = re.search(r'\b(?:429|500|502|503|504)\b|Too\s*Many\s*Requests|ServiceUnavailable|GatewayTimeout|InternalServerError', diagnostic, re.IGNORECASE)
        retry_after = re.search(r'retry-after[\s:=]+(\d+)', diagnostic, re.IGNORECASE)
        delay = max(5 * 2 ** attempt, int(retry_after[1]) if retry_after else 0)
        if not retryable_command or not transient or attempt == 4 or delay > 60:
            if result.stderr:
                print(result.stderr.strip(), file=sys.stderr, flush=True)
            raise subprocess.CalledProcessError(result.returncode, invocation, output=result.stdout, stderr=result.stderr)
        delay += random.uniform(0, 1)
        print(f'Azure throttled or temporarily unavailable; retrying {args[1]} in {delay:.1f}s ({attempt + 1}/4).', flush=True)
        time.sleep(delay)
    raise AssertionError('Unreachable retry state')


def healthy(url, revision=None, timeout=180, alive=None):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if alive is not None and not alive():
            raise RuntimeError('Production container exited before becoming healthy.')
        try:
            request = urllib.request.Request(url, headers={'Cache-Control': 'no-cache'})
            with urllib.request.urlopen(request, timeout=min(10, max(1, deadline - time.monotonic()))) as response:
                if response.status == 200 and (revision is None or response.headers.get('X-PACE-Revision') == revision):
                    return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(2)
    raise RuntimeError('The expected application version did not become healthy.')


def smoke(image, variables=None):
    values = dict(variables or {})
    # Startup tests must not seed accounts or run production mutations.
    values = {k: v for k, v in values.items() if not k.startswith('Auth__SeedAdminEmails')}
    values.update({'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_HTTP_PORTS': '8080',
                   'Auth__Enabled': 'true', 'Auth__SeedAdminEmails__0': '', 'Deployment__Revision': 'startup-check',
                   'Jwt__Key': values.get('Jwt__Key') or secrets.token_urlsafe(48)})
    with tempfile.TemporaryDirectory(prefix='pace-startup-') as temporary:
        path = Path(temporary) / 'environment'
        path.touch(mode=0o600)
        if any('\n' in str(value) or '\r' in str(value) for value in values.values()):
            raise RuntimeError('Multiline environment variables are not supported by the image startup check.')
        path.write_text(''.join(f'{key}={value}\n' for key, value in values.items()))
        container = command('docker', 'run', '--detach', '--publish', '127.0.0.1::8080',
                            '--env-file', str(path), image)
        try:
            running = lambda: command('docker', 'inspect', '--format', '{{.State.Running}}', container) == 'true'
            if not running():
                raise RuntimeError('Production container exited before becoming healthy.')
            port = command('docker', 'port', container, '8080/tcp').splitlines()[0].rsplit(':', 1)[1]
            healthy(f'http://127.0.0.1:{int(port)}/health', 'startup-check', timeout=90, alive=running)
        finally:
            subprocess.run(['docker', 'rm', '--force', container], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print('Production image startup check passed.', flush=True)


def deploy(app, image, revision, resource_group, app_name):
    properties = app['properties']
    previous_image = properties['template']['containers'][0]['image']
    variables = properties['template']['containers'][0].get('env', [])
    previous_revision = next((v.get('value') for v in variables if v['name'] == 'Deployment__Revision'), None)
    url = 'https://' + properties['configuration']['ingress']['fqdn'] + '/health'
    # Capture rollback state before any mutation; never touch runtime secrets or schema.
    try:
        az('containerapp', 'update', '--name', app_name, '--resource-group', resource_group,
           '--image', image, '--set-env-vars', 'Deployment__Revision=' + revision)
        current = az('containerapp', 'show', '--name', app_name, '--resource-group', resource_group)
        if current['properties']['template']['containers'][0]['image'] != image:
            raise RuntimeError('Azure did not select the requested image.')
        healthy(url, revision)
    except Exception as failure:
        print('New deployment failed; restoring the previous image.', flush=True)
        try:
            az('containerapp', 'update', '--name', app_name, '--resource-group', resource_group,
               '--image', previous_image, '--set-env-vars', 'Deployment__Revision=' + (previous_revision or 'previous'))
            healthy(url, previous_revision)
        except Exception as rollback_failure:
            raise RuntimeError('Deployment AND rollback failed. Inspect Azure immediately; restoration is not confirmed.') from rollback_failure
        raise RuntimeError('Deployment failed; previous image was restored and its health verified.') from failure
    print(f'Deployed {revision}; the new version is serving healthy requests.', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True)
    parser.add_argument('--app', default='pace-api')
    parser.add_argument('--resource-group', default='pace-demo-rg')
    parser.add_argument('--revision', default=os.environ.get('GITHUB_SHA'))
    parser.add_argument('--smoke-only', action='store_true')
    parser.add_argument('--push', action='store_true')
    args = parser.parse_args()
    if args.smoke_only:
        smoke(args.image)
        return
    if not args.revision:
        parser.error('--revision is required for deployment')
    app = az('containerapp', 'show', '--name', args.app, '--resource-group', args.resource_group)
    smoke(args.image)
    if args.push:
        subprocess.run(['docker', 'push', args.image], check=True)
    deploy(app, args.image, args.revision, args.resource_group, args.app)


if __name__ == '__main__':
    main()
