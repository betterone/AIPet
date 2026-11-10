#!/usr/bin/env python3
"""Keep the user's local signing choices across a Unity export; no Apple account calls."""
import json
import pathlib
import re
import subprocess
import sys

KEYS = {"DEVELOPMENT_TEAM", "CODE_SIGN_STYLE", "CODE_SIGN_IDENTITY",
        "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "PROVISIONING_PROFILE", "PROVISIONING_PROFILE_SPECIFIER",
        "PRODUCT_BUNDLE_IDENTIFIER"}
ATTRS = {"DevelopmentTeam", "ProvisioningStyle"}

def read(path):
    return json.loads(subprocess.check_output(["plutil", "-convert", "json", "-o", "-", str(path)]))

def targets(project):
    objects = project["objects"]
    attrs = objects[project["rootObject"]].get("attributes", {}).get("TargetAttributes", {})
    for guid, target in objects.items():
        if target.get("isa") != "PBXNativeTarget":
            continue
        configs = {}
        for config in objects[target["buildConfigurationList"]]["buildConfigurations"]:
            obj = objects[config]
            configs[obj["name"]] = (config, {k: v for k, v in obj["buildSettings"].items() if k in KEYS})
        yield target["name"], guid, configs, {k: v for k, v in attrs.get(guid, {}).items() if k in ATTRS}

def snapshot(project):
    return {name: {"configs": {n: values for n, (_, values) in configs.items()}, "attrs": attrs}
            for name, _, configs, attrs in targets(project)}

def replace_fields(body, keys, values, indent):
    for key in keys:
        body = re.sub(r"(?m)^\s*(?:" + re.escape(key) + "|" + re.escape(json.dumps(key)) + r")\s*=.*?;\n", "", body)
    return body + "".join(indent + json.dumps(k) + " = " + json.dumps(v) + ";\n" for k, v in values.items())

mode, project_path, snapshot_path = sys.argv[1:]
path, saved = pathlib.Path(project_path), pathlib.Path(snapshot_path)
if mode == "capture":
    saved.write_text(json.dumps(snapshot(read(path)) if path.exists() else {}, indent=2))
elif mode == "restore":
    settings = json.loads(saved.read_text())
    original = text = path.read_text()
    for name, guid, configs, attrs in targets(read(path)):
        if name not in settings:
            continue
        for config_name, (config_guid, _) in configs.items():
            if config_name not in settings[name]["configs"]:
                continue
            pattern = r"(\t\t" + config_guid + r" /\*[^\n]+\*/ = \{.*?buildSettings = \{\n)(.*?)(\t\t\t\};)"
            values = settings[name]["configs"][config_name]
            text, count = re.subn(pattern, lambda m: m[1] + replace_fields(m[2], KEYS, values, "\t\t\t\t") + m[3], text, count=1, flags=re.S)
            if count != 1:
                raise RuntimeError("Could not preserve signing for " + name + " / " + config_name)
        pattern = r"(^\t\t\t\t\t" + guid + r" = \{\n)(.*?)(^\t\t\t\t\t\};)"
        text, count = re.subn(pattern, lambda m: m[1] + replace_fields(m[2], ATTRS, settings[name]["attrs"], "\t\t\t\t\t\t") + m[3], text, count=1, flags=re.S|re.M)
        if settings[name]["attrs"] and count != 1:
            raise RuntimeError("Could not preserve target signing attributes for " + name)
    path.write_text(text)
    try:
        actual = snapshot(read(path))
        for name, value in settings.items():
            if actual.get(name) != value:
                raise RuntimeError("Signing preservation verification failed for " + name)
    except Exception:
        path.write_text(original)
        raise
    print("Existing local signing settings preserved and verified.")
else:
    raise SystemExit("Expected capture or restore")
