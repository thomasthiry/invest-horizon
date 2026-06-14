# Reclaim Docker Disk Space (Windows / WSL2)

## 1. Check usage
```powershell
docker system df
```

## 2. Prune (safe — keeps running containers, volumes, tagged images)
```powershell
docker builder prune -f    # build cache (usually the biggest)
docker image prune -f      # dangling <none> images
```

## 3. Compact the vhdx — REQUIRED to give space back to Windows
Pruning only frees space *inside* the disk; the `.vhdx` file shrinks only when compacted.
Run in an **Administrator** PowerShell:
```powershell
Stop-Process -Name "Docker Desktop" -Force -ErrorAction SilentlyContinue
wsl --shutdown
Optimize-VHD -Path "C:\Users\Thomas\AppData\Local\Docker\wsl\disk\docker_data.vhdx" -Mode Full
```
Then relaunch Docker Desktop. Postgres volume data is untouched.

## Notes
- After releases, `docker compose up --build` leaves lots of build cache + dangling images — re-run this.
- Heavier: `docker system prune -a` also removes ALL unused images (re-pull/rebuild later).