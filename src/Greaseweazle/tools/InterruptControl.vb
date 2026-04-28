Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this module mirrors the side-effect of
    ' Python's KeyboardInterrupt handling in src/greaseweazle/tools/util.py::with_drive_selected.
    '
    ' Python: SIGINT -> KeyboardInterrupt is raised on the same (main) thread that's
    ' executing the long-running USB command, so the Try/Except in with_drive_selected
    ' naturally runs usb.reset() and the Finally turns the motor off before the
    ' process exits — all on a single thread, in deterministic order.
    '
    ' .NET: Console.CancelKeyPress fires on a thread-pool thread and the process is
    ' terminated by default WITHOUT running Finally blocks. The strategy that
    ' faithfully matches Python is:
    '   1. Cancel the default termination so the worker thread keeps running.
    '   2. Close the active serial port, which unblocks the worker thread's
    '      pending SerialPort.Read with an IOException.
    '   3. Let the WORKER thread run all cleanup (usb.reset() to reopen the port
    '      then drive_motor(False) + drive_deselect()) inside WithDriveSelected.
    '      The worker thread is the same thread that issued the read_track
    '      command, so this mirrors Python's "all cleanup on one thread" model
    '      and avoids races against an in-flight USB command.
    '   4. Surface the abort as a KeyboardInterruptException that propagates up
    '      to Program.Main, which returns 1 silently (matching Python's
    '      `except KeyboardInterrupt: sys.exit(1)`).
    Public Module InterruptControl

        Private ReadOnly _lock As New Object()
        ' Held by the Ctrl-C handler thread for the entire duration of
        ' SerialPort.Close(). The worker thread's abort-cleanup code acquires
        ' it before calling Serial.Open() so the reopen doesn't race the
        ' handler's still-in-progress close (which on Windows briefly holds
        ' the COM port and would otherwise fail the reopen with
        ' UnauthorizedAccessException: "Access to the port 'COM3' is denied").
        Private ReadOnly _closeLock As New Object()
        Private _activeUnit As Unit
        Private _activeDrive As DriveSpec
        Private _handlerInstalled As Boolean
        Private _aborting As Boolean

        ' Python map: no-1:1; install once at process startup from cli/Program.Main.
        Public Sub Install()
            SyncLock _lock
                If _handlerInstalled Then
                    Return
                End If
                AddHandler Console.CancelKeyPress, AddressOf OnCancelKeyPress
                _handlerInstalled = True
            End SyncLock
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::with_drive_selected (call-site state
        ' captured so the Ctrl-C handler can drive the same cleanup path).
        Public Sub Register(unit As Unit, drive As DriveSpec)
            SyncLock _lock
                _activeUnit = unit
                _activeDrive = drive
            End SyncLock
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::with_drive_selected (clears state on
        ' normal exit so a subsequent Ctrl-C outside a drive-selected block is a no-op).
        Public Sub Unregister()
            SyncLock _lock
                _activeUnit = Nothing
                _activeDrive = Nothing
            End SyncLock
        End Sub

        ' Python map: no-1:1; flag inspected by WithDriveSelected to convert the
        ' worker thread's IOException (raised when the handler closed the port) into
        ' a KeyboardInterruptException for clean propagation.
        Public ReadOnly Property Aborted As Boolean
            Get
                SyncLock _lock
                    Return _aborting
                End SyncLock
            End Get
        End Property

        Private Sub OnCancelKeyPress(sender As Object, e As ConsoleCancelEventArgs)
            Dim unit As Unit
            SyncLock _lock
                If _aborting Then
                    ' A previous Ctrl-C is already being handled. Let the runtime's
                    ' default behaviour terminate the process if the user insists.
                    Return
                End If
                _aborting = True
                unit = _activeUnit
            End SyncLock

            If unit Is Nothing OrElse unit.Serial Is Nothing Then
                ' No device-side cleanup to do (e.g. `convert` runs without a
                ' USB unit, or Ctrl-C fires before/after with_drive_selected).
                ' Leave e.Cancel at its default False so the runtime's normal
                ' Ctrl-C termination kicks in — otherwise the worker keeps
                ' running because we suppressed Win32 CTRL_C termination.
                Return
            End If

            ' Cancel default Win32 CTRL_C termination so device cleanup runs
            ' to completion on the worker thread.
            e.Cancel = True

            ' Close the serial port. This is the ONLY action we take from the
            ' handler thread — it unblocks the worker thread's pending Read with
            ' an IOException so the same thread that issued the long-running USB
            ' command can run usb.reset() (reopen) + drive_motor(False) +
            ' drive_deselect() in deterministic order, exactly like Python's
            ' KeyboardInterrupt flow on the main thread.
            '
            ' We deliberately DO NOT send DriveMotor/DriveDeselect from this
            ' thread — that would race against the worker's in-flight USB
            ' command and may corrupt the protocol.
            '
            ' We hold _closeLock for the FULL duration of Close() so the worker
            ' thread's abort-cleanup code (which calls WaitForCloseComplete()
            ' before reopening the port) blocks until Windows has fully
            ' released the COM port. Without this serialisation the worker's
            ' Open() races the still-in-progress Close() and fails with
            ' UnauthorizedAccessException "Access to the port 'COMx' is denied".
            SyncLock _closeLock
                Try
                    unit.Serial.Close()
                Catch
                End Try
            End SyncLock
        End Sub

        ' Python map: no-1:1; called by WithDriveSelected before reopening the
        ' serial port on the abort path. Acquiring _closeLock here blocks
        ' until the handler thread's in-flight Close() (held under the same
        ' lock) has fully released the COM port at the OS level, preventing
        ' an "Access to the port 'COMx' is denied" race when the worker
        ' tries to reopen.
        Public Sub WaitForCloseComplete()
            SyncLock _closeLock
                ' Acquiring + releasing is enough — the handler holds this for
                ' the entire Close() call.
            End SyncLock
        End Sub

    End Module

End Namespace
