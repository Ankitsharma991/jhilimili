package com.ankitsharma.utsavnights.clips;

import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.util.Log;

import java.nio.ByteBuffer;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Phase 9 step 6 (F14): encodes the game's frames (I420, from the GPU) and sound (16-bit PCM) into an MP4 with
 * MediaCodec (H.264 and AAC) and MediaMuxer. The game hands over each frame and audio block from its main thread;
 * they are copied at once and encoded on a worker thread, so the game never waits on the encoder. A frame that
 * arrives while the encoder is behind is dropped rather than queued without limit.
 */
public final class ClipEncoder {
    private static final String TAG = "UtsavClips";
    private static final long TIMEOUT_US = 10000;
    private static final int MAX_QUEUED_FRAMES = 4;

    private static final class Packet {
        final boolean video;
        final byte[] data;
        final int length;
        final long ptsUs;
        final boolean end;

        Packet(boolean video, byte[] data, int length, long ptsUs, boolean end) {
            this.video = video;
            this.data = data;
            this.length = length;
            this.ptsUs = ptsUs;
            this.end = end;
        }
    }

    private static final class Pending {
        final boolean video;
        final ByteBuffer data;
        final MediaCodec.BufferInfo info;

        Pending(boolean video, ByteBuffer data, MediaCodec.BufferInfo info) {
            this.video = video;
            this.data = data;
            this.info = info;
        }
    }

    private final String path;
    private final int width;
    private final int height;
    private final int sampleRate;
    private final int channels;
    private final LinkedBlockingQueue<Packet> queue = new LinkedBlockingQueue<>();
    private final ArrayDeque<byte[]> framePool = new ArrayDeque<>();
    private final List<Pending> pending = new ArrayList<>();
    private final Object poolLock = new Object();
    private MediaCodec video;
    private MediaCodec audio;
    private MediaMuxer muxer;
    private int videoTrack = -1;
    private int audioTrack = -1;
    private boolean muxing;
    private boolean videoDone;
    private boolean audioDone;
    private Thread worker;
    private final AtomicInteger queuedFrames = new AtomicInteger();
    private volatile boolean finished;
    private volatile String error = "";
    private volatile int framesWritten;
    private final AtomicInteger framesDropped = new AtomicInteger();
    private long lastVideoPts = -1;
    private long lastAudioPts = -1;

    public ClipEncoder(String path, int width, int height, int fps, int videoBitrate, int sampleRate, int channels) {
        this.path = path;
        this.width = width;
        this.height = height;
        this.sampleRate = sampleRate;
        this.channels = channels;
        try {
            MediaFormat vf = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height);
            vf.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
            vf.setInteger(MediaFormat.KEY_BIT_RATE, videoBitrate);
            vf.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            vf.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            video = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            video.configure(vf, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            video.start();

            MediaFormat af = MediaFormat.createAudioFormat(MediaFormat.MIMETYPE_AUDIO_AAC, sampleRate, channels);
            af.setInteger(MediaFormat.KEY_AAC_PROFILE, MediaCodecInfo.CodecProfileLevel.AACObjectLC);
            af.setInteger(MediaFormat.KEY_BIT_RATE, 128000);
            af.setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, 16384 * channels);
            audio = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_AUDIO_AAC);
            audio.configure(af, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            audio.start();

            muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
        } catch (Exception e) {
            fail("could not start the encoder: " + e);
            releaseAll();
            finished = true;
            return;
        }

