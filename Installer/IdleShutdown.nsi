; IdleShutdown.nsi
;
; This script should be compiled using the NullSoft Install System (NSIS 2.46).
; Download from:  http://nsis.sourceforge.net/Download
;
;--------------------------------

!ifdef HAVE_UPX
!packhdr tmp.dat "upx\upx -9 tmp.dat"
!endif

!ifdef NOCOMPRESS
SetCompress off
!endif

;--------------------------------

!include "MUI.nsh"

Name "Idle Shutdown"
Caption "Orvado Idle Shutdown"
Icon "${NSISDIR}\Contrib\Graphics\Icons\nsis1-install.ico"
OutFile "IdleShutdownSetup.exe"

SetDateSave on
SetDatablockOptimize on
CRCCheck on
SilentInstall normal
; BGGradient 000000 800000 FFFFFF
; InstallColors FF8080 000030
XPStyle on

InstallDir "$PROGRAMFILES\Orvado\Idle Shutdown"
InstallDirRegKey HKLM "Software\Orvado\Idle Shutdown" "Install_Dir"

CheckBitmap "${NSISDIR}\Contrib\Graphics\Checks\classic-cross.bmp"

;LicenseText "Orvado - Idle Shutdown"
;LicenseData "Orvado.rtf"

RequestExecutionLevel admin

ShowInstDetails show
;--------------------------------
;Pages
  !insertmacro MUI_PAGE_WELCOME
;  !insertmacro MUI_PAGE_LICENSE "M1_EULA.rtf"
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES

    # These indented statements modify settings for MUI_PAGE_FINISH
	# (uncomment next 5 lines to launch app after install)
;    !define MUI_FINISHPAGE_NOAUTOCLOSE
;    !define MUI_FINISHPAGE_RUN
;    !define MUI_FINISHPAGE_RUN_CHECKED
;    !define MUI_FINISHPAGE_RUN_TEXT "Start Idle Shutdown"
;    !define MUI_FINISHPAGE_RUN_FUNCTION "LaunchIdleShutdown"

;    !define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
;    !define MUI_FINISHPAGE_SHOWREADME $INSTDIR\readme.txt
  !insertmacro MUI_PAGE_FINISH

;Languages
!insertmacro MUI_LANGUAGE "English"

;--------------------------------

;Page license
;Page components
;Page directory
;Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

;--------------------------------

!ifndef NOINSTTYPES ; only if not defined
  ;InstType "Most"
  InstType "Full"
  ;InstType "More"
  ;InstType "Base"
  InstType /NOCUSTOM
  ;InstType /COMPONENTSONLYONCUSTOM
!endif

AutoCloseWindow false
ShowInstDetails show

;--------------------------------

Section ""

  ; make sure that only one instance of the installer is running
  System::Call 'kernel32::CreateMutexA(i 0, i 0, t "idleShutdownMutex") i .r1 ?e'
  Pop $R0

  StrCmp $R0 0 +3
  MessageBox MB_OK|MB_ICONEXCLAMATION "The installer is already running."
  Abort

SectionEnd

;--------------------------------

Section "" ; empty string makes it hidden, so would starting with -

  ; write reg info
  WriteRegStr HKLM "Software\Orvado\IdleShutdown" "Install_Dir" "$INSTDIR"

  ; write uninstall strings
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown" "DisplayName" "Orvado - Idle Shutdown (remove only)"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown" "UninstallString" '"$INSTDIR\IdleShutdownUninstall.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown" "Publisher" "Orvado Technologies"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown" "HelpTelephone" "(800) 663-0966"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown" "HelpLink" "http://www.orvado.com"

  SetOutPath $INSTDIR
  File /a "..\IdleShutdown\bin\Release\IdleShutdown.exe"
  File /a "..\IdleShutdown\bin\Release\IdleShutdown.exe.config"

  WriteUninstaller "IdleShutdownUninstall.exe"

  ; Now create shortcuts (no start menu)
  ;CreateDirectory "$SMPROGRAMS\Orvado"
  ;CreateDirectory "$SMPROGRAMS\Orvado\Idle Shutdown"
  ;CreateShortCut "$SMPROGRAMS\Orvado\Idle Shutdown\Log File.lnk" "$INSTDIR\IdleShutdown.log"
  ;CreateShortCut "$SMPROGRAMS\Orvado\Idle Shutdown\Uninstall.lnk" "$INSTDIR\IdleShutdownUninstall.exe"

  Call installService
  Call startService

SectionEnd

;--------------------------------

Function installService ; Install the service

	DetailPrint "Installing Service:  Orvado Idle Shutdown"
	SimpleSC::InstallService "IdleShutdown" "Orvado Idle Shutdown" "16" "2" "$INSTDIR\IdleShutdown.exe" "" "" ""
	Pop $0 ; returns an errorcode (<>0) otherwise success (0)
	IntCmp $0 0 Done +1 +1
		Push $0
		SimpleSC::GetErrorMessage
		Pop $0
		MessageBox MB_OK|MB_ICONSTOP "Unable to install the service - Reason: $0"
	Done:

