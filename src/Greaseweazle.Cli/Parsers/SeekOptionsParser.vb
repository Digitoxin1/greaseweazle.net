Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb seek`. Migrated from Seek.BuildRuntimePreview;
    ' preserves the special handling for negative-cylinder positionals
    ' (otherwise `-3` would be rejected as an unknown flag).
    Public NotInheritable Class SeekOptionsParser

        Private Const ActionName As String = "seek"

        Private Sub New()
        End Sub

        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As SeekOptions
            Dim force = False
            Dim motorOn = False
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
                    Case "--force"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        force = True
                    Case "--motor-on"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        motorOn = True
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) AndAlso
                           Not IsSignedIntegerToken(rawToken) Then
                            Argparse(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If positionals.Count = 0 Then
                Argparse("the following arguments are required: cylinder")
            ElseIf positionals.Count > 1 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(1))))
            End If
            ' Python's seek.py uses `type=lambda x: int(x)` for the positional;
            ' argparse extracts the literal parameter name from the lambda
            ' source ("x") when emitting the error message:
            '   argument cylinder: invalid x value: '<input>'
            ' Matching that wording verbatim keeps stderr byte-equal with gw.exe.
            Dim cyl As Integer
            If Not Integer.TryParse(positionals(0), Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, cyl) OrElse cyl < 0 Then
                Argparse(String.Format("argument cylinder: invalid x value: '{0}'", positionals(0)))
            End If
            Dim promptNeeded = Greaseweazle.Tools.Seek.ShouldPromptForExtremeCylinder(cyl, force)
            Dim drive As DriveSpec = Nothing
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Argparse(ex.Message)
            End Try

            Return New SeekOptions With {
                .Cyl = cyl,
                .Force = force,
                .MotorOn = motorOn,
                .PromptNeeded = promptNeeded,
                .Live = live,
                .Device = device,
                .Drive = drive
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

        Private Shared Function IsSignedIntegerToken(value As String) As Boolean
            Dim parsed As Integer
            Return Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed)
        End Function

    End Class

End Namespace
