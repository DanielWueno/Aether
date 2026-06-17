# Aether: Reverse Tethering Framework

Aether is a .NET 10 application designed to provide reverse tethering for Android devices (sharing the PC's internet connection with the phone via USB) without utilizing the Android `VpnService` API. 

By avoiding the VPN slot, Aether allows users to run third-party VPN applications (like Surfshark or Google One VPN) on the Android device simultaneously while tethered.

## ⚙️ How It Works (Architecture)

Standard reverse tethering applications (like Gnirehtet or Tetrd) create a local VPN on the Android device to capture network traffic. Aether takes a different approach:

1.  **ADB Reverse Tunnel:** It establishes a TCP tunnel over the USB connection using `adb reverse`.
2.  **Global Proxy Injection:** It injects global HTTP proxy settings directly into the Android system configuration (`settings put global http_proxy`).
3.  **Transparent TCP Relay:** The PC runs a custom, lightweight TCP relay. Instead of acting as a traditional HTTP proxy that intercepts and decrypts SSL traffic, Aether acts as a transparent conduit. It reads the initial `CONNECT` request and then blindly relays the bytes between the mobile device and the destination server.

This transparent approach ensures that SSL handshakes occur directly between the Android application and the external server, preventing certificate authority errors (`ERR_CERT_AUTHORITY_INVALID`) when used in standard networks.

## 📋 Requirements

*   **OS:** Windows 10 or 11
*   **Runtime:** .NET 10
*   **Device:** Android 6.0 or higher with **USB Debugging** enabled.
*   **Dependencies:** `adb.exe` must be present in the system PATH or placed in a local `adb` folder within the application directory.

## 🚀 Usage

1.  Connect your Android device via USB and ensure USB Debugging is authorized.
2.  Launch `Aether.Desktop.exe`. The application will appear in the system tray.
3.  Right-click the tray icon (or use the main window) and click **"Start Tethering"**.
4.  To verify connectivity, open a browser on the device.

### Diagnostics & Corporate Proxies

Aether includes a **Diagnostics** button to detect system-level proxies (e.g., configurations applied via PAC files). 

*   If your network requires a proxy to access the internet, Aether will attempt to chain its connection through it automatically.
*   If automatic detection fails, you can manually enter the proxy address (e.g., `http://192.168.1.50:8080`) in the UI before starting the tethering service.

## ⚠️ Known Limitations (Enterprise Environments)

Aether is designed for standard network environments. If you are operating within a strict corporate network that employs **Deep Packet Inspection (DPI)**, **Cloud Access Security Brokers (CASB)** (e.g., Netskope), or aggressive **EDR** solutions (e.g., CrowdStrike), you will encounter limitations:

*   **Certificate Errors:** CASB solutions intercept traffic at the kernel/network level and inject their own SSL certificates. Because Aether acts as a transparent relay, the Android device will receive the corporate certificate. Unless the corporate root certificate is manually installed on the Android device, applications will reject the connection (`ERR_CERT_AUTHORITY_INVALID`).
*   **VPN Blocking:** Corporate firewalls frequently block the protocols and ports used by commercial VPNs (like Surfshark). In these environments, even if Aether successfully relays the traffic, the VPN connection will timeout or return a `403 Forbidden` error.

## 🏗️ Project Structure

*   `Aether.Core`: Contains the ADB orchestration logic and the TCP Relay implementation.
*   `Aether.Shared`: Interfaces and models.
*   `Aether.Desktop`: WPF User Interface and System Tray management.

## 🤝 Contributing
Contributions, issues, and feature requests are welcome. Feel free to fork the repository and submit a pull request.