FunctionEnd

;--------------------------------

Function startService ; Run the service

	DetailPrint "Starting Service:  Orvado Idle Shutdown"
	SimpleSC::StartService "IdleShutdown" "" "30"
	Pop $0 ; returns an errorcode (<>0) otherwise success (0)
	IntCmp $0 0 Done +1 +1
		Push $0
		SimpleSC::GetErrorMessage
		Pop $0
		MessageBox MB_OK|MB_ICONSTOP "Unable to start the service - Reason: $0"
	Done:

FunctionEnd

;--------------------------------

Function un.StopService ; Stop the service

	SimpleSC::GetServiceStatus "IdleShutdown"
	Pop $0 ; returns an errorcode (<>0) otherwise success (0)
	Pop $1 ; return the status of the service (See "service_status" in the parameters)

	IntCmp $0 0 CheckStatus +1 +1
		Push $0
		SimpleSC::GetErrorMessage
		Pop $0
		MessageBox MB_OK|MB_ICONSTOP "Unable to stop the service - Reason: $0"
		Goto Continue

	CheckStatus:
		; if status returned "1 - SERVICE_STOPPED", no need to do anything
		IntCmp $1 1 Done +1 +1

	Continue:
		DetailPrint "Stopping Service:  Orvado Idle Shutdown"
		SimpleSC::StopService "IdleShutdown" 1 30
		Pop $0 ; returns an errorcode (<>0) otherwise success (0)
		IntCmp $0 0 Done +1 +1
			Push $0
			SimpleSC::GetErrorMessage
			Pop $0
			MessageBox MB_OK|MB_ICONSTOP "Unable to stop the service - Reason: $0"
	Done:

FunctionEnd

;--------------------------------

Function un.RemoveService ; Remove the service

	DetailPrint "Removing Service:  Orvado Idle Shutdown"
	SimpleSC::RemoveService "IdleShutdown"
	Pop $0 ; returns an errorcode (<>0) otherwise success (0)
	IntCmp $0 0 Done +1 +1
		Push $0
		SimpleSC::GetErrorMessage
		Pop $0
		MessageBox MB_OK|MB_ICONSTOP "Unable to remove the service - Reason: $0"
	Done:

FunctionEnd

;--------------------------------

; Uninstaller

UninstallText "This will uninstall Orvado - Idle Shutdown. Hit next to continue."
UninstallIcon "${NSISDIR}\Contrib\Graphics\Icons\nsis1-uninstall.ico"

Section "Uninstall"

	SimpleSC::ExistsService "IdleShutdown"
	Pop $0 ; returns an errorcode if the service doesn't exist (<>0)/service exists (0)
	IntCmp $0 0 +1 NoService NoService
		Call un.StopService
		Call un.RemoveService

	NoService:
		DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\IdleShutdown"
		DeleteRegKey HKLM "SOFTWARE\Orvado\IdleShutdown"
		;DeleteRegKey /ifempty HKLM "SOFTWARE\Orvado\IdleShutdown"
		;${unregisterExtension} ".bak" "SQL Server Backup"

		;Delete "$INSTDIR\silent.nsi"
		Delete "$INSTDIR\IdleShutdown.exe"
		Delete "$INSTDIR\IdleShutdown.exe.config"
		Delete "$INSTDIR\IdleShutdown.log"
		Delete "$INSTDIR\*.*"
		RMDIR "$INSTDIR"

		;MessageBox MB_YESNO|MB_ICONQUESTION "Would you like to remove the directory $INSTDIR\cpdest?" IDNO NoDelete
		;  Delete "$INSTDIR\cpdest\*.*"
		;  RMDir "$INSTDIR\cpdest" ; skipped if no
		;NoDelete:

		;RMDir "$INSTDIR\MyProjectFamily\MyProject"
		;RMDir "$INSTDIR\MyProjectFamily"
		;RMDir "$INSTDIR"

		;IfFileExists "$INSTDIR" 0 NoErrorMsg
		;  MessageBox MB_OK "Note: $INSTDIR could not be removed!" IDOK 0 ; skipped if file doesn't exist
		;NoErrorMsg:

		; Now remove shortcuts too
		;Delete "$SMPROGRAMS\Orvado\Idle Shutdown\Log File.lnk"
		;Delete "$SMPROGRAMS\Orvado\Idle Shutdown\Uninstall.lnk"
		;RMDIR "$SMPROGRAMS\Orvado\Idle Shutdown"
SectionEnd

;Function LaunchIdleShutdown
  ; This function is never called - disabled launching app after install (it's a service)
;  ExecShell "" "$SMPROGRAMS\Orvado\Idle Shutdown\Log File.lnk"
;FunctionEnd