# Security

This repository must not contain credentials or production secrets.

Never commit:

- `.env` files
- database passwords or production connection strings
- AWS access keys or session tokens
- JWT signing keys
- certificates and private keys

If a secret is committed, revoke or rotate it immediately and remove it from Git history before continuing.
