# Lite CPMM - Cyberpunk Mod Manager (Lite)

**Lite CPMM** is a lightweight, Nexus-integrated mod manager designed specifically for Cyberpunk 2077. It lets you download, manage, and install your mods (which you are tracking on Nexus Mods) easily using the Nexus Mods API.

---

## Features

- Fetch tracked mods using your Nexus Mods API key (account login integration to be implemented)
- Filter based on category and search mods based on name
- Check available update status
- Download individual mod files (Premium + non-premium Nexus Mods account support)
- Mass install and uninstall with a single click
- Export and import mod lists.
- Users with **premium** Nexus Mods accounts can mass download from imported modlists.

---

## Getting Started

### 1. Set up your settings
<img width="818" alt="settings" src="https://github.com/user-attachments/assets/e792596b-2218-465f-9bcc-17ce33140c83" />

- Input your **Nexus API key** (required for fetching tracked mods).
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
<img width="818" alt="mod list status messages" src="https://github.com/user-attachments/assets/5b6dadb1-18c9-4d15-986a-4caafd4bd703" />


---

### 4. Download Mod Files

- If you are a **Nexus Premium user**, click **"Download Files"** to fetch files directly

<img width="443" alt="download files" src="https://github.com/user-attachments/assets/890ea63e-c242-489a-9b4c-10f0f7949b25" />

- If you're a **non-premium user**, click the orange "Mod manager download" button on Nexus Mods. The app will handle the `.nxm` link and track the download.
- You can **double click** the mod name in the manager to get to the mod's page.

<img width="704" alt="nexus mods example" src="https://github.com/user-attachments/assets/2acb7a29-e40a-4341-82f6-86f2a6a26e5c" />


- All downloaded files will appear in your game’s `Mods` directory:
<img width="641" alt="downloaded mods location" src="https://github.com/user-attachments/assets/24663491-9811-43c6-a508-0011bac81483" />

- Mod files can be deleted with the `Manage Files` button:

<img width="443" alt="manage files" src="https://github.com/user-attachments/assets/a073e4f0-a524-4d7a-b71f-b464f9b582f4" />

- Multiple mods can be selected at once with ctrl+click, and then clicking `Manage Files` button lets you delete files for selected mods. Without selection all downloaded files are shown.
<img width="818" alt="mod list filter and selection" src="https://github.com/user-attachments/assets/9514c669-ba47-43b5-8209-040d6a72293a" />

---

#### Mod Status Meaning

<img width="818" alt="mod list status messages" src="https://github.com/user-attachments/assets/89d0fcf0-28cb-4feb-b0ab-6e7fdfbab960" />

- **Latest Downloaded**: The most recent overall file by timestamp is downloaded.
- **Downloaded**: A file is downloaded, but there are some newer files uploaded by mod author.
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

