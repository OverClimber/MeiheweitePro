"""Initialize the server's synthetic create-game packet before reading its strings."""
import argparse
from pathlib import Path


def patch(root):
    path = Path(root) / 'ygoserver' / 'netserver.cpp'
    data = path.read_bytes()
    old = b'CTOS_CreateGame* pkt = new CTOS_CreateGame;'
    new = b'CTOS_CreateGame packet{};\n\tCTOS_CreateGame* pkt = &packet;'
    if data.count(new) == 1 and old not in data:
        return False
    if data.count(old) != 1 or new in data:
        raise ValueError('InitDuel packet allocation missing or ambiguous')
    start = data.index(b'void NetServer::InitDuel()')
    end = data.index(b'\n}', start)
    if not start < data.index(old) < end:
        raise ValueError('packet allocation is outside InitDuel')
    path.write_bytes(data.replace(old, new))
    return True


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    args = parser.parse_args()
    print('InitDuel packet initialized:', patch(args.root))
