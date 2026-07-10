using Gst;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GStreamer.Services;

public partial class GStask
{
    [DllImport("libgstreamer-1.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern void gst_event_copy_segment(IntPtr raw, IntPtr segment);

    Pad videoStartProbePad;
    ulong videoStartProbeId;
    Gst.PadProbeCallback videoStartProbeCallback;
    ulong videoStartProbeRequestedSeconds;

    void InstallVideoStartProbe(ulong requestedNs)
    {
        RemoveVideoStartProbe();

        videoStartProbeRequestedSeconds = requestedNs;

        using var mq = bin?.GetByName("mq");
        videoStartProbePad = mq?.GetStaticPad("src_0");

        if (videoStartProbePad == null)
            return;

        videoStartProbeCallback = OnVideoStartProbe;
        videoStartProbeId = videoStartProbePad.AddProbe(
            PadProbeType.EventDownstream | PadProbeType.Buffer,
            videoStartProbeCallback
        );
    }

    void RemoveVideoStartProbe()
    {
        if (videoStartProbePad != null && videoStartProbeId != 0)
        {
            try
            {
                videoStartProbePad.RemoveProbe(videoStartProbeId);
            }
            catch { }
        }

        ClearVideoStartProbeState();

        try
        {
            videoStartProbePad?.Dispose();
        }
        catch { }

        videoStartProbePad = null;
    }

    void ClearVideoStartProbeState()
    {
        videoStartProbeId = 0;
        videoStartProbeCallback = null;
        videoStartProbeRequestedSeconds = 0;
    }

    PadProbeReturn OnVideoStartProbe(Pad pad, PadProbeInfo info)
    {
        if ((info.Type & PadProbeType.EventDownstream) != 0)
        {
            using var ev = info.GetEvent();

            if (TryGetSegmentClockTime(ev, out ulong segmentTime) &&
                IsAcceptableVideoStartClockTime(segmentTime))
            {
                ApplyVideoStartClockTime(segmentTime);
            }

            return PadProbeReturn.Ok;
        }

        if ((info.Type & PadProbeType.Buffer) == 0)
            return PadProbeReturn.Ok;

        using var buffer = info.GetBuffer();

        if (!TryGetBufferClockTime(buffer, out ulong presentationTime))
            return PadProbeReturn.Ok;

        if (!IsAcceptableVideoStartClockTime(presentationTime))
            return PadProbeReturn.Ok;

        ApplyVideoStartClockTime(presentationTime);

        ClearVideoStartProbeState();
        return PadProbeReturn.Remove;
    }

    bool TryGetSegmentClockTime(Event ev, out ulong clockTime)
    {
        clockTime = 0;

        if (ev == null || ev.Type != EventType.Segment)
            return false;

        if (!TryCopySegment(ev, out Gst.Segment segment))
            return false;

        if (segment.Format != Format.Time)
            return false;

        clockTime = segment.Time != ulong.MaxValue
            ? segment.Time
            : segment.Start;

        return clockTime != ulong.MaxValue;
    }

    static bool TryGetBufferClockTime(Gst.Buffer buffer, out ulong clockTime)
    {
        clockTime = 0;

        if (buffer == null)
            return false;

        ulong pts = buffer.Handle.GetPts();
        if (pts != ulong.MaxValue)
        {
            clockTime = pts;
            return true;
        }

        ulong dts = buffer.Handle.GetDts();
        if (dts != ulong.MaxValue)
        {
            clockTime = dts;
            return true;
        }

        return false;
    }

    bool IsAcceptableVideoStartClockTime(ulong clockTime)
    {
        ulong requested = videoStartProbeRequestedSeconds;
        ulong maxBackDiff = SecondsToClockTime(Math.Max(1, conf.segment_seconds));

        return requested <= maxBackDiff ||
               clockTime >= requested - maxBackDiff;
    }

    void ApplyVideoStartClockTime(ulong clockTime)
    {
        Volatile.Write(ref positionSeekSeconds, clockTime);
        Volatile.Write(ref positionSeconds, clockTime);
        mp4Reader.SetTimelineOffsetNs(clockTime);
    }

    static bool TryCopySegment(Event ev, out Gst.Segment segment)
    {
        segment = default;

        if (ev == null)
            return false;

        IntPtr nativeSegment = IntPtr.Zero;

        try
        {
            nativeSegment = Marshal.AllocHGlobal(Marshal.SizeOf<Gst.Segment>());

            gst_event_copy_segment(
                ev.Handle.DangerousGetHandle(),
                nativeSegment
            );

            segment = Marshal.PtrToStructure<Gst.Segment>(nativeSegment);
            return true;
        }
        catch
        {
            segment = default;
            return false;
        }
        finally
        {
            if (nativeSegment != IntPtr.Zero)
                Marshal.FreeHGlobal(nativeSegment);
        }
    }
}