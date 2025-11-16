"""
Small helper script to inspect Nexus Mods API responses,
in particular the category IDs for Baldur's Gate 3.

Usage (in a regular terminal with internet access):

    set NEXUS_API_KEY=your_api_key_here   # Windows
    # or: export NEXUS_API_KEY=...       # Linux/macOS

    python tools/test_api.py

This will print all categories for the BG3 domain (`baldursgate3`).
"""

import os
import sys
from typing import Any

import requests


BASE_URL = "https://api.nexusmods.com/v1"
BG3_DOMAIN = "baldursgate3"


def get_api_key() -> str:
    key = os.getenv("NEXUS_API_KEY")
    if not key:
        print("NEXUS_API_KEY environment variable is not set.", file=sys.stderr)
        sys.exit(1)
    return key


def fetch_bg3_categories(api_key: str) -> list[dict[str, Any]]:
    """
    Try the categories endpoint first; if that returns 4xx/422 or
    an unexpected shape, fall back to the game details endpoint.
    """
    headers = {
        "apikey": api_key,
        "Accept": "application/json",
    }

    # 1) Try the documented categories endpoint
    cat_url = f"{BASE_URL}/games/{BG3_DOMAIN}/mods/categories.json"
    resp = requests.get(cat_url, headers=headers, timeout=30)

    if resp.ok:
        data = resp.json()
        if isinstance(data, list):
            return data
        else:
            print(
                f"[WARN] /mods/categories.json returned non-list payload, "
                f"falling back to /games/{{domain}}.json. Top-level type: {type(data)}",
                file=sys.stderr,
            )
    else:
        print(
            f"[WARN] Categories endpoint failed with {resp.status_code}: "
            f"{resp.text}",
            file=sys.stderr,
        )

    # 2) Fallback: game details. Many Nexus responses expose categories here.
    game_url = f"{BASE_URL}/games/{BG3_DOMAIN}.json"
    resp2 = requests.get(game_url, headers=headers, timeout=30)
    resp2.raise_for_status()
    game_data = resp2.json()

    # Look for a list-valued field that clearly contains categories.
    for key in ("categories", "mod_categories"):
        value = game_data.get(key)
        if isinstance(value, list):
            print(f"[INFO] Using '{key}' from /games endpoint as categories.", file=sys.stderr)
            return value

    print(
        "[ERROR] Could not find a 'categories' or 'mod_categories' list in "
        f"/games/{BG3_DOMAIN}.json response. Full top-level keys: {list(game_data.keys())}",
        file=sys.stderr,
    )
    return []


def main() -> None:
    api_key = get_api_key()
    categories = fetch_bg3_categories(api_key)

    if not categories:
        print("No categories found.", file=sys.stderr)
        return

    print(f"Found {len(categories)} categories for '{BG3_DOMAIN}':")
    print("category_id\tname\tparent")
    for cat in sorted(categories, key=lambda c: c.get("category_id", 0)):
        cid = cat.get("category_id", cat.get("id"))
        name = cat.get("name")
        parent = (
            cat.get("parent_category")
            or cat.get("parent_category_id")
            or cat.get("parent")
        )
        print(f"{cid}\t{name}\t{parent}")


if __name__ == "__main__":
    main()
