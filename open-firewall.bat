@echo off
rem Run this file as Administrator once. It lets phones on your private Wi-Fi reach the hub on port 5000.
netsh advfirewall firewall add rule name="AllWebHub Hub" dir=in action=allow protocol=TCP localport=5000 profile=private,domain
pause
