Namespace Greaseweazle.Actions

    ' Strongly-typed return value of BandwidthCommand.Run.
    '
    ' The algorithm always produces a Write row; if the device echoed the
    ' written bytes back correctly it then produces a Read row; if both
    ' rows succeeded it produces a Summary block. Garble flags surface
    ' whether either direction reported corrupted data — Python's
    ' bandwidth.py prints an "ERROR: USB ... data garbled ..." line and
    ' aborts at the first garbled direction, so a Read row is only
    ' present when WriteGarbled is False.
    '
    ' Returns Nothing when --test is set (algorithm never opens a port).
    Public NotInheritable Class BandwidthResult

        Public Sub New(writeRow As BandwidthRow,
                       writeGarbled As Boolean,
                       readRow As BandwidthRow,
                       readGarbled As Boolean,
                       summary As BandwidthSummary)
            Me.WriteRow = writeRow
            Me.WriteGarbled = writeGarbled
            Me.ReadRow = readRow
            Me.ReadGarbled = readGarbled
            Me.Summary = summary
        End Sub

        ' Always populated.
        Public ReadOnly Property WriteRow As BandwidthRow

        ' True when the device's CRC of the host->device payload didn't
        ' match — Python prints `ERROR: USB write data garbled ...` and
        ' aborts before measuring the read direction.
        Public ReadOnly Property WriteGarbled As Boolean

        ' Populated only when WriteGarbled = False.
        Public ReadOnly Property ReadRow As BandwidthRow

        ' True when the device->host payload didn't byte-match what we
        ' wrote. Python prints `ERROR: USB read data garbled ...` and
        ' suppresses the summary block.
        Public ReadOnly Property ReadGarbled As Boolean

        ' Populated only when both directions succeeded.
        Public ReadOnly Property Summary As BandwidthSummary

    End Class

    ' Min/Mean/Max bandwidth row in Mbps. Mean is computed by the host
    ' (bytes * 8 / elapsed_seconds / 1_000_000); Min/Max come from the
    ' device's own per-bulk-transfer measurements via Unit.BwStats().
    Public NotInheritable Class BandwidthRow

        Public Sub New(min As Double, mean As Double, max As Double)
            Me.Min = min
            Me.Mean = mean
            Me.Max = max
        End Sub

        Public ReadOnly Property Min As Double
        Public ReadOnly Property Mean As Double
        Public ReadOnly Property Max As Double

    End Class

    ' Final block printed under both bandwidth rows. The estimate is
    ' compared against the firmware-required minimum: when the estimate
    ' falls short, BelowRequirement is True and RequiredMinMbps carries
    ' the minimum the firmware needs; otherwise the firmware-derived
    ' max-flux-rate / min-average-flux numbers are populated.
    Public NotInheritable Class BandwidthSummary

        Public Sub New(estimatedMinMbps As Double,
                       belowRequirement As Boolean,
                       requiredMinMbps As Double,
                       maxFluxRateMsps As Double,
                       minAvgFluxUs As Double)
            Me.EstimatedMinMbps = estimatedMinMbps
            Me.BelowRequirement = belowRequirement
            Me.RequiredMinMbps = requiredMinMbps
            Me.MaxFluxRateMsps = maxFluxRateMsps
            Me.MinAvgFluxUs = minAvgFluxUs
        End Sub

        Public ReadOnly Property EstimatedMinMbps As Double
        Public ReadOnly Property BelowRequirement As Boolean

        ' Meaningful when BelowRequirement = True.
        Public ReadOnly Property RequiredMinMbps As Double

        ' Meaningful when BelowRequirement = False.
        Public ReadOnly Property MaxFluxRateMsps As Double
        Public ReadOnly Property MinAvgFluxUs As Double

    End Class

End Namespace
