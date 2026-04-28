Namespace Greaseweazle.Actions

    ' Strongly-typed return value of DelaysCommand.Run.
    '
    ' All values reflect what the device actually reports after the optional
    ' --select/--step/... overrides are applied. The CLI front-end's
    ' DelaysFormatter writes them to the console; library consumers can
    ' inspect each field directly (e.g. a GUI showing them in form controls).
    '
    ' The three nullable fields gate on the firmware's reported parameter
    ' block size: older devices only return five delays (paramSize=10),
    ' newer ones add Pre-Write (12), Post-Write (14), and Index-Mask (16).
    ' A `Nothing` value means the running firmware doesn't expose that
    ' delay at all — the formatter omits the corresponding line.
    Public NotInheritable Class DelaysResult

        Public Sub New(selectMicros As Integer,
                       stepMicros As Integer,
                       settleMillis As Integer,
                       motorMillis As Integer,
                       watchdogMillis As Integer,
                       preWriteMicros As Integer?,
                       postWriteMicros As Integer?,
                       indexMaskMicros As Integer?)
            Me.SelectDelayMicros = selectMicros
            Me.StepDelayMicros = stepMicros
            Me.SettleTimeMillis = settleMillis
            Me.MotorDelayMillis = motorMillis
            Me.WatchdogMillis = watchdogMillis
            Me.PreWriteMicros = preWriteMicros
            Me.PostWriteMicros = postWriteMicros
            Me.IndexMaskMicros = indexMaskMicros
        End Sub

        Public ReadOnly Property SelectDelayMicros As Integer
        Public ReadOnly Property StepDelayMicros As Integer
        Public ReadOnly Property SettleTimeMillis As Integer
        Public ReadOnly Property MotorDelayMillis As Integer
        Public ReadOnly Property WatchdogMillis As Integer
        Public ReadOnly Property PreWriteMicros As Integer?
        Public ReadOnly Property PostWriteMicros As Integer?
        Public ReadOnly Property IndexMaskMicros As Integer?

    End Class

End Namespace
