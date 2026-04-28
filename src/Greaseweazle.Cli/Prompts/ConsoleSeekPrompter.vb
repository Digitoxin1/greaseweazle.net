Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Prompts

    ' Console-backed implementation of ISeekPrompter. Writes the supplied
    ' prompt text to stdout and reads a single line from stdin; only the
    ' literal answer "Yes" (case-sensitive) confirms the seek. When stdin
    ' is redirected (no interactive TTY) Confirm returns False after
    ' printing the prompt — same default-deny behaviour the unified exe
    ' had so a piped invocation can't accidentally run an extreme seek.
    Public NotInheritable Class ConsoleSeekPrompter
        Implements ISeekPrompter

        Private ReadOnly _output As TextWriter
        Private ReadOnly _input As TextReader

        Public Sub New(output As TextWriter, input As TextReader)
            _output = output
            _input = input
        End Sub

        Public Function Confirm(promptText As String) As Boolean Implements ISeekPrompter.Confirm
            _output.Write(promptText)
            Dim canRead As Boolean
            If _input IsNot Nothing AndAlso Not Object.ReferenceEquals(_input, Console.In) Then
                ' A caller wired up an explicit non-Console reader (test
                ' harness, future GUI host with its own input pump) — let
                ' it answer.
                canRead = True
            Else
                Try
                    canRead = Not Console.IsInputRedirected
                Catch
                    canRead = False
                End Try
            End If
            If Not canRead Then Return False
            Dim answer = _input.ReadLine()
            Return String.Equals(answer, "Yes", StringComparison.Ordinal)
        End Function

    End Class

End Namespace
