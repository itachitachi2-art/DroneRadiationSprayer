#!/usr/bin/env python3
"""Static v3.3 contract audit. Does not execute game or prove sound precedence."""
import argparse,csv,json,hashlib
from pathlib import Path
import xml.etree.ElementTree as E
p=argparse.ArgumentParser();p.add_argument('--config',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
r=Path(__file__).resolve().parents[1];checks=[]
def check(ok,label):
 assert ok,label
 checks.append(label);print('PASS:',label)
cat=json.loads((r/'docs/gun-sound/catalog.json').read_text());patch=E.parse(r/'Config/item_modifiers.xml').getroot();mods=patch.findall('./append/item_modifier');vanilla=E.parse(a.config/'item_modifiers.xml').getroot();sounds=E.parse(a.config/'sounds.xml').getroot();soundmap={n.get('name'):n for n in sounds};items=E.parse(a.config/'items.xml').getroot()
check(len(cat)==24 and len(mods)==25,'24 gun sounds plus original drone')
check(len({c['id'] for c in cat})==24,'Unique IDs')
check(len([c for c in cat if not c['suppressed']])==17,'17 normal gun sound options')
check(len([c for c in cat if c['suppressed']])==7,'7 deduplicated suppressed clip-set options')
new={n.get('name'):n for n in mods[1:]};old={n.get('name'):n for n in vanilla}
check(not set(new)&set(old),'No native name collisions')
check(not any('droneGunSound' in n.get('modifier_tags','').split(',') or 'droneGunSound' in n.get('blocked_tags','').split(',') for n in vanilla),'New category has no native exclusion collision')
check(patch.tag=='configs' and len(patch)==1 and patch[0].tag=='append' and patch[0].get('xpath')=='/item_modifiers','Only additive modifier patch; no vanilla suppressor mutation')
for c in cat:
 n=new[c['id']];props={x.get('name'):x.get('value') for x in n.findall('property')};ov=n.findall('item_property_overrides')
 check(n.attrib=={'name':c['id'],'installable_tags':'gun','modifier_tags':'droneGunSound','blocked_tags':'noMods','type':'attachment'},c['id']+': gun-only, same family, normal noMods guard')
 check(props=={'Extends':'modGeneralMaster','CustomIcon':'modGunSoundSuppressorSilencer','DescriptionKey':c['id']+'Desc','CreativeMode':'Player','ShowQuality':'false','EconomicValue':'0'},c['id']+': suppressor icon, no acquisition changes')
 check(not n.findall('.//effect_group') and len(ov)==1 and ov[0].get('name')=='*',c['id']+': no direct stat bonuses/effects')
 v={x.get('name'):x.get('value') for x in ov[0]}
 check(v=={'Sound_start':c['sound'],'Sound_loop':c['sound'],'Sound_repeat':'','Sound_end':c['end']},c['id']+': sound-only property overrides')
 check(all(not s or s in soundmap for s in v.values()),c['id']+': every nonempty sound resolves')
check(len({tuple(x.get('ClipName') for x in soundmap[c['sound']].findall('AudioClip') if x.get('AltSound')!='true') for c in cat})==24,'24 distinct primary clip sets')
guns=[]
for i in items:
 t=i.find("property[@name='Tags']");tags=set(t.get('value','').split(',')) if t is not None else set()
 if 'gun' in tags and 'noMods' not in tags:guns.append(i.get('name'))
check(len(guns)==17 and {c['source_item'] for c in cat if not c['suppressed']}==set(guns),'Covers all 17 native non-admin gun-tagged weapons')
with (r/'Config/Localization.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.reader(f))
check(all(len(row)==20 for row in rows),'All localization rows align to native 20 columns')
keys=[row[0] for row in rows[1:]];check(len(keys)==50 and len(set(keys))==50,'50 unique localized name/description keys')
check(set(keys)=={n.get('name')+s for n in mods for s in ('','Desc')},'Localization matches every part')
check(all(row[6] and row[12] for row in rows[1:]),'Every part has English and Japanese text')
check(hashlib.sha256((r/'DroneRadiationSprayer.dll').read_bytes()).hexdigest()=='4d2da710330e7d2c3fc7784127713a2f5da9da4367df26297e4caa0f02a55f9f','Adopted user-tested DLL unchanged')
report={'revision':'gun-sound1-candidate','passed':len(checks),'failed':0,'game_executed':False,'suppressor_sound_precedence_verified':False,'checks':checks,'compatible_native_guns':guns,'limitations':['Static tags establish no added native modifier-category conflict. Runtime installation UI was not executed.','Both suppressor and sound parts override firing sounds; precedence remains unverified.','No fresh core compilation or Harmony harness execution; existing DLL and sources preserved.']}
a.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print('PASS:',len(checks),'static checks')
