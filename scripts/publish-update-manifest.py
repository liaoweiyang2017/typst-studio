#!/usr/bin/env python3
"""Validate stable release assets, then attach the public update manifest."""
import base64
import xml.etree.ElementTree as ET
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile


def gh(*args):
    return subprocess.check_output(["gh", *args], text=True)


def main():
    tag = sys.argv[1]
    if not re.fullmatch(r"v\d{1,6}\.\d{1,6}\.\d{1,6}", tag):
        raise ValueError("Only stable vMAJOR.MINOR.PATCH tags are supported")
    repo = os.environ["GH_REPO"]
    if repo != "liaoweiyang2017/typst-studio":
        raise ValueError("Unexpected distribution repository")
    version = tag[1:]
    release = json.loads(gh("api", f"repos/{repo}/releases/tags/{tag}"))
    if release["draft"] or release["prerelease"] or release["tag_name"] != tag:
        raise ValueError("A published stable release is required")
    dmg = f"Typst-Studio-{version}-macOS-arm64.dmg"
    names = {asset["name"] for asset in release["assets"] if asset["state"] == "uploaded"}
    required = [dmg, dmg + ".sha256", "release-info.json"]
    signed_update = tuple(map(int, version.split("."))) >= (0, 1, 7)
    if signed_update:
        required.append("appcast.xml")
    if not set(required).issubset(names):
        raise ValueError("Upload DMG, checksum and release-info.json before publishing")
    with tempfile.TemporaryDirectory(prefix="typst-release-") as temp:
        folder = Path(temp)
        args = ["release", "download", tag, "--repo", repo, "--dir", temp]
        for name in required:
            args += ["--pattern", name]
        gh(*args)
        expected = (folder / (dmg + ".sha256")).read_text().split()[0]
        if not re.fullmatch(r"[a-f0-9]{64}", expected):
            raise ValueError("Invalid checksum file")
        digest = hashlib.sha256()
        with (folder / dmg).open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest() != expected:
            raise ValueError("DMG checksum mismatch")
        info = json.loads((folder / "release-info.json").read_text())
        if info.get("version") != version or info.get("architecture") != "arm64":
            raise ValueError("Release metadata does not match the tag/architecture")
        minimum = info.get("minimum_macos", "")
        if not re.fullmatch(r"\d{1,3}\.\d{1,3}(\.\d{1,3})?", minimum):
            raise ValueError("Invalid macOS requirement")
        if signed_update:
            # Publishing a feed never requires the private signing key.
            # Verify the developer signature with the public key pinned in the app.
            appcast = folder / "appcast.xml"
            if appcast.stat().st_size > 65536:
                raise ValueError("Oversized appcast")
            ns = "{http://www.andymatuschak.org/xml-namespaces/sparkle}"
            items = ET.parse(appcast).findall("./channel/item")
            if len(items) != 1:
                raise ValueError("Exactly one release is expected")
            item = items[0]
            enclosure = item.find("enclosure")
            expected_url = f"https://github.com/{repo}/releases/download/{tag}/{dmg}"
            if enclosure is None or enclosure.get("url") != expected_url or int(enclosure.get("length", "0")) != (folder / dmg).stat().st_size:
                raise ValueError("Appcast artifact mismatch")
            if item.findtext(ns + "shortVersionString") != version or item.findtext(ns + "version") != str(info.get("build")):
                raise ValueError("Appcast version mismatch")
            if item.findtext(ns + "minimumSystemVersion") != minimum:
                raise ValueError("Appcast system requirement mismatch")
            if item.findtext(ns + "hardwareRequirements") != "arm64":
                raise ValueError("Appcast architecture mismatch")
            signature = base64.b64decode(enclosure.get(ns + "edSignature", ""), validate=True)
            if len(signature) != 64:
                raise ValueError("Invalid update signature")
            key = base64.b64decode("5GT6r4MeI9a9lD55kJvNFIDvDQhG5rboEM4XJA/BUfo=")
            (folder / "public.der").write_bytes(bytes.fromhex("302a300506032b6570032100") + key)
            (folder / "signature").write_bytes(signature)
            subprocess.run(["openssl", "pkeyutl", "-verify", "-pubin", "-keyform", "DER", "-inkey", str(folder / "public.der"), "-rawin", "-in", str(folder / dmg), "-sigfile", str(folder / "signature")], check=True)
        manifest = {
            "version": version,
            "minimum_macos": minimum,
            "architecture": "arm64",
            "download_url": f"https://github.com/{repo}/releases/download/{tag}/{dmg}",
            "release_url": f"https://github.com/{repo}/releases/tag/{tag}",
            "sha256": expected,
        }
        output = folder / "update.json"
        output.write_text(json.dumps(manifest, indent=2) + "\n")
        gh("release", "upload", tag, str(output), "--repo", repo, "--clobber")
        print(f"Published verified update manifest for {tag}")


if __name__ == "__main__":
    main()
