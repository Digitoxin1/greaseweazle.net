Namespace Greaseweazle.Actions

    ' Discriminator for PinResult.
    Public Enum PinResultKind
        ' Dry-run `get --test`: nothing to surface to the caller because the
        ' level can only come from hardware (Python pin.py:51 only calls
        ' pin_get when args.live is true). Library consumers ignore this
        ' result; the CLI prints nothing.
        NoOp = 0

        ' The action invocation supplied no recognised subcommand. The CLI
        ' interprets this as "print usage and exit 1"; library consumers
        ' simply observe the kind and ignore the (unset) Pin/Level fields.
        UsageRequested = 1

        ' `usb.SetPin` succeeded (or, in --test dry-run mode, would have).
        ' Python pin.py:30-31 prints `Pin %u is set %s`.
        PinSet = 2

        ' `usb.GetPin` returned. Python pin.py:55-56 prints `Pin %u is %s`.
        PinValue = 3
    End Enum

    ' Strongly-typed return value of PinCommand.Run.
    '
    ' The library produces no text; the CLI front-end's PinFormatter inspects
    ' Kind and renders the payload to match Python's print output. Other
    ' consumers (a GUI, an automation script) can read Pin/Level directly.
    Public NotInheritable Class PinResult

        Public Sub New(kind As PinResultKind, pin As Integer, level As Boolean)
            Me.Kind = kind
            Me.Pin = pin
            Me.Level = level
        End Sub

        Public ReadOnly Property Kind As PinResultKind

        ' Pin number the result refers to. Meaningful when Kind is PinSet or
        ' PinValue; zero otherwise (UsageRequested has no pin context).
        Public ReadOnly Property Pin As Integer

        ' Pin level. Meaningful when Kind is PinSet or PinValue.
        Public ReadOnly Property Level As Boolean

        Friend Shared Function NoOp() As PinResult
            Return New PinResult(PinResultKind.NoOp, 0, False)
        End Function

        Friend Shared Function Usage() As PinResult
            Return New PinResult(PinResultKind.UsageRequested, 0, False)
        End Function

        Friend Shared Function [Set](pin As Integer, level As Boolean) As PinResult
            Return New PinResult(PinResultKind.PinSet, pin, level)
        End Function

        Friend Shared Function Value(pin As Integer, level As Boolean) As PinResult
            Return New PinResult(PinResultKind.PinValue, pin, level)
        End Function

    End Class

End Namespace
