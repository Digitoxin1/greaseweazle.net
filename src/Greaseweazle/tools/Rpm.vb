Imports Greaseweazle.Core

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `rpm` action.
    Public Class RpmOptions
        Public Property Nr As Integer
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/rpm.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Rpm

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/rpm.py::speed_str
        Public Shared Function SpeedString(timePerRev As Double) As String
            Return String.Format(
                Globalization.CultureInfo.InvariantCulture,
                "Rate: {0:F3} rpm ; Period: {1:F3} ms",
                60.0 / timePerRev,
                timePerRev * 1000.0)
        End Function

        ' Python map: src/greaseweazle/tools/rpm.py::print_rpm
        Public Shared Function PrintRpm(timePerRev As IReadOnlyList(Of Double)) As List(Of String)
            Dim output As New List(Of String)()
            If timePerRev Is Nothing OrElse timePerRev.Count <= 1 Then
                Return output
            End If

            Dim minValue = timePerRev.Min()
            Dim maxValue = timePerRev.Max()
            Dim meanValue = timePerRev.Average()
            Dim sorted = timePerRev.OrderBy(Function(x) x).ToList()
            Dim median = sorted(sorted.Count \ 2)

            output.Add("FASTEST:  " & SpeedString(minValue))
            output.Add("Ar.Mean:  " & SpeedString(meanValue))
            output.Add("Median:   " & SpeedString(median))
            output.Add("SLOWEST:  " & SpeedString(maxValue))
            Return output
        End Function


    End Class

End Namespace
