Idle-Shutdown
=============

Windows service to force system shutdown after idle timeout.  This program was
created as a method to automatically shutdown a Windows PC after a child has left
it on for an extended period of time.  Be careful with this utility because it
may cause the user to lose work as the result of an unplanned shutdown.  Use this
software at your own risk.

The software is writtine C-Sharp using Visual Studio 2013.  The IdleShutdown
project defines a Windows Service which checks the idle time on the computer and
when the total idle time reaches a set limit (20 minutes by default), the service
forcibly shuts down the computer.  This is done by calling the shutdown executable
in the Windows system folder.  If the system does not shut down cleanly within
sixty seconds, the service will then begin killing all user processes that are
currently running (with the exception of shutdown.exe itself).

If these steps fail to shutdown the computer, the service will retry a couple of
minutes later.  The amount of idle time necessary to force an automatic shutdown
is configured in the application config file (IdleShutdown.exe.config after the
program is installed).

The installer uses the NullSoft Install System (NSIS) to build an installer.
You will also need to download the Service Control (SC) plugin to the NullSoft
installer in order to build a working installer.