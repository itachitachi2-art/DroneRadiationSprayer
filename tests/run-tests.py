#!/usr/bin/env python3
"""Supplemental Linux/Mono checks. No Unity/game execution or Windows claim.

Pass --mono, --mcs, --managed, --harmony, --config, and --output explicitly.
The same production sources are compiled against real v3.3 references, then
against controlled doubles with real Harmony for the behavioral harness.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser(description=__doc__)
for name in ('mono', 'mcs', 'managed', 'harmony', 'config', 'output'):
    p.add_argument('--' + name, type=Path, required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
out = a.output.resolve()
out.mkdir(parents=True, exist_ok=True)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

report = {'version': ET.parse(root / 'ModInfo.xml').getroot().find('Version').get('value'),
          'game_executed': False, 'windows_framework_csc_executed': False,
          'real_game_harmony_patchall_executed': False, 'checks': []}

def save():
    (out / 'validation.json').write_text(json.dumps(report, indent=2) + '\n')

def run(name, command, env=None):
    result = subprocess.run(list(map(str, command)), cwd=root, env=env, capture_output=True, text=True)
    output = result.stdout + result.stderr
    (out / (name + '.log')).write_text(output)
    print(output, end='')
    report['checks'].append({'name': name, 'exit_code': result.returncode,
                             'status': 'PASS' if result.returncode == 0 else 'FAIL'})
    save()
    if result.returncode:
        raise SystemExit(result.returncode)
    return output

run('config-contract', [sys.executable, root / 'tests/check-config.py', '--config', a.config])
ps1 = (root / 'build.ps1').read_text(encoding='utf-8-sig')
required = re.findall(r"'([^']+\.dll)'", re.search(r'\$required = @\((.*?)\n\)', ps1, re.S)[1])
optional = re.findall(r"'([^']+\.dll)'", re.search(r'foreach \(\$optional in @\((.*?)\n\)\)', ps1, re.S)[1])
assert '/nostdlib+' in ps1 and "& $csc '/noconfig' ('@' + $rsp)" in ps1
assert 'System.Text.UTF8Encoding($false)' in ps1
assert 'No fallback was used.' in ps1 and 'return $explicitFull' in ps1
refs = [a.managed.resolve() / x for x in required]
refs += [a.managed.resolve() / x for x in optional if (a.managed / x).is_file()]
refs.append(a.harmony.resolve())
assert all(x.is_file() for x in refs), 'Missing actual game references'
sources = sorted((root / 'Scripts').glob('*.cs'))
report['source_sha256'] = {str(x.relative_to(root)): digest(x) for x in sources}
report['references_sha256'] = {x.name: digest(x) for x in refs}
report['config_sha256'] = {x.name: digest(x) for x in sorted((root / 'Config').iterdir())}
report['build_scripts_sha256'] = {x: digest(root / x) for x in ('build.cmd', 'build.ps1')}
report['actual_reference_list_matches_windows_script'] = True
dll = out / 'DroneRadiationSprayer.dll'
rsp = out / 'actual-v33.rsp'
rsp.write_text('\n'.join(['/nologo', '/nostdlib+', '/target:library', '/optimize+', '/debug-', '/langversion:5',
                         '/out:"' + str(dll) + '"'] + ['/reference:"' + str(x) + '"' for x in refs] +
                        ['"' + str(x) + '"' for x in sources]) + '\n', encoding='utf-8')
if dll.exists():
    dll.unlink()
run('actual-v33-build', [a.mcs, '/noconfig', '@' + str(rsp)])
assert dll.is_file()
report['dll_sha256'] = digest(dll)
exe = out / 'sprayer-tests.exe'
run('controlled-compile', [a.mcs, '-noconfig', '-langversion:5', '-r:System', '-r:System.Core', '-r:' + str(a.harmony.resolve()),
                           '-out:' + str(exe)] + sources + [root / 'tests/sprayer-harness.cs'])
env = dict(os.environ, MONO_PATH=str(a.harmony.resolve().parent))
controlled = run('controlled-harmony', [a.mono, exe], env)
report['controlled_output'] = controlled.splitlines()
report['status'] = 'PASS_ACTUAL_REFERENCE_BUILD_AND_CONTROLLED_TESTS'
report['limitations'] = [
    'Behavioral checks use controlled game/Unity doubles, not actual game execution.',
    'Actual game Harmony patching, display, sleep/aggro behavior, multiplayer and F10 need in-game validation.',
    'Performance checks verify scan/buff counts, not real server FPS or timing under load.',
    'Windows Framework csc and the PowerShell build script were not executed.'
]
save()
print(report['status'])
