---
name: vps-bootstrap
description: Use when setting up a brand new Ubuntu 24.04 VPS for the first time, before any Coolify deployment exists. Covers the one-time bootstrap script (base hardening, firewall, fail2ban) and installing Coolify. This is a prerequisite to deployment-coolify, not an ongoing deploy task. Trigger on "set up a new VPS", "bootstrap the server", or "prepare a fresh server for Coolify".
tools: Read, Write, Bash, Grep, Glob
model: inherit
---

This is a one-time, high-stakes operation on a fresh server. It is the prerequisite step to Coolify deployment — read `deployment-coolify` for what happens after this script runs.

## Non-negotiable rules

This script runs once on a fresh server — never re-run on a live production server. Postgres, Redis, and the reverse proxy are not installed bare-metal — Coolify manages them as Docker containers afterward. Docker is installed by Coolify's own installer (it installs Docker from the official source if it is missing), so the bootstrap script only handles base OS hardening before handing off to Coolify. UFW firewall is enabled with ports 22, 80, 443 open, plus port 8000 for the Coolify dashboard (lock this down to your IP once set up). `fail2ban` is installed for SSH brute-force protection. Unattended security upgrades are enabled. SSH is hardened (root login and password auth disabled) — but only after confirming a working key-based login, or you will lock yourself out.

## What gets installed

| Tool | Method | Purpose |
|---|---|---|
| git, curl, wget | apt | Utilities |
| ufw | apt | Firewall |
| fail2ban | apt | SSH brute force protection |
| unattended-upgrades | apt | Automatic security patches |
| Docker Engine | Coolify installer | Runs all app and resource containers |
| Coolify | official install script | Self-hosted PaaS — builds, deploys, manages Traefik/SSL/Postgres/Redis |

## Bootstrap script location

Commit to `devops/scripts/bootstrap.sh` — safe to commit, contains no secrets. The `SSH_PUBLIC_KEY` variable is filled in at runtime per server, never committed with a value.

## Script structure (steps, in order)

1. System update — `apt update && apt upgrade -y`, install curl/wget/git/ufw/fail2ban/unattended-upgrades/build-essential
2. UFW firewall — deny incoming by default, allow outgoing, allow 22/80/443 and 8000 (Coolify dashboard), enable
3. fail2ban — `bantime=1h`, `findtime=10m`, `maxretry=5` on sshd jail
4. Unattended security upgrades enabled via `/etc/apt/apt.conf.d/20auto-upgrades`
5. SSH hardening — install the provided `SSH_PUBLIC_KEY` into `root`'s `authorized_keys`, then set `PermitRootLogin prohibit-password`, `PasswordAuthentication no`, restart sshd
6. Install Coolify — `curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash` (installs Docker if absent, sets up the Coolify stack)

The script must fail fast (`set -euo pipefail`), require root (`[[ $EUID -ne 0 ]] && fail`), and require `SSH_PUBLIC_KEY` to be set before running — refuse to proceed with an empty key.

> Coolify expects to manage the server as `root` (or a sudo-capable user) and provisions its own deploy access, Docker networks, and data volumes. There is no separate Kamal-style `deploy` user or manual `/data` directories to create — Coolify owns container storage.

## Running it

```bash
# Option A: paste into the server directly
nano bootstrap.sh   # paste, edit the CONFIGURATION section
bash bootstrap.sh

# Option B: once committed to the repo
curl -fsSL https://raw.githubusercontent.com/<org>/<repo>/main/devops/scripts/bootstrap.sh | sudo bash
```

## Post-bootstrap verification

Open a **new SSH session** (not the same session used to run the script) and verify: `docker ps` shows the Coolify containers running, `sudo ufw status` shows the allowed ports, `sudo fail2ban-client status` shows the sshd jail active, and the Coolify dashboard is reachable at `http://<VPS_IP>:8000`.

## Coolify first-run setup

1. Open `http://<VPS_IP>:8000` and create the admin account on the very first visit (the registration form is open until the first account exists — do this immediately).
2. Set a domain for the Coolify dashboard itself and enable HTTPS, or restrict port 8000 to your IP via UFW.
3. Connect this Git repository as a **Source** (GitHub App recommended) so deployments can pull and build.
4. Continue with the `deployment-coolify` subagent to create the applications and managed resources.

## What Claude Code must not do

Never run `bootstrap.sh` — the developer runs this manually on a fresh server. Never install Postgres, Redis, or a reverse proxy bare-metal — these run as Coolify-managed Docker resources afterward. Never disable `PasswordAuthentication` before confirming the `SSH_PUBLIC_KEY` works, or the server may become unreachable. Never modify the `SSH_PUBLIC_KEY` placeholder — it's filled in by the developer at runtime, per server. Never open additional UFW ports beyond 22/80/443/8000 without explicit instruction, and recommend restricting 8000 to a trusted IP once Coolify is configured.
