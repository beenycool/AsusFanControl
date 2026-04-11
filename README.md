# Asus Fan Control

### Download
Go to [releases](../../releases)

### Run

The same `AsusFanControl.exe` starts the **GUI** when you double‑click it or run it with **no arguments**, and runs **command-line mode** when you pass any CLI flag (for example `AsusFanControl.exe --get-fan-speeds`).

<details>
    <summary>Command line (same executable)</summary>
    
    AsusFanControl.exe <args>
        --get-fan-speeds
        --set-fan-speeds=0-100 (percent value, 0 for turning off test mode)
        --get-fan-count
        --get-fan-speed=fanId (comma separated)
        --set-fan-speed=fanId:0-100 (comma separated, percent value, 0 for turning off test mode)
        --get-cpu-temp
</details>

GUI: run `AsusFanControl.exe` with no arguments (or use `RunAsAdmin.bat` where provided).  

![AsusFanControlGUI](https://github.com/Karmel0x/AsusFanControl/assets/25367564/fe197ad0-7079-4d51-ae78-177cb6369e96)

### Why need it?
My laptop does not support the [Fan Profile](https://github.com/Karmel0x/AsusFanControl/assets/25367564/924d990a-bf20-4b8d-bf9d-56c460174d99) option, but it often overheats. Looked for apps to control fans, but none is working.

### Compatibility
This program should work on any laptop with x64 windows where [Fan Diagnosis](https://github.com/Karmel0x/AsusFanControl/assets/25367564/7129833b-97af-4da8-9148-b71e49552ea4) in [MyASUS](https://apps.microsoft.com/store/detail/myasus/9N7R5S6B0ZZH) application is working as it is using same library.

[ASUS System Control Interface](https://www.asus.com/support/faq/1047338/) is necessary for this software to work - `ASUS System Analysis` service [must be running](../../issues/16). It's automatically installed with `MyASUS` app.

The release build embeds the same `AsusWinIO64.dll` (licenced to `(c) ASUSTek COMPUTER INC.`) that you can find in `C:\Windows\System32\DriverStore\FileRepository\asussci2.inf_amd64_-\ASUSSystemAnalysis\` if you have MyASUS installed; the DLL also remains in the repository under `AsusFanControl.Core` for the build.

[Works on](../../issues/13): 
- ASUS: VivoBook, ZenBook, TUF Gaming, ROG Strix, ROG Zephyrus, ROG Flow
