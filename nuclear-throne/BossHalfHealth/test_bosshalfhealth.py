"""Simple static regression checks; game runtime verification is still needed."""
from pathlib import Path
import re

src = Path(__file__).with_name('bosshalfhealth.mod.gml').read_text()
assert src.count('#define step') == 1
assert src.count('#define bhh_apply_once') == 1
assert '#macro BHH_HEALTH_FACTOR 0.5' in src
assert re.search(r'if \(variable_instance_exists\(id, "bhh_health_adjusted"\)\) return;', src)
assert 'bhh_health_adjusted = true;' in src
assert '_old_max * BHH_HEALTH_FACTOR' in src
assert '_old_hp * BHH_HEALTH_FACTOR' in src
assert 'maxhealth = _new_max;' in src
assert 'my_health = _new_hp;' in src
expected = {'BanditBoss', 'ScrapBoss', 'LilHunter', 'Nothing', 'Nothing2',
            'FrogQueen', 'HyperCrystal', 'TechnoMancer', 'Last', 'OasisBoss',
            'CrownGuardian'}
actual = set(re.findall(r'^with \((\w+)\)\s+bhh_apply_once\(\);', src, re.M))
assert actual == expected, (actual, expected)
assert 'with (enemy)' not in src
assert 'with (Player)' not in src
print('PASS: Boss Half Health static regression checks; 11 selected boss objects.')