        worker = new Thread(this::run, "UtsavClipEncoder");
        worker.start();
    }

    /** An empty string while all is well, else what went wrong. */
    public String error() {
        return error;
    }

    /** True once finish() has written the file (or the encoder failed). */
    public boolean isFinished() {
        return finished;
    }

    public int framesWritten() {
        return framesWritten;
    }

    public int framesDropped() {
        return framesDropped.get();
    }

    /** One I420 frame (Y, then U, then V; width x height) from a direct buffer. False when it was dropped. */
    public boolean addVideo(ByteBuffer frame, long ptsUs) {
        if (finished || !error.isEmpty() || queuedFrames.get() >= MAX_QUEUED_FRAMES) {
            framesDropped.incrementAndGet();
            return false;
        }

        int length = width * height * 3 / 2;
        if (frame.capacity() < length) {
            framesDropped.incrementAndGet();
            return false;
        }

        byte[] copy;
        synchronized (poolLock) {
            copy = framePool.poll();
        }

        if (copy == null || copy.length < length) {
            copy = new byte[length];
        }

        frame.position(0);
        frame.get(copy, 0, length);
        queuedFrames.incrementAndGet();
        queue.add(new Packet(true, copy, length, ptsUs, false));
        return true;
    }

    /** A block of interleaved 16-bit little-endian PCM. */
    public void addAudio(byte[] pcm, int length, long ptsUs) {
        if (finished || !error.isEmpty() || length <= 0) {
            return;
        }

        byte[] copy = new byte[length];
        System.arraycopy(pcm, 0, copy, 0, length);
        queue.add(new Packet(false, copy, length, ptsUs, false));
    }

    /** Ends both streams; the file is complete when isFinished() turns true. */
    public void finish() {
        queue.add(new Packet(true, null, 0, 0, true));
    }

    /** Stops at once and deletes the partial file. */
    public void cancel() {
        fail("cancelled");
        queue.add(new Packet(true, null, 0, 0, true));
    }

    private void run() {
        try {
            while (true) {
                Packet p = queue.take();
                if (p.end) {
                    break;
                }

                if (!error.isEmpty()) {
                    continue;
                }

                if (p.video) {
                    queuedFrames.decrementAndGet();
                    feedVideo(p);
                    synchronized (poolLock) {
                        if (framePool.size() < MAX_QUEUED_FRAMES + 2) {
                            framePool.add(p.data);
                        }
                    }
                } else {
                    feedAudio(p);
                }

                drain(video, true, false);
                drain(audio, false, false);
            }

            if (error.isEmpty()) {
                endStream(video, true);
                endStream(audio, false);
                for (int i = 0; i < 200 && !(videoDone && audioDone); i++) {
                    if (!videoDone) {
                        drain(video, true, true);
                    }

                    if (!audioDone) {
                        drain(audio, false, true);
                    }
                }
            }
        } catch (Exception e) {
            fail("encoding failed: " + e);
        } finally {
            releaseAll();
            if (!error.isEmpty() || framesWritten == 0) {
                if (error.isEmpty()) {
                    fail("no frames were recorded");
                }

                new java.io.File(path).delete();
            }

            finished = true;
        }
    }

    private void feedVideo(Packet p) {
        int index = video.dequeueInputBuffer(TIMEOUT_US);
        if (index < 0) {
            framesDropped.incrementAndGet();
            return;
        }

        long pts = Math.max(p.ptsUs, lastVideoPts + 1);
        lastVideoPts = pts;
        Image image = video.getInputImage(index);
        if (image == null) {
            fail("the video encoder gave no input image");
            return;
        }

        Image.Plane[] planes = image.getPlanes();
        int chromaW = width / 2;
        int chromaH = height / 2;
        copyPlane(p.data, 0, width, height, planes[0]);
        copyPlane(p.data, width * height, chromaW, chromaH, planes[1]);
        copyPlane(p.data, width * height + chromaW * chromaH, chromaW, chromaH, planes[2]);
        video.queueInputBuffer(index, 0, p.length, pts, 0);
        framesWritten++;
    }

    /** Copies one tightly packed plane into the codec's plane, whatever its row and pixel strides. */
    private static void copyPlane(byte[] src, int offset, int w, int h, Image.Plane plane) {
        ByteBuffer dst = plane.getBuffer();
        int rowStride = plane.getRowStride();
        int pixelStride = plane.getPixelStride();
        if (pixelStride == 1 && rowStride == w) {
            dst.position(0);
            dst.put(src, offset, w * h);
            return;
        }

        for (int y = 0; y < h; y++) {
            int row = y * rowStride;
            int from = offset + y * w;
            if (pixelStride == 1) {
                dst.position(row);
                dst.put(src, from, w);
            } else {
                for (int x = 0; x < w; x++) {
                    int at = row + x * pixelStride;
                    if (at < dst.limit()) {
                        dst.put(at, src[from + x]);
                    }
                }
            }
        }
    }

    private void feedAudio(Packet p) {
        int done = 0;
        long pts = Math.max(p.ptsUs, lastAudioPts + 1);
        int bytesPerSecond = sampleRate * channels * 2;
        while (done < p.length) {
            int index = audio.dequeueInputBuffer(TIMEOUT_US);
            if (index < 0) {
                drain(audio, false, false);
                continue;
            }

            ByteBuffer in = audio.getInputBuffer(index);
            in.clear();
            int n = Math.min(in.remaining(), p.length - done);
            n -= n % (2 * channels);
            in.put(p.data, done, n);
            long at = pts + (long) done * 1000000L / bytesPerSecond;
            audio.queueInputBuffer(index, 0, n, at, 0);
            lastAudioPts = at;
            done += n;
        }
    }

    private void endStream(MediaCodec codec, boolean isVideo) {
        int index = codec.dequeueInputBuffer(TIMEOUT_US * 10);
        if (index >= 0) {
            long pts = isVideo ? lastVideoPts + 1 : lastAudioPts + 1;
            codec.queueInputBuffer(index, 0, 0, Math.max(0, pts), MediaCodec.BUFFER_FLAG_END_OF_STREAM);
        } else if (isVideo) {
            videoDone = true;
        } else {
            audioDone = true;
        }
    }

    private void drain(MediaCodec codec, boolean isVideo, boolean ending) {
        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        while (true) {
            int index = codec.dequeueOutputBuffer(info, ending ? TIMEOUT_US : 0);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) {
                return;
            }

            if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (isVideo) {
                    videoTrack = muxer.addTrack(codec.getOutputFormat());
                } else {
                    audioTrack = muxer.addTrack(codec.getOutputFormat());
                }

                if (videoTrack >= 0 && audioTrack >= 0 && !muxing) {
                    muxer.start();
                    muxing = true;
                    for (Pending p : pending) {
                        muxer.writeSampleData(p.video ? videoTrack : audioTrack, p.data, p.info);
                    }

                    pending.clear();
                }

                continue;
            }

            if (index < 0) {
                continue;
            }

            ByteBuffer out = codec.getOutputBuffer(index);
            boolean config = (info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0;
            if (out != null && info.size > 0 && !config) {
                out.position(info.offset);
                out.limit(info.offset + info.size);
                if (muxing) {
                    muxer.writeSampleData(isVideo ? videoTrack : audioTrack, out, info);
                } else {
                    // Until both tracks are known, keep a copy (only the first few packets).
                    ByteBuffer copy = ByteBuffer.allocate(info.size);
                    copy.put(out);
                    copy.flip();
                    MediaCodec.BufferInfo kept = new MediaCodec.BufferInfo();
                    kept.set(0, info.size, info.presentationTimeUs, info.flags);
                    pending.add(new Pending(isVideo, copy, kept));
                }
            }

            codec.releaseOutputBuffer(index, false);
            if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                if (isVideo) {
                    videoDone = true;
                } else {
                    audioDone = true;
                }

                return;
            }
        }
    }

    private void fail(String message) {
        if (error.isEmpty()) {
            error = message;
            Log.w(TAG, message);
        }
    }

    private void releaseAll() {
        try {
            if (video != null) {
                video.stop();
                video.release();
            }
        } catch (Exception ignored) {
            // Already stopped.
        }

        try {
            if (audio != null) {
                audio.stop();
                audio.release();
            }
        } catch (Exception ignored) {
            // Already stopped.
        }

        try {
            if (muxer != null) {
                if (muxing) {
                    muxer.stop();
                }

                muxer.release();
            }
        } catch (Exception e) {
            fail("could not finish the file: " + e);
        }

        video = null;
        audio = null;
        muxer = null;
    }
}
