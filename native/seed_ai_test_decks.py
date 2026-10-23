"""Seed first-install player decks from the already bundled standard AI decks."""
import argparse, zipfile
from pathlib import Path

def seed(archive):
    mapping={
        'WindBot/Decks/AI_MokeyMokey.ydk':'deck/iOS_AI_Test.ydk',
        'rd/ai/WindBot/Decks/AI_Test.ydk':'deck_rd/iOS_RD_AI_Test.ydk',
    }
    with zipfile.ZipFile(archive,'a',zipfile.ZIP_DEFLATED) as data:
        for source,target in mapping.items():
            if target in data.namelist(): raise ValueError('test deck already seeded')
            content=data.read(source)
            if b'#main' not in content or b'!side' not in content:
                raise ValueError('invalid bundled deck: '+source)
            data.writestr(target,content)
            print('Seeded',target,'from bundled standard deck')
    with zipfile.ZipFile(archive) as data:
        if data.testzip(): raise ValueError('resource CRC failure')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('archive',type=Path)
    seed(p.parse_args().archive)
