# Security Policy

## Supported versions

TextRecast is currently under initial development. Security fixes are applied to the latest revision on the `main` branch.

## Reporting a vulnerability

Do not disclose a suspected vulnerability in a public issue. Use the repository's private security-advisory reporting feature when it is available. Otherwise, contact the repository owner privately through the contact method listed on their hosting profile.

Include:

- A clear description of the issue and its impact
- Reproduction steps or a minimal proof of concept
- The affected TextRecast version or commit
- Relevant Windows and .NET versions
- Any suggested mitigation

Do not include real private text, clipboard contents, credentials, or other sensitive data in a report.

## Security boundaries

TextRecast handles text selected in other Windows applications and uses UI Automation, the clipboard, and simulated keyboard input. Changes to capture, replacement, model downloading, prompt construction, or native interop require particular care.

The application must continue to:

- Avoid logging or persisting selected and generated text
- Verify the source window, process, capture age, and selection before replacement
- Reject empty, oversized, or unsupported replacement content
- Verify downloaded model size and SHA-256 before installation
- Start model network requests only after explicit user selection and Install
- Keep retries and resumed downloads bound to the selected catalog model
- Restrict installer writes and cleanup to validated per-user, product-owned paths
- Preserve the previous complete application payload when an upgrade activation fails
- Run with the current user's privileges rather than requesting elevation
