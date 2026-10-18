#!/usr/bin/env python3
"""Sign Windows installers and their feeds locally; never publish the private key."""
import argparse
import base64
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--key', type=Path, default=Path.home() / '.config/typst-studio/updates/windows-ed25519.pem')
parser.add_argument('--output', type=Path, default=root / 'dist')
parser.add_argument('--artifacts', type=Path, default=root / 'Windows/artifacts/installers')
args = parser.parse_args()
openssl = os.environ.get('OPENSSL_BIN') or shutil.which('openssl')
if not openssl or not args.key.is_file():
    raise SystemExit('OpenSSL 3 and the local Windows signing key are required.')
public = subprocess.check_output([openssl, 'pkey', '-in', str(args.key), '-pubout', '-outform', 'DER'])[-32:]
expected = (root / 'Windows/UpdatePublicKey.txt').read_text().strip()
if base64.b64encode(public).decode() != expected:
    raise SystemExit('Signing key does not match the application public key.')
version = ET.parse(root / 'Windows/Directory.Build.props').findtext('.//Version')
if not version or len(version.split('.')) != 3 or not all(p.isdigit() for p in version.split('.')):
    raise SystemExit('Expected stable MAJOR.MINOR.PATCH version.')
args.output.mkdir(parents=True, exist_ok=True)
ns = 'http://www.andymatuschak.org/xml-namespaces/sparkle'
ET.register_namespace('sparkle', ns)
with tempfile.TemporaryDirectory(prefix='typst-windows-sign-') as temp:
    def sign(path):
        signature = Path(temp) / 'signature'
        subprocess.run([openssl, 'pkeyutl', '-sign', '-inkey', str(args.key), '-rawin', '-in', str(path), '-out', str(signature)], check=True)
        return base64.b64encode(signature.read_bytes()).decode()
    for architecture in ['arm64', 'x64']:
        installer = args.artifacts / f'Typst-Studio-{version}-Windows-{architecture}-Setup.exe'
        if not installer.is_file(): raise SystemExit(f'Missing {installer.name}')
        rss = ET.Element('rss', {'version': '2.0'})
        channel = ET.SubElement(rss, 'channel')
        ET.SubElement(channel, 'title').text = f'Typst Studio Windows {architecture}'
        item = ET.SubElement(channel, 'item')
        ET.SubElement(item, 'title').text = f'Typst Studio {version}'
        ET.SubElement(item, 'enclosure', {
            'url': f'https://github.com/liaoweiyang2017/typst-studio/releases/download/v{version}/{installer.name}',
            f'{{{ns}}}version': version, f'{{{ns}}}os': 'windows',
            'length': str(installer.stat().st_size), 'type': 'application/octet-stream',
            f'{{{ns}}}signature': sign(installer),
        })
        feed = args.output / f'appcast-windows-{architecture}.xml'
        feed.write_bytes(ET.tostring(rss, encoding='utf-8', xml_declaration=True))
        Path(str(feed) + '.signature').write_text(sign(feed) + '\n')
        print(f'Signed {installer.name} and {feed.name}')
