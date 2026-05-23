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
        Private Shared ReadOnly _majorMinor As String = ResolveMajorMinor(_value)

        Public Shared ReadOnly Property Value As String
            Get
                Return _value
            End Get
        End Property

        ' Two-component "major.minor" view of Value, matching the form Python's
        ' `__version__` takes (e.g. "1.23"). Image writers that need to stamp
        ' provenance into fixed-width or Python-compatible headers (KryoFlux OOB
        ' info block, EDSK creator field) should prefer this over Value so the
        ' patch/build component from InformationalVersion ("1.23.3") or
        ' AssemblyName.Version ("1.23.0.0") doesn't leak in.
        Public Shared ReadOnly Property MajorMinor As String
            Get
                Return _majorMinor
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

        Private Shared Function ResolveMajorMinor(full As String) As String
            If String.IsNullOrEmpty(full) Then Return full
            Dim parts = full.Split("."c)
            If parts.Length >= 2 Then
                Return parts(0) & "." & parts(1)
            End If
            Return full
        End Function

    End Class

End Namespace
