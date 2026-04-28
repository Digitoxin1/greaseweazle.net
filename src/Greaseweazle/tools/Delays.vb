Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `delays` action. Pure POCO - the CLI's
    ' DelaysOptionsParser produces this from argv. Values is keyed on the
    ' delay-slot name without the CLI's leading "--" (e.g. "step",
    ' "pre-write") so the library's algorithm stays free of CLI flag
    ' vocabulary and can translate the override into the matching
    ' UsbProtocol.Params.Delays slot.
    Public Class DelaysOptions
        Public Property Values As Dictionary(Of String, Integer)
        Public Property Live As Boolean = True
        Public Property Device As String
    End Class

    ' Python map: src/greaseweazle/tools/delays.py::Delays
    Public NotInheritable Class Delays

        ' Python map: src/greaseweazle/tools/delays.py::Delays.__init__
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/delays.py::print_info_line
        Public Shared Function PrintInfoLine(name As String, value As String, Optional tab As Integer = 0) As String
            Dim prefix = New String(" "c, Math.Max(tab, 0))
            Dim left = (name & ":").PadRight(Math.Max(14 - tab, 0))
            Return prefix & left & value
        End Function

        ' Python map: src/greaseweazle/tools/delays.py::Delays.update
        Public Shared Sub Update(usbClient As Unit, paramSize As Integer, values As UShort())
            Dim outDat(paramSize - 1) As Byte
            For i = 0 To (paramSize \ 2) - 1
                Dim packed = BitConverter.GetBytes(values(i))
                outDat(i * 2) = packed(0)
                outDat(i * 2 + 1) = packed(1)
            Next
            usbClient.SetParams(UsbProtocol.Params.Delays, outDat)
        End Sub

    End Class

End Namespace
