#!/usr/bin/env python3
"""Validate the candidate against the supplied game's Config directory."""
import argparse
import csv
import io
from pathlib import Path
import re
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--config', type=Path, required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
checks = 0

def check(condition, description):
    global checks
    assert condition, description
    checks += 1
    print('PASS: ' + description)

patch = ET.parse(root / 'Config/item_modifiers.xml').getroot()
vanilla = ET.parse(a.config / 'item_modifiers.xml').getroot()
buffs = ET.parse(a.config / 'buffs.xml').getroot()
mod = patch.find('./append/item_modifier')
check(patch.tag == 'configs' and len(patch) == 1, 'Exactly one additive XML patch')
check(patch[0].tag == 'append' and patch[0].get('xpath') == '/item_modifiers', 'Native modifier append target')
check(len(patch[0]) == 25 and mod.get('name') == 'modDroneRadiationSprayer', 'Original drone plus 24 sound attachments')
check(vanilla.find("./item_modifier[@name='modDroneRadiationSprayer']") is None, 'No vanilla item-name collision')
check(mod.get('installable_tags') == 'drone' and mod.get('type') == 'attachment', 'Removable drone-only attachment')
check(not any(x.get('modifier_tags') == mod.get('modifier_tags') for x in vanilla), 'Unique modifier category')
props = {x.get('name'): x.get('value') for x in mod.findall('property')}
check(props.get('Extends') == 'modGeneralMaster', 'Native modifier master inheritance')
check(props.get('CustomIcon') == 'modDroneRadiationSprayer' and (root / 'UIAtlases/ItemIconAtlas/modDroneRadiationSprayer.png').is_file(), 'Own runtime icon exists and matches CustomIcon')
check(props.get('CreativeMode') == 'Player' and props.get('ShowQuality') == 'false', 'Creative candidate, no quality variants')
check(not mod.findall('.//effect_group') and not mod.findall('.//item_property_overrides'), 'No passive-stat or weapon/AI XML changes')
check(not any(x.name in ('buffs.xml', 'entityclasses.xml', 'items.xml', 'recipes.xml', 'loot.xml', 'traders.xml') for x in (root / 'Config').iterdir()), 'No global buff/entity/item or unapproved acquisition patches')
block = buffs.find("./buff[@name='buffRadiatedRegenBlock15']")
check(block is not None and block.find('duration').get('value') == '15', 'Actual vanilla inhibitor duration is 15 seconds')
check(block.find('stack_type').get('value') == 'replace' and not block.findall('effect_group'), 'Native inhibitor replaces, with no damage/AI effect group')
regen = buffs.find("./buff[@name='buffRadiatedRegen']")
check(any(x.get('name') == '!HasBuff' and 'buffRadiatedRegenBlock15' in x.get('buff', '').split(',') for x in regen.findall('.//requirement')), 'Native regeneration explicitly checks the chosen inhibitor')
rows = list(csv.DictReader(io.StringIO((root / 'Config/Localization.csv').read_text(encoding='utf-8-sig'))))
check(len(rows) == 50 and all(None not in x for x in rows), 'Localization CSV is well formed')
check({mod.get('name'), props['DescriptionKey']}.issubset({x['Key'] for x in rows}), 'Item and description localization keys match')
check(all(x['english'] and x['japanese'] for x in rows), 'English and Japanese localization present')
source = '\n'.join(x.read_text() for x in sorted((root / 'Scripts').glob('*.cs')))
check('zombie.Buffs.AddBuff(InhibitorName, -1, true, false, -1f);' in source, 'Explicit native AddBuff arguments avoid shared duration override')
check(not re.search(r'\.Health\s*=|\.DurationMax\s*=|\.SetAttackTarget\(|\.SetRevengeTarget\(|\.DamageEntity\(|\.DamageSelf\(', source), 'No direct health, buff duration, damage or target mutation')
check(source.count('[HarmonyPatch(') == 1 and '[HarmonyPatch(typeof(EntityDrone), "OnUpdateEntity")]' in source, 'Only drone update is patched')
version = ET.parse(root / 'ModInfo.xml').getroot().find('Version').get('value')
check(version == '0.1.0.1' and '[assembly: AssemblyVersion("' + version + '")]' in source and '[assembly: AssemblyFileVersion("' + version + '")]' in source, 'Candidate assembly and ModInfo versions agree')
print('PASS: %d config/source contract checks' % checks)
