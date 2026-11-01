# AllWebHub Share - PC Hub

Fast local file sharing between phones and PCs on the same Wi-Fi or hotspot. No internet needed.
One PC runs this small ASP.NET Core server; phones open its address in a browser, upload files, and download them on other devices.

## Run from source
1. Install the .NET SDK (match the version in `AllWebHubHub.csproj`).
2. Run `run.bat` (or press F5 in Visual Studio).
3. Open `http://localhost:5000` on the PC. The page shows the address and a QR code for your phone.
4. Run `open-firewall.bat` once as administrator so phones can connect.

## Run from a release (no .NET needed)
Download `AllWebHubHub-win-x64.zip` from the Releases page, extract it, and double-click `AllWebHubHub.exe`.

Files are stored in the `Shared` folder next to the program. There is no sign-in, so use it only on a network you trust.
