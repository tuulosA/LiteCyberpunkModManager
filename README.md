# Helix Mod Manager (HMM)

**Helix Mod Manager (HMM)** is a lightweight, Nexus-integrated mod manager for Cyberpunk 2077 and Baldur's Gate 3. It lets you download, manage, and install your tracked Nexus Mods quickly through the Nexus Mods API.

---

## Features

- Fetch tracked mods after linking your Nexus Mods account via SSO (API key is stored automatically)
- Filter based on category and search mods based on name
- Check available update status
- Download individual mod files (Premium + non-premium Nexus Mods account support)
- Mass install and uninstall with a single click
- Export and import mod lists.
- Users with **premium** Nexus Mods accounts can mass download from imported modlists.

---

## Getting Started

### 1. Set up your settings
<img width="1634" height="969" alt="Screenshot 2025-11-22 114250" src="https://github.com/user-attachments/assets/7ca6e0b3-f64a-4684-9f98-8802ff9fff91" />


- Click **Link Nexus Account** to open the Nexus Mods SSO flow. The app watches the websocket for the API key and stores it automatically (no manual copy/paste).
- Set your **Game Installation Directory** and **Output Directory** if not set correctly by default.
- Here you can **export** and **import** mod lists, as well as **clear** all your tracked mods on Nexus Mods.

---

### 2. Track your desired mods on Nexus Mods
<img width="368" alt="nexus mods example track mods" src="https://github.com/user-attachments/assets/25ace659-b140-4649-a8aa-c7646d0653ff" />

Go to your desired mod's page and hit `Track` to add it to your tracked list.
- `Tracking Centre` button takes you to your tracked mods.
---

### 3. Fetch Tracked Mods


Navigate to the **Mods** tab and click **"Fetch Tracked Mods"** to populate your list.

You can now:
- See **status** of mods (e.g., *Downloaded*, *Update Available!*, etc.)
- **Filter** by mod categories
- **Search** mods by name
<img width="1636" height="971" alt="image" src="https://github.com/user-attachments/assets/3cf3561e-d992-4024-8341-76594cc51be1" />

---

### 4. Download Mod Files

- If you are a **Nexus Premium user**, click **"Download Files"** to fetch files directly

<img width="441" height="408" alt="image" src="https://github.com/user-attachments/assets/89194fbd-33f5-476a-b8b7-d38d3501fc7e" />

- If you're a **non-premium user**, click the orange "Mod manager download" button on Nexus Mods. The app will handle the `.nxm` link and track the download.
- You can **double click** the mod name in the manager to get to the mod's page.

<img width="704" alt="nexus mods example" src="https://github.com/user-attachments/assets/2acb7a29-e40a-4341-82f6-86f2a6a26e5c" />

- All downloaded files will appear in your game’s `Mods` directory:
<img width="641" alt="downloaded mods location" src="https://github.com/user-attachments/assets/24663491-9811-43c6-a508-0011bac81483" />

- Mod files can be deleted with the `Manage Files` button:

<img width="443" height="298" alt="image" src="https://github.com/user-attachments/assets/db0762d4-8022-44c2-8371-6635e6e812d7" />

- Multiple mods can be selected at once with ctrl+click, and then clicking `Manage Files` button lets you delete files for selected mods. Without selection all downloaded files are shown.
<img width="1636" height="971" alt="image" src="https://github.com/user-attachments/assets/8e875566-ea93-4af2-9bcb-3a582d93646b" />

---

#### Mod Status Meaning

<img width="1635" height="970" alt="image" src="https://github.com/user-attachments/assets/9cdfaf14-d446-4db6-b5c9-5dec28ba5137" />



- **Latest Downloaded**: The most recent overall file by timestamp is downloaded.
- **Downloaded**: A file is downloaded, but there are some newer files uploaded by mod author.
- **Update Available!**: A newer file with the same filename exists.
- **Not Downloaded**: No file for this mod yet.

---

### 5. Install Mods

Go to the **Files** tab to:
- **Install** or **Uninstall** mods with one click
- Delete specific downloaded mod files

<img width="1634" height="969" alt="image" src="https://github.com/user-attachments/assets/2d79cb0b-d5e5-4801-9097-0e5badeb3351" />


<img width="1636" height="972" alt="image" src="https://github.com/user-attachments/assets/082d748b-20d3-4481-aa3c-4b830958f144" />


The manager automatically handles different archive structures, including:
- Mods packed with a clean structure like: somezip.zip/archive/pc/mod/...
- Mods packed inside an extra folder: somezip.zip/SOMERANDOMFOLDER/archive/pc/mod/...
- Zipped `.archive` files without normal folder structures, which get extracted to archive/pc/mod/

---

### For users with premium account on Nexus Mods

- You can export your downloaded mod files into a **modlist.json**, which can be used for mass download.
- Useful for sharing a mod list you have created.

<img width="1633" height="969" alt="image" src="https://github.com/user-attachments/assets/35555f60-949b-488e-912a-e45002e223be" />

---

## Requirements

- Windows 10/11
- Cyberpunk 2077 installed
- Nexus Mods account (SSO link required to fetch API key)
- .NET Desktop Runtime 8.0 or later (required to run the app)

---

## Building / Releasing

1. Restore dependencies: `dotnet restore HelixModManager/HelixModManager.csproj`
2. Publish for Windows:  
   `dotnet publish HelixModManager/HelixModManager.csproj -c Release -r win-x64 --self-contained false`
   - Add `/p:PublishSingleFile=true` if you want a single executable.
   - Use `--self-contained true` to bundle the .NET runtime.
3. Distribute everything under `HelixModManager/bin/Release/net8.0-windows/win-x64/publish/` (zip the folder or your single file build).

---

## License

MIT License

---

