import os
import json
import requests
import yaml
import sys
import base64
from typing import Any, Optional

# Configuration
REPO = os.environ.get('GITHUB_REPOSITORY')
TOKEN = os.environ.get('GITHUB_TOKEN')
HEADERS = {'Accept': 'application/vnd.github.v3+json'}
if TOKEN:
    HEADERS['Authorization'] = f'token {TOKEN}'
MANIFEST_FILE = 'manifest.json'
REQUEST_TIMEOUT = 20
JELLYFIN_ZIP_CANDIDATES = (
    "Jellyfin.Plugin.AnimeThemesSync.zip",
    "AnimeThemesSync.zip",
)

def fetch_json(url: str, headers: Optional[dict[str, str]] = None) -> Any:
    response = requests.get(url, headers=headers, timeout=REQUEST_TIMEOUT)
    response.raise_for_status()
    return response.json()

def fetch_text(url: str, headers: Optional[dict[str, str]] = None) -> str:
    response = requests.get(url, headers=headers, timeout=REQUEST_TIMEOUT)
    response.raise_for_status()
    return response.text

def get_file_content(path, ref):
    """Fetch file content from GitHub API for a specific ref (tag/branch)"""
    url = f"https://api.github.com/repos/{REPO}/contents/{path}?ref={ref}"
    try:
        resp = requests.get(url, headers=HEADERS, timeout=REQUEST_TIMEOUT)
        if resp.status_code == 404:
            print(f"File not found: {path} at {ref}")
            return None
        resp.raise_for_status()
        content = resp.json().get('content', '')
        return base64.b64decode(content).decode('utf-8')
    except Exception as e:
        print(f"Error fetching {path} from {ref}: {e}")
        return None

def main():
    print(f"Generating manifest for {REPO}...")

    if not REPO:
        print("Error: GITHUB_REPOSITORY must be set.")
        sys.exit(1)

    # 1. Get all releases
    releases_url = f"https://api.github.com/repos/{REPO}/releases"
    try:
        releases = fetch_json(releases_url, HEADERS)
    except Exception as e:
        print(f"Error fetching releases: {e}")
        sys.exit(1)

    # 2. Get global metadata from main branch (or current context)
    print("Fetching build.yaml from main branch for global metadata...")
    global_yaml_content = get_file_content('build.yaml', 'main')

    # Fallback to local file if API fails (e.g. running locally or branch name mismatch)
    if not global_yaml_content:
        print("Fallback: Reading local build.yaml")
        try:
            with open('build.yaml', 'r', encoding='utf-8') as f:
                global_yaml_content = f.read()
        except FileNotFoundError:
            print("Error: Could not find build.yaml")
            sys.exit(1)

    global_config = yaml.safe_load(global_yaml_content)

    manifest = {
        "guid": global_config.get('guid'),
        "name": global_config.get('name'),
        "description": global_config.get('description'),
        "overview": global_config.get('overview'),
        "owner": global_config.get('owner'),
        "category": global_config.get('category'),
        "imageUrl": global_config.get('imageUrl'), # Ensure imageUrl is included if present
        "versions": []
    }

    # Clean up None values
    manifest = {k: v for k, v in manifest.items() if v is not None}
    manifest["versions"] = []

    for release in releases:
        if release.get('draft') or release.get('prerelease'):
            continue

        tag = release['tag_name']
        version = tag.lstrip('v') # Remove 'v' prefix if present
        print(f"Processing release {tag} (version {version})...")

        # Fetch per-release metadata from build.yaml at that tag
        build_yaml_content = get_file_content('build.yaml', tag)
        if not build_yaml_content:
            print(f"  Warning: build.yaml not found for tag {tag}. Skipping.")
            continue

        build_config = yaml.safe_load(build_yaml_content)

        # Find assets (prefer Jellyfin package explicitly)
        assets = release.get('assets', [])
        zip_asset = next(
            (a for a in assets if a['name'] in JELLYFIN_ZIP_CANDIDATES),
            None
        )

        if not zip_asset:
            zip_asset = next(
                (
                    a for a in assets
                    if a['name'].endswith('.zip') and not a['name'].startswith('Emby.')
                ),
                None
            )

        if not zip_asset:
            print(f"  Skipping {tag}: No Jellyfin .zip asset found.")
            continue

        # Checksum logic
        checksum = "00000000000000000000000000000000" # Default 32 Zeros
        preferred_md5_name = f"{zip_asset['name']}.md5"
        md5_asset = next((a for a in assets if a['name'] == preferred_md5_name), None)

        if not md5_asset:
            md5_asset = next(
                (
                    a for a in assets
                    if a['name'].endswith('.md5') and not a['name'].startswith('Emby.')
                ),
                None
            )

        if md5_asset:
             print(f"  Found MD5 asset: {md5_asset['name']}")
             try:
                 # Download and read MD5
                 content = fetch_text(md5_asset['browser_download_url']).strip()
                 if content:
                     checksum = content.split()[0]
             except Exception as e:
                 print(f"  Failed to read MD5 asset: {e}")
        else:
            print(f"  Warning: No .md5 asset found for {tag}.")

        version_entry = {
            "version": version,
            "changelog": release.get('body', ''),
            "targetAbi": build_config.get('targetAbi'),
            "sourceUrl": zip_asset['browser_download_url'],
            "checksum": checksum,
            "timestamp": release.get('published_at')
        }
        manifest["versions"].append(version_entry)

    manifest["versions"].sort(
        key=lambda v: v.get("timestamp") or "",
        reverse=True,
    )

    # Wrap in list as Jellyfin expects a list of plugins
    output = [manifest]

    with open(MANIFEST_FILE, 'w', encoding='utf-8') as f:
        json.dump(output, f, indent=4)
    print(f"Successfully generated {MANIFEST_FILE} with {len(manifest['versions'])} versions.")

if __name__ == "__main__":
    main()
