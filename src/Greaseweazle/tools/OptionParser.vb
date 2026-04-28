Namespace Greaseweazle.Shared

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration OptionParser)
    Public NotInheritable Class OptionParser

        ' Python map: src/greaseweazle/tools/util.py::(no direct 1:1 symbol; VB static utility class constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::split_opts
        Public Shared Function SplitOpts(sequence As String) As Tuple(Of String, Dictionary(Of String, String))
            Dim parts = sequence.Split(New String() {"::"}, StringSplitOptions.None)
            Dim name = parts(0)
            Dim options As New Dictionary(Of String, String)()

            For i = 1 To parts.Length - 1
                For Each chunk In parts(i).Split(":"c)
                    Dim token = chunk
                    If token.Length = 0 Then
                        Continue For
                    End If

                    Dim split = token.Split("="c)
                    Dim opt As String
                    Dim val As String
                    If split.Length = 2 Then
                        opt = split(0)
                        val = split(1)
                    Else
                        opt = token
                        val = "yes"
                    End If

                    If opt.Length <> 0 Then
                        options(opt) = val
                    End If
                Next
            Next

            Return Tuple.Create(name, options)
        End Function

    End Class

End Namespace
