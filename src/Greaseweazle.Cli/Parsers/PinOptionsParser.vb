Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb pin`. Migrated from the legacy
    ' Pin.BuildRuntimePreview; preserves the existing usage/get/set
    ' subcommand dispatch and `--device`/`--drive` handling.
    Public NotInheritable Class PinOptionsParser

        Private Const ActionName As String = "pin"

        Private Sub New()
        End Sub

        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        ' Pin uses argparse subparsers (`get`, `set`), so the usage banner
        ' once a subcommand is in flight differs from the top-level pin
        ' banner. Python's argparse prints e.g.
        '   usage: gw.exe pin set [options] pin level
        '   gw.exe pin set: error: ...
        ' while a typo at the top level keeps the parent banner. The shared
        ' ParserHelpers.Argparse always uses the parent ("pin") banner, so
        ' this throws ArgparseException directly with the subparser-specific
        ' usage + action label to match gw.exe's output.
        Private Shared Sub ArgparseSub(subcommand As String, message As String)
            Dim usage = String.Format("usage: gw-vb pin {0} [options] pin{1}",
                                      subcommand,
                                      If(String.Equals(subcommand, "set", StringComparison.Ordinal), " level", ""))
            Throw New ArgparseException(String.Format("pin {0}", subcommand), usage, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As PinOptions
            If args.Count = 0 Then
                Return New PinOptions With {.Mode = "usage"}
            End If

            Dim subcommand = args(0)
            ' Anything other than `get` / `set` falls through to usage mode -
            ' Python's argparse handles unrecognised subcommands by reprinting
            ' the parent usage banner (which the CLI's PinFormatter renders).
            ' Don't strict-validate flags in that mode.
            Dim isKnownSub = String.Equals(subcommand, "set", StringComparison.Ordinal) OrElse
                             String.Equals(subcommand, "get", StringComparison.Ordinal)
            If Not isKnownSub Then
                Return New PinOptions With {.Mode = "usage"}
            End If

            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim positionals As New List(Of String)()
            Dim i = 1
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
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            ArgparseSub(subcommand, String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
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
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            ArgparseSub(subcommand, String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If String.Equals(subcommand, "set", StringComparison.Ordinal) Then
                If positionals.Count < 2 Then
                    ArgparseSub("set", "the following arguments are required: pin, level")
                ElseIf positionals.Count > 2 Then
                    ArgparseSub("set", String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(2))))
                End If
                Dim pin = ParseUInt(positionals(0), "pin", "set")
                Dim level As Boolean = False
                Try
                    level = ParserHelpers.Level(positionals(1))
                Catch ex As ArgumentException
                    ' Python wraps ParserHelpers.Level (argparse type=level)
                    ' so its error is prefixed with `argument level: ...`.
                    ArgparseSub("set", String.Format("argument level: {0}", ex.Message))
                End Try
                Return New PinOptions With {
                    .Mode = "set",
                    .Live = live,
                    .Device = device,
                    .Drive = ResolveDrive(driveToken, "set"),
                    .Pin = pin,
                    .Level = level
                }
            End If
            ' subcommand = "get" (validated above)
            If positionals.Count < 1 Then
                ArgparseSub("get", "the following arguments are required: pin")
            ElseIf positionals.Count > 1 Then
                ArgparseSub("get", String.Format("unrecognized arguments: {0}", String.Join(" ", positionals.Skip(1))))
            End If
            Dim getPin = ParseUInt(positionals(0), "pin", "get")
            Return New PinOptions With {
                .Mode = "get",
                .Live = live,
                .Device = device,
                .Drive = ResolveDrive(driveToken, "get"),
                .Pin = getPin
            }
        End Function

        Private Shared Function ParseUInt(value As String, fieldName As String, subcommand As String) As Integer
            Dim parsed As Integer
            ' Python's pin.py: positional uses `type=lambda x: int(x)`; argparse
            ' surfaces the lambda's parameter name ("x") in the error message.
            ' Mirror gw.exe's wording verbatim.
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                ArgparseSub(subcommand, String.Format("argument {0}: invalid x value: '{1}'", fieldName, value))
            End If
            Return parsed
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

        Private Shared Function ResolveDrive(token As String, subcommand As String) As DriveSpec
            Try
                Return ParserHelpers.Drive(token)
            Catch ex As ArgumentException
                ArgparseSub(subcommand, ex.Message)
                Return Nothing
            End Try
        End Function

    End Class

End Namespace
