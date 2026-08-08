# TextRecast Privacy Notice

Effective: August 8, 2026

TextRecast is a local Windows writing assistant. This notice describes the data handled by the application and its installer.

## Text processing

Selected text, generated text, prompts, and model output are processed on the current computer by the local language model. TextRecast does not send that content to a hosted text-processing service, and it does not save a writing history.

TextRecast does not currently include an account system, advertising, analytics, telemetry, or automatic crash reporting.

## Clipboard and Windows access

TextRecast uses Windows UI Automation to read and replace a selection when the source application supports it. It can temporarily use the Windows clipboard as a fallback. The application attempts to restore the previous clipboard contents and reports when restoration cannot be verified.

Windows clipboard history, clipboard synchronization, accessibility software, or third-party clipboard managers operate outside TextRecast and may retain content placed on the clipboard. Disable those features before handling sensitive text if that behavior is not acceptable.

## Network access

TextRecast connects to the pinned model source only when a selected model is not already installed and the user confirms its download. The request identifies the model file and necessarily exposes ordinary connection data, such as the IP address and HTTP request metadata, to the model host. Selected text and generated text are not included in that request.

After a verified model is installed, normal text formatting is local and does not require a text-processing service.

## Data stored on the computer

TextRecast can store the following data for the current Windows user under `%LOCALAPPDATA%\TextRecast`:

- the selected local model and its verified model metadata;
- model-selection and setup state;
- a partial model download so an interrupted download can resume; and
- the downloaded model file.

The installer stores application files under the selected per-user installation directory and adds normal Windows uninstall information and a Start Menu shortcut. On the completion page, it offers a desktop shortcut and a shortcut in the current user's Windows Startup folder; both choices are enabled by default and can be cleared before choosing Finish. The Startup shortcut launches the floating TextRecast button when that Windows user signs in. Uninstall removes these application shortcuts but preserves the user model and setup data so reinstalling does not require another model download. That data can be removed manually by deleting `%LOCALAPPDATA%\TextRecast` after TextRecast is closed.

## Questions and changes

Privacy-related questions can be raised through the repository's issue tracker. Do not include private text, clipboard contents, credentials, or other sensitive data in an issue.

Material changes to TextRecast's data handling will be reflected in this notice and its effective date.
