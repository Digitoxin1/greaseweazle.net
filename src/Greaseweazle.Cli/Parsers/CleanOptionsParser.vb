Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb clean`. Migrated from
    ' Clean.BuildRuntimePreview; eagerly builds the per-pass cylinder
    ' sequences via Clean.BuildPassSequences so the library's CleanCommand
    ' just walks the pre-computed schedule.
    Public NotInheritable Class CleanOptionsParser

        Private Const ActionName As String = "clean"

        Private Sub New()
        End Sub

        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As CleanOptions
            Dim cyls = 80
            Dim passes = 3
            Dim linger = 100
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim positionals As New List(Of String)()

            Dim i = 0
            While i < args.Count
                Dim rawToken = args(i)
                If String.Equals(rawToken, "--", StringComparison.Ordinal) Then
                    For j = i To args.Count - 1
                        positionals.Add(args(j))
                    Next
                    Exit While
                End If
                Dim token = rawToken
                Dim inlineValue As String = Nothing
                Dim equalsIndex = rawToken.IndexOf("="c)
                If rawToken.StartsWith("--", StringComparison.Ordinal) AndAlso equalsIndex > 2 Then
                    token = rawToken.Substring(0, equalsIndex)
                    inlineValue = rawToken.Substring(equalsIndex + 1)
                End If
                Select Case token
                    Case "--cyls"
                        cyls = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--passes"
                        passes = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--linger"
                        linger = Integer.Parse(TakeOptionValue(args, i, token, inlineValue), Globalization.CultureInfo.InvariantCulture)
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
                        End If
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Argparse(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If positionals.Count > 0 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            ErrorHandling.Check(cyls >= 0, "--cyls must be >= 0")
            ErrorHandling.Check(passes >= 0, "--passes must be >= 0")
            ErrorHandling.Check(linger >= 0, "--linger must be >= 0")

            Dim lines As New List(Of String)()
            Dim sequences = Greaseweazle.Tools.Clean.BuildPassSequences(cyls, passes)
            For p = 0 To sequences.Count - 1
                Dim suffix = If(sequences(p).Count = 0, "", String.Join(" ", sequences(p)) & " ")
                lines.Add(String.Format("Pass {0}: {1}", p, suffix))
            Next
            Return New CleanOptions With {
                .Cyls = cyls,
                .PassLines = lines,
                .Sequences = sequences,
                .LingerMs = linger,
                .Live = live,
                .Device = device,
                .Drive = ResolveDrive(driveToken)
            }
        End Function

        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName)
            Return args(index)
        End Function

        Private Shared Function ResolveDrive(token As String) As DriveSpec
            Try
                Return ParserHelpers.Drive(token)
            Catch ex As ArgumentException
                Argparse(ex.Message)
                ' Argparse always throws - the Return below is unreachable but
                ' the compiler can't prove it, so the assignment to a Nothing
                ' DriveSpec satisfies definite-assignment without ever running.
                Return Nothing
            End Try
        End Function

    End Class

End Namespace
