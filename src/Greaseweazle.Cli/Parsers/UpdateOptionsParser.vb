Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb update`. Migrated from
    ' Update.BuildRuntimePreview. The library's
    ' Update.ValidateTagFileExclusion check still runs at parse time so
    ' the same `--file` + `--tag` exclusion error fires from the CLI.
    Public NotInheritable Class UpdateOptionsParser

        Private Const ActionName As String = "update"

        Private Sub New()
        End Sub

        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As UpdateOptions
            Dim fileValue As String = Nothing
            Dim tagValue As String = Nothing
            Dim force = False
            Dim bootloader = False
            Dim live = True
            Dim device As String = Nothing
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
                    Case "--file"
                        fileValue = TakeOptionValue(args, i, token, inlineValue)
                    Case "--tag"
                        tagValue = TakeOptionValue(args, i, token, inlineValue)
                    Case "--device"
                        device = TakeOptionValue(args, i, token, inlineValue)
                    Case "--force"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        force = True
                    Case "--bootloader"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        bootloader = True
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
            Greaseweazle.Tools.Update.ValidateTagFileExclusion(fileValue, tagValue)

            Return New UpdateOptions With {
                .FileValue = fileValue,
                .TagValue = tagValue,
                .Force = force,
                .Bootloader = bootloader,
                .Live = live,
                .Device = device
            }
        End Function

        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("-", StringComparison.Ordinal) Then
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

    End Class

End Namespace
