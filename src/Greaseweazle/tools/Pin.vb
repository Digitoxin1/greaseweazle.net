Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `pin` action. Pure POCO - the CLI's
    ' PinOptionsParser produces this from argv; library consumers may
    ' construct one directly (e.g. .Mode = "set", .Pin = 2, .Level = True).
    Public Class PinOptions
        Public Property Mode As String
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
        Public Property Pin As Integer
        Public Property Level As Boolean
    End Class

    ' Python map: src/greaseweazle/tools/pin.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Pin

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/pin.py::_pin_get
        Public Shared Function PinGetInner(usbClient As Unit, pin As Integer) As Boolean
            Return usbClient.GetPin(pin)
        End Function

    End Class

End Namespace
