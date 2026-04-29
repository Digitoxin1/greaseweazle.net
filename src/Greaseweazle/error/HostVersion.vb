Imports System.Reflection

Namespace Greaseweazle.Core

    ' Resolves the Greaseweazle library version string used in image-file
    ' provenance fields (SCP footer "Greaseweazle <ver>", IMD comment header,
    ' "Host Tools" line printed by `info`).
    '
    ' Python map: `from greaseweazle import __version__` resolves to the
    ' single string defined in `src/greaseweazle/__init__.py` (e.g. "1.23").
    ' We mirror that by preferring AssemblyInformationalVersionAttribute
    ' (set in Directory.Build.props as <InformationalVersion>) and falling
    ' back to AssemblyName.Version which is "Major.Minor.Build.Revision"
    ' style ("1.23.0.0") - longer than Python but never empty.
    Public NotInheritable Class HostVersion

        Private Sub New()
        End Sub

        Private Shared ReadOnly _value As String = Resolve()

        Public Shared ReadOnly Property Value As String
            Get
                Return _value
            End Get
        End Property

        Private Shared Function Resolve() As String
            Dim asm = GetType(HostVersion).Assembly
            Dim infoAttr = TryCast(CustomAttributeExtensions.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)(asm),
                                   AssemblyInformationalVersionAttribute)
            If infoAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(infoAttr.InformationalVersion) Then
                Return infoAttr.InformationalVersion
            End If
            Dim ver = asm.GetName().Version
            If ver IsNot Nothing Then
                Return ver.ToString()
            End If
            Return "0.0"
        End Function

    End Class

End Namespace
