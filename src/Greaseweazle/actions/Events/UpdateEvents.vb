Namespace Greaseweazle.Actions

    ' Identifies which firmware slot an Update operation is targeting.
    ' Bootloader updates have a tighter ack-failure mode (recovery is
    ' nearly impossible without a programmer), so the CLI renders a
    ' different "** UPDATE FAILED" footer when this is Bootloader.
    Public Enum UpdateTarget
        MainFirmware = 0
        Bootloader = 1
    End Enum

    ' Raised once by UpdateCommand right before issuing the GitHub asset
    ' fetch (or right before opening the local --file). Subscribers
    ' render Python's "Downloading latest firmware: NAME" line so the
    ' user sees what's in flight while the network call is running.
    Public NotInheritable Class UpdateDownloadStartedEventArgs
        Inherits EventArgs

        Public Sub New(payloadName As String)
            Me.PayloadName = payloadName
        End Sub

        ' Bare filename of the .upd asset (e.g. "greaseweazle-firmware-v1.0.upd").
        Public ReadOnly Property PayloadName As String

    End Class

    ' Raised once after ExtractUpdate has selected a payload that
    ' matches the connected device, before any flash is touched.
    ' Subscribers render Python's "Updating Bootloader to version
    ' M.N..." or "Updating Main Firmware to version M.N..." line.
    Public NotInheritable Class UpdateStartedEventArgs
        Inherits EventArgs

        Public Sub New(target As UpdateTarget, major As Integer, minor As Integer)
            Me.Target = target
            Me.Major = major
            Me.Minor = minor
        End Sub

        Public ReadOnly Property Target As UpdateTarget
        Public ReadOnly Property Major As Integer
        Public ReadOnly Property Minor As Integer

    End Class

End Namespace
