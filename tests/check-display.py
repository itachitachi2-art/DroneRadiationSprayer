#!/usr/bin/env python3
"""Display-only checks; does not launch the game or execute its DLLs."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--game-config', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
checks = []

def check(ok, name):
    assert ok, name
    checks.append(name)
    print('PASS: ' + name)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

check((root / 'Config/Localization.csv').is_file(), 'CSV filename matches v3.3 loader')
check(not (root / 'Config/Localization.txt').exists(), 'Obsolete TXT is absent')
with (a.game_config / 'Localization.csv').open(encoding='utf-8-sig', newline='') as f:
    native_header = next(csv.reader(f))
with (root / 'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as f:
    lines = list(csv.reader(f))
check(lines[0] == native_header, 'Exact native 20-column header')
check(len(lines[0]) == 20 and 'KeepLoaded' in lines[0], 'KeepLoaded metadata column present')
check(all(len(row) == len(lines[0]) for row in lines), 'All rows have complete column alignment')
rows = {row[0]: dict(zip(lines[0], row)) for row in lines[1:]}
check(set(rows) == {'modDroneRadiationSprayer', 'modDroneRadiationSprayerDesc'}, 'Exact name and description keys')
check(rows['modDroneRadiationSprayer']['english'] == 'Drone Radiation Sprayer', 'English display name')
check(rows['modDroneRadiationSprayer']['japanese'] == 'ドローン用放射能除去散布装置', 'Japanese display name')
check(all(rows['modDroneRadiationSprayerDesc'][language] for language in ('english', 'japanese')), 'Both descriptions present')
# The verified native patch loader maps by case-insensitive header name.
column_map = [next(i for i, name in enumerate(native_header) if name.lower() == column.lower()) for column in lines[0]]
for language in ('english', 'japanese'):
    merged = [''] * len(native_header)
    for source, target in enumerate(column_map):
        merged[target] = lines[1][source]
    check(merged[native_header.index(language)] == rows['modDroneRadiationSprayer'][language], language + ' survives native-style column mapping')
item = ET.parse(root / 'Config/item_modifiers.xml').getroot().find('./append/item_modifier')
props = {x.get('name'): x.get('value') for x in item.findall('property')}
check(item.get('name') == 'modDroneRadiationSprayer', 'Internal item identity preserved')
check(props['DescriptionKey'] == 'modDroneRadiationSprayerDesc', 'DescriptionKey preserved')
check('LocalizationKey' not in props, 'No unused LocalizationKey override')
check(props['CustomIcon'] == 'modDroneRadiationSprayer', 'CustomIcon matches own sprite name')
icon_path = root / 'UIAtlases/ItemIconAtlas/modDroneRadiationSprayer.png'
with Image.open(icon_path) as im:
    check(im.format == 'PNG' and im.mode == 'RGBA' and im.size == (160, 160), 'PNG RGBA 160x160')
    check(im.getchannel('A').getextrema() == (0, 255), 'Real transparency and opaque content')
    check(all(im.getpixel(xy)[3] == 0 for xy in ((0, 0), (159, 0), (0, 159), (159, 159))), 'Transparent padded corners')
expected = {
    'DroneRadiationSprayer.dll': '4d2da710330e7d2c3fc7784127713a2f5da9da4367df26297e4caa0f02a55f9f',
    'Scripts/DroneRadiationSprayerMod.cs': 'd7751450353e576e9b4fb71cb2891a671c0de53dfd611bf730029dd5d58095fe',
    'Scripts/SprayerController.cs': '0c93a0e5753eb4141748b7e52a03095ac9760c888e796ccc2a0c6151b0227ac2'
}
for path, sha in expected.items():
    check(digest(root / path) == sha, 'Accepted core bytes unchanged: ' + path)
report = {
    'revision': '0.1.0.1-display-fix1', 'core_dll_version': '0.1.0.1',
    'game_executed': False, 'recompiled': False, 'core_tests_rerun': False,
    'checks': checks, 'passed': len(checks), 'failed': 0,
    'sha256': {path: digest(root / path) for path in list(expected) + [
        'Config/item_modifiers.xml', 'Config/Localization.csv',
        'UIAtlases/ItemIconAtlas/modDroneRadiationSprayer.png']}
}
a.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
print('PASS: %d display-only checks' % len(checks))
