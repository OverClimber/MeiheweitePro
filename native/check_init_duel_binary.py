"""Reject the unconditional trap observed in the shipped InitDuel function."""
import argparse
import re
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('binary', type=Path)
parser.add_argument('--objdump', default='llvm-objdump')
args = parser.parse_args()
result = subprocess.run([args.objdump, '-d', str(args.binary)], check=True,
                        capture_output=True, text=True)
match = re.search(r'^[^\n]*_+ZN3ygo9NetServer8InitDuelEv>?:\n(.*?)(?=\n\n|\Z)',
                  result.stdout, re.M | re.S)
if not match:
    raise SystemExit('InitDuel disassembly missing')
body = match.group(1)
if re.search(r'\bbrk\b', body) or not re.search(r'\bret\b', body):
    raise SystemExit('InitDuel contains a trap or has no normal return')
print('PASS InitDuel has a normal return and no BRK instruction')
