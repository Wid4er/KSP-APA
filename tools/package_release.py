#!/usr/bin/env python3
"""Package only committed public install files."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
from zipfile import ZipFile, ZIP_DEFLATED, ZipInfo


def git(*args):
    return subprocess.check_output(["git", *args])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="dist")
    args = parser.parse_args()
    os.chdir(Path(__file__).resolve().parents[1])
    metadata = json.loads(git("show", "HEAD:APA/AirParkStability.version"))
    numbers = [metadata["VERSION"][k] for k in ("MAJOR", "MINOR", "PATCH", "BUILD")]
    if not all(type(n) is int and n >= 0 for n in numbers):
        raise ValueError("Version must contain nonnegative integer components")
    version = ".".join(map(str, numbers if numbers[3] else numbers[:3]))
    expected = {
        "APA/AirParkStability.version", "APA/Settings.cfg", "APA/Plugins/AirParkStability.dll",
        "README.md", "docs/BUILD.md", "docs/TESTING.md",
    }
    payload = git("archive", "--format=tar", "HEAD", "APA", "README.md", "docs/BUILD.md", "docs/TESTING.md")
    entries = {}
    with tarfile.open(fileobj=io.BytesIO(payload)) as tar:
        for entry in tar:
            if entry.isdir():
                continue
            if not entry.isfile() or entry.name not in expected:
                raise ValueError("Unexpected package entry: " + entry.name)
            target = "GameData/" + entry.name if entry.name.startswith("APA/") else entry.name
            entries[target] = tar.extractfile(entry).read()
    if len(entries) != len(expected):
        raise ValueError("Missing expected package files")
    disclosure = b"> **AI involvement disclosure:** ChatGPT-6.1 was involved in the development of this project."
    if entries["README.md"].splitlines()[0] != disclosure:
        raise ValueError("README must start with the requested disclosure")
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    archive = out / ("AirParkStability-" + version + ".zip")
    with ZipFile(archive, "w", ZIP_DEFLATED) as package:
        for name, data in sorted(entries.items()):
            info = ZipInfo(name, date_time=(2020, 1, 1, 0, 0, 0))
            info.compress_type = ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            package.writestr(info, data)
    with ZipFile(archive) as package:
        if package.testzip() is not None or set(package.namelist()) != set(entries):
            raise ValueError("Invalid generated archive")
        for name, data in entries.items():
            if package.read(name) != data:
                raise ValueError("Archive content mismatch: " + name)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    checksum = archive.with_suffix(".zip.sha256")
    checksum.write_text(digest + "  " + archive.name + "\n", encoding="utf-8")
    print("Version: " + version)
    print("Archive: " + str(archive))
    print("SHA256: " + digest)
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as outputs:
            outputs.write("version=" + version + "\n")
            outputs.write("tag=v" + version + "\n")
            outputs.write("archive=" + str(archive) + "\n")
            outputs.write("checksum=" + str(checksum) + "\n")


if __name__ == "__main__":
    main()
