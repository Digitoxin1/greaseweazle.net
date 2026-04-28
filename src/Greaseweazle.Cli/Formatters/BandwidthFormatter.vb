Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Python map: src/greaseweazle/tools/bandwidth.py — turns a typed
    ' BandwidthResult into the column-aligned bytes the unified exe used
    ' to print. Library consumers that don't care about the textual form
    ' inspect BandwidthResult fields directly.
    Public NotInheritable Class BandwidthFormatter

        Private Sub New()
        End Sub

        Public Shared Sub Render(result As BandwidthResult, output As TextWriter)
            If result Is Nothing Then Return
            output.WriteLine("")
            output.WriteLine(String.Format("{0,-19}{1,-7}/   {2,-7}/   {3,-7}", "", "Min.", "Mean", "Max."))
            output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                           "Write Bandwidth: {0,8:F3} / {1,8:F3} / {2,8:F3} Mbps",
                                           result.WriteRow.Min, result.WriteRow.Mean, result.WriteRow.Max))
            If result.WriteGarbled Then
                output.WriteLine("ERROR: USB write data garbled (Host -> Device)")
                Return
            End If
            output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                           "Read Bandwidth:  {0,8:F3} / {1,8:F3} / {2,8:F3} Mbps",
                                           result.ReadRow.Min, result.ReadRow.Mean, result.ReadRow.Max))
            If result.ReadGarbled Then
                output.WriteLine("ERROR: USB read data garbled (Device -> Host)")
                Return
            End If

            output.WriteLine("")
            output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                           "Estimated Consistent Min. Bandwidth: {0:F3} Mbps",
                                           result.Summary.EstimatedMinMbps))
            If result.Summary.BelowRequirement Then
                output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                               " -> **WARNING** BELOW REQUIRED MIN.: {0:F3} Mbps",
                                               result.Summary.RequiredMinMbps))
            Else
                output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                               " -> Max. Flux Rate: {0:F3} Msamples/sec",
                                               result.Summary.MaxFluxRateMsps))
                output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                               " -> Min. Ave. Flux: {0:F3} us",
                                               result.Summary.MinAvgFluxUs))
            End If
        End Sub

    End Class

End Namespace
