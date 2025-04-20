# Lite CPMM - Cyberpunk Mod Manager (Lite)

**Lite CPMM** is a lightweight, Nexus-integrated mod manager designed specifically for Cyberpunk 2077. It lets you download, manage, and install your mods (which you are tracking on Nexus Mods) easily using the Nexus Mods API.

---

## Features

- Fetch and track mods using your Nexus Mods API key (account login integration to be implemented)
- Filter and categorize mods
- Check available update status
- Download mod files (Premium + non-premium Nexus Mods account support)
- Install and uninstall with a single click

---

## Getting Started

### 1. Set up your settings
<img width="818" alt="settings" src="https://github.com/user-attachments/assets/f31efe22-c8cf-4a1a-91d5-7a25f3e56f43" />


- Input your **Nexus API key** (required for fetching tracked mods).
- Set your **Game Installation Directory** and **Output Directory** if not set correctly by default.

---

### 2. Track your desired mods on Nexus Mods
<img width="368" alt="nexus mods example track mods" src="https://github.com/user-attachments/assets/25ace659-b140-4649-a8aa-c7646d0653ff" />

Go to your desired mod's page and hit `Track` to add it to your tracked list.

---

### 3. Fetch Tracked Mods


Navigate to the **Mods** tab and click **"Fetch Tracked Mods"** to populate your list.

You can now:
- See **status** of mods (e.g., *Downloaded*, *Update Available!*, etc.)
- **Filter** by mod categories
<img width="818" alt="mod list status messages" src="https://github.com/user-attachments/assets/5b6dadb1-18c9-4d15-986a-4caafd4bd703" />


---

### 4. Download Mod Files

- If you are a **Nexus Premium user**, click **"Download Files"** to fetch files directly

<img width="443" alt="download files" src="https://github.com/user-attachments/assets/890ea63e-c242-489a-9b4c-10f0f7949b25" />

- If you're a **non-premium user**, click the orange "Mod manager download" button on Nexus Mods. The app will handle the `.nxm` link and track the download.

<img width="704" alt="nexus mods example" src="https://github.com/user-attachments/assets/2acb7a29-e40a-4341-82f6-86f2a6a26e5c" />


- All downloaded files will appear in your game’s `Mods` directory:
<img width="641" alt="downloaded mods location" src="https://github.com/user-attachments/assets/24663491-9811-43c6-a508-0011bac81483" />

- Mod files can be deleted with the `Manage Files` button:

<img width="443" alt="manage files" src="https://github.com/user-attachments/assets/a073e4f0-a524-4d7a-b71f-b464f9b582f4" />

---

#### Mod Status Meaning

<img width="818" alt="mod list status messages" src="https://github.com/user-attachments/assets/89d0fcf0-28cb-4feb-b0ab-6e7fdfbab960" />

- **Latest Downloaded**: The most recent version (by timestamp and file name) is installed.
- **Downloaded**: A file is downloaded, there are some newer files uploaded by mod author.
- **Update Available!**: A newer file with the same filename exists.
- **Not Downloaded**: No file for this mod yet.

---

### 5. Install Mods

Go to the **Files** tab to:
- **Install** or **Uninstall** mods with one click
- Delete specific downloaded mod files

<img width="819" alt="file list" src="https://github.com/user-attachments/assets/257a91e6-59cf-4d6f-a2b6-9ee9efe90413" />


The manager automatically handles different archive structures, including:
- Mods packed with a clean structure like: somezip.zip/archive/pc/mod/...
- Mods packed inside an extra folder: somezip.zip/SOMERANDOMFOLDER/archive/pc/mod/...
- Zipped `.archive` files without normal folder structures, which get extracted to archive/pc/mod/

---

## Requirements

- Windows 10/11
- Cyberpunk 2077 installed
- Nexus Mods account (API key)
- .NET Desktop Runtime 8.0 or later (required to run the app)

---

## License

MIT License

---

