Namespace Greaseweazle.Actions

    ' High-level outcome of an UpdateCommand.Run.
    Public Enum UpdateOutcome
        ' --test was set; no USB activity took place.
        DryRun = 0

        ' Device was flashed with the new payload and acknowledged the
        ' write (ack == 0 from UpdateMainFirmware/UpdateBootloader).
        Completed = 1

        ' The flash command returned a non-zero ack: the device is in a
        ' partially-written state. CLI surfaces a "** UPDATE FAILED"
        ' message; library consumers can read AckCode for diagnostics.
        Failed = 2

        ' Device was already running a version >= the payload's, and
        ' --force was not specified. No flash was attempted.
        Skipped = 3
    End Enum

    ' Strongly-typed return value of UpdateCommand.Run. Library consumers
    ' branch on Outcome to decide what UI state to surface; the CLI
    ' formatter renders the matching legacy text block. CmdError still
    ' propagates for unrecoverable USB errors (the formatter inspects
    ' ex.Code for the special OutOfFlash/OutOfSRAM messages).
    Public NotInheritable Class UpdateSummary

        Public Sub New(target As UpdateTarget,
                       outcome As UpdateOutcome,
                       appliedMajor As Integer,
                       appliedMinor As Integer,
                       deviceMajor As Integer,
                       deviceMinor As Integer,
                       ackCode As Integer,
                       needsUnplug As Boolean)
            Me.Target = target
            Me.Outcome = outcome
            Me.AppliedMajor = appliedMajor
            Me.AppliedMinor = appliedMinor
            Me.DeviceMajor = deviceMajor
            Me.DeviceMinor = deviceMinor
            Me.AckCode = ackCode
            Me.NeedsUnplug = needsUnplug
        End Sub

        Public ReadOnly Property Target As UpdateTarget
        Public ReadOnly Property Outcome As UpdateOutcome

        ' Version of the payload that was (or would have been) applied.
        ' Populated for Completed and Failed (and for Skipped — these
        ' carry the version the user asked for vs DeviceMajor/Minor).
        Public ReadOnly Property AppliedMajor As Integer
        Public ReadOnly Property AppliedMinor As Integer

        ' Version the device is currently running. Only meaningful for
        ' Outcome = Skipped (Python's "Device is already running version
        ' M.N." line); zero in other cases.
        Public ReadOnly Property DeviceMajor As Integer
        Public ReadOnly Property DeviceMinor As Integer

        ' Non-zero ack returned by the flash command. Only meaningful
        ' for Outcome = Failed.
        Public ReadOnly Property AckCode As Integer

        ' True for a successful Main Firmware update on a non-jumperless
        ' board: the user must remove the Update Jumper before next
        ' boot. Always False for Bootloader updates and DryRun/Skipped
        ' outcomes.
        Public ReadOnly Property NeedsUnplug As Boolean

        Friend Shared Function ForDryRun(target As UpdateTarget) As UpdateSummary
            Return New UpdateSummary(target, UpdateOutcome.DryRun, 0, 0, 0, 0, 0, False)
        End Function

        Friend Shared Function ForCompleted(target As UpdateTarget,
                                            major As Integer,
                                            minor As Integer,
                                            needsUnplug As Boolean) As UpdateSummary
            Return New UpdateSummary(target, UpdateOutcome.Completed, major, minor, 0, 0, 0, needsUnplug)
        End Function

        Friend Shared Function ForFailed(target As UpdateTarget,
                                         major As Integer,
                                         minor As Integer,
                                         ackCode As Integer) As UpdateSummary
            Return New UpdateSummary(target, UpdateOutcome.Failed, major, minor, 0, 0, ackCode, False)
        End Function

        Friend Shared Function ForSkipped(target As UpdateTarget,
                                          appliedMajor As Integer,
                                          appliedMinor As Integer,
                                          deviceMajor As Integer,
                                          deviceMinor As Integer) As UpdateSummary
            Return New UpdateSummary(target, UpdateOutcome.Skipped, appliedMajor, appliedMinor,
                                     deviceMajor, deviceMinor, 0, False)
        End Function

    End Class

End Namespace
