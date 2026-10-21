#!/usr/bin/env python3
"""Deploy only PACE.API. Requires Azure CLI login and a working Docker daemon."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def az(*args):
    return json.loads(subprocess.check_output(
        ['az', *args, '--only-show-errors', '--output', 'json'], text=True
    ) or 'null')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--location', default='eastasia')
    parser.add_argument('--resource-group', default='pace-demo-rg')
    parser.add_argument('--app-name', default='pace-api')
    parser.add_argument('--config', type=Path, default=ROOT / 'PACE.API/appsettings.Development.json')
    args = parser.parse_args()
    account = az('account', 'show')  # Fail before creating anything if not signed in.
    config = json.loads(args.config.read_text())
    secret_values = {
        'database': config.get('ConnectionStrings', {}).get('DefaultConnection', ''),
        'jwt-key': config.get('Jwt', {}).get('Key', '') or secrets.token_urlsafe(48),
        'admin-password-hash': config.get('AdminLogin', {}).get('PasswordHash', ''),
        'openai-key': config.get('OpenAI', {}).get('ApiKey', ''),
        'gemini-key': config.get('Gemini', {}).get('ApiKey', ''),
        'deepseek-key': config.get('DeepSeekConfig', {}).get('ApiKey', ''),
        'github-token': config.get('Analysis', {}).get('GitHubToken', ''),
    }
    for key in ['database', 'admin-password-hash', 'gemini-key', 'openai-key', 'deepseek-key']:
        if not secret_values[key]:
            raise SystemExit(f'Missing configuration: {key}. No Azure resources created.')
    if len(secret_values['jwt-key'].encode()) < 32:
        raise SystemExit('JWT key must be at least 32 bytes.')
    admins = config.get('Auth', {}).get('SeedAdminEmails', [])
    if not admins:
        raise SystemExit('Configure Auth:SeedAdminEmails before deploying.')

    suffix = hashlib.sha256((account['id'] + args.resource_group).encode()).hexdigest()[:12]
    registry = 'pace' + suffix
    environment = 'pace-environment'
    print(f"Subscription: {account['name']}; region: {args.location}; app: {args.app_name}", flush=True)
    az('extension', 'add', '--name', 'containerapp', '--upgrade')
    for provider in ['Microsoft.App', 'Microsoft.OperationalInsights', 'Microsoft.ContainerRegistry']:
        az('provider', 'register', '--namespace', provider, '--wait')
    if not az('group', 'exists', '--name', args.resource_group):
        az('group', 'create', '--name', args.resource_group, '--location', args.location)
    # Basic registry incurs its own charge even when the application scales to zero.
    az('acr', 'create', '--name', registry, '--resource-group', args.resource_group,
       '--location', args.location, '--sku', 'Basic', '--admin-enabled', 'true')
    az('containerapp', 'env', 'create', '--name', environment,
       '--resource-group', args.resource_group, '--location', args.location,
       '--logs-destination', 'none', '--enable-workload-profiles', 'true')
    env = az('containerapp', 'env', 'show', '--name', environment, '--resource-group', args.resource_group)
    acr = az('acr', 'show', '--name', registry)
    credentials = az('acr', 'credential', 'show', '--name', registry)
    password = credentials['passwords'][0]['value']
    image = acr['loginServer'] + '/pace-api:' + secrets.token_hex(6)
    # A temporary Docker config avoids persisting registry credentials on this machine.
    with tempfile.TemporaryDirectory(prefix='pace-docker-') as docker_config:
        docker_env = {**os.environ, 'DOCKER_CONFIG': docker_config}
        subprocess.run(['docker', 'login', acr['loginServer'], '--username', credentials['username'],
                        '--password-stdin'], input=password, text=True, env=docker_env, check=True)
        subprocess.run(['docker', 'build', '--platform', 'linux/amd64', '-t', image, '.'],
                       cwd=ROOT, env=docker_env, check=True)
        subprocess.run(['docker', 'push', image], env=docker_env, check=True)

    variables = [
        {'name': 'ASPNETCORE_ENVIRONMENT', 'value': 'Production'},
        {'name': 'ASPNETCORE_HTTP_PORTS', 'value': '8080'},
        {'name': 'Auth__Enabled', 'value': 'true'},
        {'name': 'Swagger__Enabled', 'value': 'true'},
        {'name': 'Analysis__CloneRoot', 'value': '/tmp/pace_repos'},
    ]
    for name, secret in {
        'ConnectionStrings__DefaultConnection': 'database', 'Jwt__Key': 'jwt-key',
        'AdminLogin__PasswordHash': 'admin-password-hash', 'Gemini__ApiKey': 'gemini-key',
        'OpenAI__ApiKey': 'openai-key',
        'DeepSeekConfig__ApiKey': 'deepseek-key', 'Analysis__GitHubToken': 'github-token',
    }.items():
        if secret_values[secret]:
            variables.append({'name': name, 'secretRef': secret})
    for i, email in enumerate(admins):
        variables.append({'name': f'Auth__SeedAdminEmails__{i}', 'value': email})
    for i, origin in enumerate(config.get('Cors', {}).get('AllowedOrigins', [])):
        variables.append({'name': f'Cors__AllowedOrigins__{i}', 'value': origin})
    secret_values['registry-password'] = password
    manifest = {
        'name': args.app_name, 'type': 'Microsoft.App/containerApps', 'location': args.location,
        'properties': {
            'managedEnvironmentId': env['id'], 'workloadProfileName': 'Consumption',
            'configuration': {
                'activeRevisionsMode': 'Single',
                'ingress': {'external': True, 'targetPort': 8080, 'transport': 'auto', 'allowInsecure': False},
                'registries': [{'server': acr['loginServer'], 'username': credentials['username'],
                                'passwordSecretRef': 'registry-password'}],
                'secrets': [{'name': key, 'value': value} for key, value in secret_values.items() if value],
            },
            'template': {
                'containers': [{'name': 'api', 'image': image, 'env': variables,
                    'resources': {'cpu': 0.5, 'memory': '1Gi'},
                    'probes': [
                        {'type': 'Startup', 'tcpSocket': {'port': 8080}, 'periodSeconds': 5, 'failureThreshold': 60},
                        {'type': 'Readiness', 'httpGet': {'path': '/health', 'port': 8080}, 'periodSeconds': 10},
                        {'type': 'Liveness', 'httpGet': {'path': '/health', 'port': 8080}, 'periodSeconds': 30},
                    ]}],
                'scale': {'minReplicas': 0, 'maxReplicas': 1,
                          'rules': [{'name': 'http', 'http': {'metadata': {'concurrentRequests': '10'}}}]},
            },
        },
    }
    # JSON is valid YAML; keep all credentials in a mode-0600 file, never CLI arguments.
    with tempfile.TemporaryDirectory(prefix='pace-azure-') as temporary:
        path = Path(temporary) / 'containerapp.json'
        path.touch(mode=0o600)
        path.write_text(json.dumps(manifest))
        az('containerapp', 'create', '--name', args.app_name,
           '--resource-group', args.resource_group, '--yaml', str(path))
    fqdn = az('containerapp', 'show', '--name', args.app_name,
              '--resource-group', args.resource_group)['properties']['configuration']['ingress']['fqdn']
    print(f'Backend URL: https://{fqdn}\nHealth URL: https://{fqdn}/health')


if __name__ == '__main__':
    main()
