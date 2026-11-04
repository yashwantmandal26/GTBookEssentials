# Fresh Windows / missing device controls

**The original Control Center app is not required.** On the reviewed GL613 laptop, the ACPI-to-WMI device uses Microsoft's `wmiacpi.inf` and `wmiacpi.sys`. Essentials calls the firmware-exposed `root\wmi:ACPIMethod` interface and uses Windows HID/serial support for lighting.

Setup now checks that interface and its expected methods before installation. If the interface is missing, it tries one bounded Windows device rescan, then checks again. This does not install an unknown driver or reset WMI. Normal installations do not perform a repair scan.

## If setup still reports missing support

1. Run Windows Update, including **Advanced options → Optional updates → Driver updates**, then restart.
2. Use [Infinix/Carlcare Drivers and Manuals](https://www.carlcare.com/global/drivers-download/) to select your exact laptop or enter its serial number **on the official site**. Install only the manufacturer's matching package, then restart and rerun Essentials Setup.
3. If no matching package is offered, contact Infinix/Carlcare. Do not use a different laptop's BIOS or third-party driver packs.

You can also install Essentials without device controls while resolving the problem. Setup makes that limitation explicit. Copying another PC's Windows `.sys` files cannot create a missing firmware interface, and this repository does not redistribute proprietary OEM/Windows drivers.

A clean Windows installation without Control Center has not yet been physically tested on this laptop. The presence of Microsoft's driver alone does not prove the firmware interface is registered and callable.

References: [Microsoft ACPI driver](https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/acpi-driver), [Microsoft ACPI-to-WMI mapping documentation](https://github.com/microsoft/Windows-driver-samples/blob/main/wmi/wmiacpi/wmi-acpi.htm), [Windows driver updates](https://support.microsoft.com/en-us/windows/hardware/drivers/the-windows-driver-policy).
