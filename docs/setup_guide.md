# Electrolite Setup Guide

Electrolite is designed to be a lightweight, zero-bloat battery charge limit manager for ASUS laptops. Since it interacts directly with ASUS driver and firmware layers, there are a few important details to ensure it functions correctly on your machine.

---

## 🚀 Installation & First Run

1. **Download the Binary:**
   - Go to the [Releases](https://github.com/AliKarbasiCom/electrolite/releases) page on GitHub.
   - Download the latest standalone zip containing the compiled executable (`Electrolite_lite.exe`).
   - Extract the executable to a folder of your choice (e.g., `C:\Program Files\Electrolite\` or `C:\Tools\Electrolite\`).

2. **Run as Administrator:**
   - **Important:** Electrolite requires **Administrator privileges** to write commands directly to the ASUS Embedded Controller driver (`\\.\ATKACPI`) and to sync the battery threshold to the registry (`HKLM`).
   - Double-click `Electrolite_lite.exe`. The application manifest is pre-configured to automatically request User Account Control (UAC) elevation.

3. **Verify Startup:**
   - Once running, the utility starts headless (no window, no taskbar icon).
   - Check your system tray for the **Battery Silhouette / Teal Lightning Bolt** icon.
   - Left-click the tray icon to toggle the popup flyout dashboard, or right-click to open the context menu.

---

## ⚙️ Running at Windows Startup

To run Electrolite automatically whenever Windows boots, you can create a simple Windows Task Scheduler task (recommended for elevated programs) or place a shortcut in your Startup folder.

### Option A: Windows Task Scheduler (Recommended for UAC bypass)
Since Electrolite requires administrator permissions, standard Windows startup folder shortcuts might trigger a UAC prompt every boot or get blocked. A Task Scheduler task bypasses this:

1. Press `Win + R`, type `taskschd.msc` and hit Enter to open **Task Scheduler**.
2. Click **Create Basic Task...** in the Actions panel.
3. Name it `Electrolite` and click Next.
4. Set the trigger to **When I log on** and click Next.
5. Set the action to **Start a program** and click Next.
6. Browse to where you saved `Electrolite.exe` and select it.
7. Click Finish.
8. Locate `Electrolite` in the Task Scheduler Library list, right-click it, and select **Properties**.
9. In the General tab, check **Run with highest privileges** (this bypasses the UAC prompt on boot).
10. In the Settings tab, make sure **Allow task to be run on demand** is checked, and **Stop the task if it runs longer than** is unchecked.

---

## 🛠️ Troubleshooting & Conflicts

### 1. The limit is not holding (reverts to 100%)
If you click **Balanced (80%)** but your laptop charges all the way to 100% anyway, another ASUS service is likely overwriting the charge limit in the background:
- **MyASUS App:** Open the MyASUS app, navigate to Customization/Battery Health Charging, and disable it or set it to correspond to your Electrolite setting.
- **ASUS System Control Interface / Armoury Crate Services:** Other ASUS background helper processes can periodically poll the BIOS and reset the limits. If you have other third-party ASUS power management tools or official battery limits enabled, make sure they are disabled or not running at the same time to prevent conflicts.

### 2. Global Hotkey not working
The default hotkey is `Ctrl + Shift + B`. If this does not toggle the mode:
- Another application on your PC (such as a browser or development environment) might have already registered that hotkey sequence globally.
- You can change the mode by right-clicking the system tray icon instead.
