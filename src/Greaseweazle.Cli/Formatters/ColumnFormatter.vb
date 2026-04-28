Namespace Greaseweazle.Cli.Formatters

    ' Pure-presentation helper that columnifies a list of strings for the
    ' CLI's "Known formats" / "Known suffixes" / catalogue output. Lives
    ' alongside the other CLI formatters because columnification is a
    ' presentation concern, not part of the library contract.
    Public NotInheritable Class ColumnFormatter

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::columnify
        Public Shared Function Columnify(strings As IEnumerable(Of String),
                                         Optional columns As Integer = 80,
                                         Optional separator As Integer = 2) As String
            Dim values = strings.ToList()
            If values.Count = 0 Then
                Return String.Empty
            End If

            Dim maxLen = values.Max(Function(s) s.Length) + separator
            Dim perRow = Math.Max(1, columns \ maxLen)
            Dim lines As New List(Of String)()

            For index = 0 To values.Count - 1 Step perRow
                Dim row = values.Skip(index).Take(perRow).ToList()
                While row.Count < perRow
                    row.Add(String.Empty)
                End While
                lines.Add(String.Concat(row.Select(Function(s) s.PadRight(maxLen))))
            Next

            Return String.Join(vbLf, lines)
        End Function

    End Class

End Namespace
