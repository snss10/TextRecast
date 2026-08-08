!ifndef APP_VERSION
  !error "APP_VERSION must be defined."
!endif
!ifndef FILE_VERSION
  !error "FILE_VERSION must be defined."
!endif
!ifndef PUBLISH_DIRECTORY
  !error "PUBLISH_DIRECTORY must be defined."
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must be defined."
!endif
!ifndef PRODUCT_ID
  !error "PRODUCT_ID must be defined."
!endif

Unicode true
RequestExecutionLevel user
SetCompressor zlib
SetOverwrite on

Name "TextRecast"
Caption "TextRecast Application Package"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\TextRecast"
InstallDirRegKey HKCU "Software\TextRecast" "InstallLocation"
BrandingText "TextRecast"
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${FILE_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "TextRecast"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "snss10"
VIAddVersionKey /LANG=1033 "FileDescription" "TextRecast per-user application package"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright 2026 snss10"

!macro Fail CODE MESSAGE
  SetErrorLevel ${CODE}
  Abort "${MESSAGE}"
!macroend

Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "TextRecast" MainSection
  SetShellVarContext current
  SetOutPath "$INSTDIR"

  IfFileExists "$INSTDIR\.textrecast-install" validate_ownership_marker create_ownership_marker

  validate_ownership_marker:
  ClearErrors
  FileOpen $0 "$INSTDIR\.textrecast-install" r
  IfErrors ownership_marker_failed
  FileRead $0 $1
  FileClose $0
  IfErrors ownership_marker_failed
  StrCmp $1 "${PRODUCT_ID}" ownership_marker_valid ownership_marker_invalid

  create_ownership_marker:
  ClearErrors
  FileOpen $0 "$INSTDIR\.textrecast-install" w
  IfErrors ownership_marker_failed
  FileWrite $0 "${PRODUCT_ID}"
  IfErrors ownership_marker_write_failed
  FileClose $0
  IfErrors ownership_marker_failed
  Goto ownership_marker_valid

  ownership_marker_write_failed:
  FileClose $0

  ownership_marker_failed:
  !insertmacro Fail 10 "The TextRecast installation ownership marker could not be created or read."

  ownership_marker_invalid:
  !insertmacro Fail 11 "The selected directory is not owned by this TextRecast installation."

  ownership_marker_valid:
  IfFileExists "$INSTDIR\Application\.textrecast-payload-complete" 0 check_previous_payload
  IfFileExists "$INSTDIR\Application\TextRecast.exe" 0 check_previous_payload
  IfFileExists "$INSTDIR\Application\LICENSE" 0 check_previous_payload
  ClearErrors
  FileOpen $0 "$INSTDIR\Application\.textrecast-payload-complete" r
  IfErrors check_previous_payload
  FileRead $0 $2
  FileClose $0
  IfErrors check_previous_payload
  StrCmp $2 "" check_previous_payload current_payload_valid

  check_previous_payload:
  IfFileExists "$INSTDIR\Application.previous\.textrecast-payload-complete" 0 no_previous_payload
  IfFileExists "$INSTDIR\Application.previous\TextRecast.exe" 0 no_previous_payload
  ClearErrors
  FileOpen $0 "$INSTDIR\Application.previous\.textrecast-payload-complete" r
  IfErrors no_previous_payload
  FileRead $0 $2
  FileClose $0
  IfErrors no_previous_payload
  StrCmp $2 "" no_previous_payload restore_previous_payload

  restore_previous_payload:
  RMDir /r "$INSTDIR\Application"
  IfFileExists "$INSTDIR\Application\*.*" recovery_failed
  RMDir "$INSTDIR\Application"
  ClearErrors
  Rename "$INSTDIR\Application.previous" "$INSTDIR\Application"
  IfErrors recovery_failed
  Goto current_payload_valid

  no_previous_payload:
  RMDir /r "$INSTDIR\Application"
  IfFileExists "$INSTDIR\Application\*.*" recovery_failed
  RMDir "$INSTDIR\Application"
  Goto current_payload_valid

  recovery_failed:
  !insertmacro Fail 20 "The previous TextRecast application payload could not be recovered. Close TextRecast and retry Setup."

  current_payload_valid:
  RMDir "$INSTDIR\Application.pending"
  IfFileExists "$INSTDIR\Application.pending\*.*" clean_pending_payload pending_payload_ready

  clean_pending_payload:
  ClearErrors
  RMDir /r "$INSTDIR\Application.pending"
  IfErrors pending_cleanup_failed

  pending_payload_ready:
  ClearErrors
  CreateDirectory "$INSTDIR\Application.pending"
  IfErrors stage_failed
  SetOutPath "$INSTDIR\Application.pending"
  ClearErrors
  File /r "${PUBLISH_DIRECTORY}\*"
  IfErrors stage_failed
  IfFileExists "$INSTDIR\Application.pending\TextRecast.exe" 0 stage_failed
  IfFileExists "$INSTDIR\Application.pending\LICENSE" 0 stage_failed
  IfFileExists "$INSTDIR\Application.pending\PRIVACY.md" 0 stage_failed

  ClearErrors
  FileOpen $0 "$INSTDIR\Application.pending\.textrecast-payload-complete" w
  IfErrors stage_failed
  FileWrite $0 "${APP_VERSION}"
  IfErrors payload_marker_write_failed
  FileClose $0
  IfErrors stage_failed

  ClearErrors
  SetOutPath "$INSTDIR"
  IfErrors stage_failed

  RMDir "$INSTDIR\Application.previous"
  IfFileExists "$INSTDIR\Application.previous\*.*" clean_previous_payload previous_payload_ready

  clean_previous_payload:
  ClearErrors
  RMDir /r "$INSTDIR\Application.previous"
  IfErrors previous_cleanup_failed

  previous_payload_ready:
  StrCpy $2 ""
  IfFileExists "$INSTDIR\Application\.textrecast-payload-complete" 0 activate_staged_payload
  ClearErrors
  FileOpen $0 "$INSTDIR\Application\.textrecast-payload-complete" r
  IfErrors current_move_failed
  FileRead $0 $2
  FileClose $0
  IfErrors current_move_failed
  StrCmp $2 "" current_move_failed
  ClearErrors
  Rename "$INSTDIR\Application" "$INSTDIR\Application.previous"
  IfErrors current_move_failed

  activate_staged_payload:
  ClearErrors
  Rename "$INSTDIR\Application.pending" "$INSTDIR\Application"
  IfErrors staged_move_failed
  Goto payload_activated

  current_move_failed:
  RMDir /r "$INSTDIR\Application.pending"
  !insertmacro Fail 30 "The existing application files are in use. Close TextRecast and retry Setup."

  staged_move_failed:
  ClearErrors
  RMDir /r "$INSTDIR\Application"
  IfFileExists "$INSTDIR\Application.previous\.textrecast-payload-complete" 0 staged_move_unrecoverable
  ClearErrors
  Rename "$INSTDIR\Application.previous" "$INSTDIR\Application"
  IfErrors staged_move_unrecoverable

  staged_move_unrecoverable:
  !insertmacro Fail 31 "The new application payload could not be activated. The previous payload was preserved when available."

  payload_marker_write_failed:
  FileClose $0

  stage_failed:
  RMDir /r "$INSTDIR\Application.pending"
  !insertmacro Fail 32 "The application payload could not be staged. Existing application files were not changed."

  pending_cleanup_failed:
  !insertmacro Fail 33 "A previous staged payload could not be removed. Close TextRecast Setup and retry."

  previous_cleanup_failed:
  RMDir /r "$INSTDIR\Application.pending"
  !insertmacro Fail 34 "The previous rollback payload could not be cleaned up. Close TextRecast and retry Setup."

  payload_activated:
  SetOutPath "$INSTDIR"
  ClearErrors
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  IfErrors metadata_failed

  ClearErrors
  CreateDirectory "$SMPROGRAMS\TextRecast"
  CreateShortcut "$SMPROGRAMS\TextRecast\TextRecast.lnk" "$INSTDIR\Application\TextRecast.exe"
  IfErrors metadata_failed

  ClearErrors
  WriteRegStr HKCU "Software\TextRecast" "ProductId" "${PRODUCT_ID}"
  WriteRegStr HKCU "Software\TextRecast" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\TextRecast" "Version" "${APP_VERSION}"

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayName" "TextRecast"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "Publisher" "snss10"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayIcon" "$INSTDIR\Application\TextRecast.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "URLInfoAbout" "https://github.com/snss10/TextRecast"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "NoRepair" 1
  IfErrors metadata_failed

  RMDir /r "$INSTDIR\Application.previous"
  IfFileExists "$INSTDIR\Application.previous\*.*" previous_cleanup_after_install_failed
  RMDir "$INSTDIR\Application.previous"
  Goto install_complete

  metadata_failed:
  IfFileExists "$INSTDIR\Application.previous\.textrecast-payload-complete" restore_after_metadata_failure clean_after_metadata_failure

  restore_after_metadata_failure:
  ClearErrors
  RMDir /r "$INSTDIR\Application"
  IfErrors metadata_rollback_failed
  ClearErrors
  Rename "$INSTDIR\Application.previous" "$INSTDIR\Application"
  IfErrors metadata_rollback_failed

  ClearErrors
  CreateDirectory "$SMPROGRAMS\TextRecast"
  CreateShortcut "$SMPROGRAMS\TextRecast\TextRecast.lnk" "$INSTDIR\Application\TextRecast.exe"
  WriteRegStr HKCU "Software\TextRecast" "ProductId" "${PRODUCT_ID}"
  WriteRegStr HKCU "Software\TextRecast" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\TextRecast" "Version" "$2"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayName" "TextRecast"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayVersion" "$2"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "Publisher" "snss10"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "DisplayIcon" "$INSTDIR\Application\TextRecast.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "URLInfoAbout" "https://github.com/snss10/TextRecast"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast" "NoRepair" 1
  !insertmacro Fail 40 "Setup could not update the Windows installation metadata. The previous application payload was restored."

  clean_after_metadata_failure:
  Delete "$SMPROGRAMS\TextRecast\TextRecast.lnk"
  RMDir "$SMPROGRAMS\TextRecast"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast"
  DeleteRegKey HKCU "Software\TextRecast"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR\Application"
  Delete "$INSTDIR\.textrecast-install"
  RMDir "$INSTDIR"
  !insertmacro Fail 41 "Setup could not create the Windows installation metadata. The incomplete installation was removed."

  metadata_rollback_failed:
  !insertmacro Fail 42 "Setup could not finish or restore the previous application payload. Run Setup again to recover it."

  previous_cleanup_after_install_failed:
  !insertmacro Fail 43 "TextRecast was updated, but its previous rollback payload could not be removed. Run Setup again after closing TextRecast."

  install_complete:
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  ClearErrors
  FileOpen $0 "$INSTDIR\.textrecast-install" r
  IfErrors uninstall_marker_invalid
  FileRead $0 $1
  FileClose $0
  StrCmp $1 "${PRODUCT_ID}" uninstall_allowed

  uninstall_marker_invalid:
  !insertmacro Fail 50 "The TextRecast installation marker is missing or invalid. Application files were not removed."

  uninstall_allowed:
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast"
  DeleteRegKey HKCU "Software\TextRecast"
  Delete "$SMPROGRAMS\TextRecast\TextRecast.lnk"
  RMDir "$SMPROGRAMS\TextRecast"
  RMDir /r "$INSTDIR"
SectionEnd
