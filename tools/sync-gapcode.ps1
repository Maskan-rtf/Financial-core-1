# Sync Financial-Core skills to GapCode CLI (WSL)
# Usage: .\tools\sync-gapcode.ps1

wsl bash -lc "sed -i 's/\r$//' /mnt/d/work/Maskan/Panel/Financial-Core/tools/sync-gapcode.sh && bash /mnt/d/work/Maskan/Panel/Financial-Core/tools/sync-gapcode.sh"
