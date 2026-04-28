Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Pure renderer for the SectorSummaryGrid carried by Convert (and
    ' later Read). Produces Python's "Cyl-> / H. S: / .X" block plus
    ' the trailing "Found N sectors of M (P%)" tally. Skips the
    ' entire block when the grid is empty (matches Python's behaviour).
    Public NotInheritable Class SectorSummaryFormatter

        Private Sub New()
        End Sub

        Public Shared Sub Render(grid As SectorSummaryGrid, output As TextWriter)
            If grid Is Nothing OrElse Not grid.HasContent Then Return

            Dim tens As String = "Cyl-> "
            Dim p = -1
            For Each c In grid.Cyls
                tens &= If(c \ 10 = p, " ", (c \ 10).ToString(CultureInfo.InvariantCulture))
                p = c \ 10
            Next
            output.WriteLine(tens)

            Dim ones As String = "H. S: "
            For Each c In grid.Cyls
                ones &= (c Mod 10).ToString(CultureInfo.InvariantCulture)
            Next
            output.WriteLine(ones)

            For Each row In grid.Rows
                Dim line = String.Format(CultureInfo.InvariantCulture, "{0}.{1,2}: ", row.Head, row.Sector)
                For Each cell In row.Cells
                    Select Case cell
                        Case SectorSummaryCell.Empty : line &= " "
                        Case SectorSummaryCell.Good : line &= "."
                        Case SectorSummaryCell.Bad : line &= "X"
                    End Select
                Next
                output.WriteLine(line)
            Next

            If grid.TotalSectors <> 0 Then
                output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                               "Found {0} sectors of {1} ({2}%)",
                                               grid.GoodSectors,
                                               grid.TotalSectors,
                                               (grid.GoodSectors * 100) \ grid.TotalSectors))
            End If
        End Sub

    End Class

End Namespace
