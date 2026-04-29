Namespace Greaseweazle.Core

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration ErrorHandling)
    Public NotInheritable Class ErrorHandling

        ' Python map: src/greaseweazle/error.py::(no direct 1:1 symbol; VB utility class constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/error.py::check
        Public Shared Sub Check(predicate As Boolean, description As String)
            If Not predicate Then
                Throw New FatalException(description)
            End If
        End Sub

    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration FatalException)
    Public Class FatalException
        Inherits Exception

        ' Python map: src/greaseweazle/error.py::Fatal.__init__
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ' Python map: src/greaseweazle/error.py::Fatal
    Public Class Fatal
        Inherits FatalException

        ' Python map: src/greaseweazle/error.py::Fatal.__init__
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ' Raised when a caller asks for a disk format that isn't registered.
    ' The base Message is a short domain-level summary so non-CLI library
    ' consumers can still display ex.Message directly. The CLI front-end
    ' recognises this subclass and additionally renders the catalogue of
    ' known formats (which is a presentation concern and lives in the CLI).
    ' KnownFormats is optional - non-Nothing means the throw site enumerated
    ' the registry; the CLI columnifies it. Nothing means the throw site
    ' didn't have the list handy and the CLI should leave it out.
    Public Class UnknownFormatException
        Inherits FatalException

        Public ReadOnly Property FormatName As String
        Public ReadOnly Property KnownFormats As IReadOnlyList(Of String)

        Public Sub New(formatName As String)
            Me.New(formatName, Nothing)
        End Sub

        Public Sub New(formatName As String, knownFormats As IReadOnlyList(Of String))
            MyBase.New(String.Format("Unknown format '{0}'", formatName))
            Me.FormatName = formatName
            Me.KnownFormats = knownFormats
        End Sub
    End Class

    ' Raised by ImageTypeRegistry (and similar code paths) when a file's
    ' extension isn't recognised. KnownSuffixes is optional - non-Nothing
    ' means the throw site enumerated the registry; the CLI columnifies it.
    Public Class UnrecognisedSuffixException
        Inherits FatalException

        Public ReadOnly Property FileName As String
        Public ReadOnly Property Suffix As String
        Public ReadOnly Property KnownSuffixes As IReadOnlyList(Of String)

        Public Sub New(fileName As String, suffix As String)
            Me.New(fileName, suffix, Nothing)
        End Sub

        Public Sub New(fileName As String, suffix As String, knownSuffixes As IReadOnlyList(Of String))
            MyBase.New(String.Format("{0}: Unrecognised file suffix '{1}'", fileName, suffix))
            Me.FileName = fileName
            Me.Suffix = suffix
            Me.KnownSuffixes = knownSuffixes
        End Sub
    End Class

    ' Raised by UsbModeCheck when the device is in firmware-update mode but
    ' the requested action isn't `update`. Carries the structured fields
    ' the CLI uses to render the bullet-point hint block (whether the
    ' update jumper is fitted, and which pins to remove it from for the
    ' connected hardware model).
    Public Class DeviceInUpdateModeException
        Inherits FatalException

        Public ReadOnly Property UpdateJumpered As Boolean
        Public ReadOnly Property HwModel As Integer

        Public Sub New(updateJumpered As Boolean, hwModel As Integer)
            MyBase.New("Device is in Firmware Update Mode")
            Me.UpdateJumpered = updateJumpered
            Me.HwModel = hwModel
        End Sub
    End Class

    ' Raised by UsbModeCheck when an `update` action is attempted but the
    ' device isn't currently in firmware-update mode (and can't be switched
    ' there programmatically). Carries the fields needed for the CLI to
    ' render the print-update-instructions bullet list.
    Public Class DeviceNotInUpdateModeException
        Inherits FatalException

        Public ReadOnly Property JumperlessUpdate As Boolean
        Public ReadOnly Property HwModel As Integer

        Public Sub New(jumperlessUpdate As Boolean, hwModel As Integer)
            MyBase.New("Device is not in Firmware Update Mode")
            Me.JumperlessUpdate = jumperlessUpdate
            Me.HwModel = hwModel
        End Sub
    End Class

    ' Raised by UsbModeCheck when the connected device's firmware is too
    ' old for the host tool. Carries firmware version + jumper info so the
    ' CLI can render the same instructions block as DeviceNotInUpdateMode.
    Public Class DeviceFirmwareUnsupportedException
        Inherits FatalException

        Public ReadOnly Property Major As Integer
        Public ReadOnly Property Minor As Integer
        Public ReadOnly Property JumperlessUpdate As Boolean
        Public ReadOnly Property HwModel As Integer

        Public Sub New(major As Integer, minor As Integer, jumperlessUpdate As Boolean, hwModel As Integer)
            MyBase.New(String.Format(Globalization.CultureInfo.InvariantCulture,
                                     "Device firmware version {0}.{1} is unsupported",
                                     major, minor))
            Me.Major = major
            Me.Minor = minor
            Me.JumperlessUpdate = jumperlessUpdate
            Me.HwModel = hwModel
        End Sub
    End Class

    ' Raised by UsbReopen when the firmware-mode switch succeeded but the
    ' device couldn't be re-discovered afterwards. CLI adds the
    ' USB-hub-hint follow-up line.
    Public Class DeviceNotFoundAfterModeSwitchException
        Inherits FatalException

        Public Sub New()
            MyBase.New("Could not find the Greaseweazle device after switching firmware mode.")
        End Sub
    End Class

    ' Raised by the CAPS/IPF backend probe when neither the x64 nor the
    ' generic loader can find the SPS/CAPSImg shared library on this
    ' platform. Carries the two probe error texts so the CLI can render
    ' the wiki-link install hint plus the underlying loader diagnostics.
    Public Class CapsLibraryNotFoundException
        Inherits FatalException

        Public ReadOnly Property X64Error As String
        Public ReadOnly Property GenericError As String

        Public Sub New(x64Error As String, genericError As String)
            MyBase.New("Could not find SPS/CAPS library")
            Me.X64Error = x64Error
            Me.GenericError = genericError
        End Sub
    End Class

    ' Raised by Seek when the drive's TRK0 signal disagrees with the
    ' commanded cylinder (the drive head didn't end up where the host
    ' asked it to). Carries enough state for the CLI to render the
    ' multi-line follow-up hints (recalibrate / slow-down) without
    ' embedding the CLI command names in the library message.
    Public Class Track0SeekMismatchException
        Inherits FatalException

        Public ReadOnly Property Trk0Asserted As Boolean
        Public ReadOnly Property Cyl As Integer

        Public Sub New(trk0Asserted As Boolean, cyl As Integer)
            MyBase.New(String.Format(Globalization.CultureInfo.InvariantCulture,
                                     "Track0 signal {0} after seek to cylinder {1}",
                                     If(trk0Asserted, "asserted", "absent"), cyl))
            Me.Trk0Asserted = trk0Asserted
            Me.Cyl = cyl
        End Sub
    End Class

End Namespace
