"""Secret-safe VAPID generator regression; ephemeral test keys never leave the temp directory."""
import os
import pathlib
import subprocess
import tempfile

with tempfile.TemporaryDirectory() as directory:
    path = pathlib.Path(directory) / 'web-push.env'
    command = ['dotnet', 'run', '--project', 'tools/DndEconomy.Vapid', '--', '--output', str(path), '--subject', 'https://example.com']
    result = subprocess.run(command, capture_output=True, text=True)
    assert result.returncode == 0, 'Generator failed'
    content = path.read_bytes()
    values = dict(line.split('=', 1) for line in content.decode().splitlines())
    assert values.keys() == {'WEB_PUSH_SUBJECT', 'WEB_PUSH_PUBLIC_KEY', 'WEB_PUSH_PRIVATE_KEY'}
    assert values['WEB_PUSH_PRIVATE_KEY'] not in result.stdout + result.stderr
    if os.name != 'nt':
        assert path.stat().st_mode & 0o777 == 0o600
    result = subprocess.run(command, capture_output=True, text=True)
    assert result.returncode != 0
    assert path.read_bytes() == content
    assert values['WEB_PUSH_PRIVATE_KEY'] not in result.stdout + result.stderr
print('PASS VAPID file is private, output contains no private key, existing pair is never overwritten')
