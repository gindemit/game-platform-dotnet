#!/usr/bin/env python3
"""Stage only the inspected optional config DLL; never build, publish or edit games."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil

ASSEMBLY = 'GamePlatform.Configuration'
GUID = '8775d311ce6a49d3a3cd583ead52be55'
META = '''fileFormatVersion: 2
guid: 8775d311ce6a49d3a3cd583ead52be55
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        OS: AnyOS
  - first:
      Android: Android
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
  - first:
      WebGL: WebGL
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
'''


def stage(assembly, metadata, output):
    assembly, metadata, output = Path(assembly).resolve(), Path(metadata).resolve(), Path(output).resolve()
    proof = json.loads(metadata.read_text(encoding='utf-8'))
    if not isinstance(proof, dict) or proof.get('assemblySha256') != hashlib.sha256(assembly.read_bytes()).hexdigest():
        raise ValueError('metadata_assembly_hash_mismatch')
    items = proof.get('assemblies')
    if (not isinstance(items, list) or len(items) != 1 or items[0].get('name') != ASSEMBLY
            or items[0].get('targetFramework') != '.NETStandard,Version=v2.1'
            or {r['name'] for r in items[0].get('references', [])} != {'netstandard'}):
        raise ValueError('config_only_closure_required')
    if assembly.name != ASSEMBLY + '.dll' or not assembly.is_file():
        raise ValueError('config_assembly_required')
    # Refuse destructive replacement: caller owns a fresh staging directory.
    if output.exists() and any(output.iterdir()):
        raise ValueError('output_must_be_empty')
    plugins = output / 'Runtime/Plugins'
    plugins.mkdir(parents=True, exist_ok=True)
    shutil.copy2(assembly, plugins / assembly.name)
    (plugins / (assembly.name + '.meta')).write_text(META, encoding='utf-8')
    (output / 'package.json').write_text(json.dumps(dict(name='com.gindemit.game-platform.configuration',
        version='0.1.0-local', displayName='Game Platform Configuration (isolated)',
        unity='6000.5', description='Optional local config core; no account or mobile SDK dependency.'), indent=2) + '\n', encoding='utf-8')
    inventory = dict(assembly=ASSEMBLY, bytes=assembly.stat().st_size,
        sha256=hashlib.sha256(assembly.read_bytes()).hexdigest(), target='netstandard2.1',
        runtimeThirdPartyPackages=0, projectReferences=0, transitiveAssemblies=['netstandard'],
        unityPlatforms=['Editor', 'Android', 'WebGL'], metadataSha256=hashlib.sha256(metadata.read_bytes()).hexdigest())
    (output / 'inventory.json').write_text(json.dumps(inventory, indent=2) + '\n', encoding='utf-8')
    return inventory


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--assembly', required=True)
    parser.add_argument('--metadata', required=True, help='Hash-bound {assemblySha256,assemblies} PackageInspector result')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    print(json.dumps(stage(args.assembly, args.metadata, args.output), indent=2))


if __name__ == '__main__':
    main()
