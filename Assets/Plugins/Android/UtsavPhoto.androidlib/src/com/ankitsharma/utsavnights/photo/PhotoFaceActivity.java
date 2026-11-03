package com.ankitsharma.utsavnights.photo;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.graphics.PointF;
import android.media.ExifInterface;
import android.media.FaceDetector;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.MediaStore;

import androidx.core.content.FileProvider;

import java.io.File;
import java.io.InputStream;

/**
 * The photo-assisted avatar's photo step (Phase 11 step 3, F26). Invisible: it asks the camera app or the system photo
 * picker for one photo, decodes a small copy (at most 480 pixels wide), finds the face with Android's own face detector,
 * deletes the camera's file at once, and keeps only the small copy's RGB bytes in memory until the game takes them
 * (takePixels clears them). Answers with UnitySendMessage: "ok:width,height,midX,midY,eyeDistance", "noface",
 * "cancelled" or "error:why".
 */
public final class PhotoFaceActivity extends Activity {
    private static final int REQUEST = 4712;
    private static final int MAX_SIDE = 480;

    private static byte[] pixels;

    private boolean camera;
    private String receiver;
    private String method;
    private File shot;

    public static void start(Activity activity, boolean camera, String receiver, String method) {
        Intent i = new Intent(activity, PhotoFaceActivity.class);
        i.putExtra("camera", camera).putExtra("receiver", receiver).putExtra("method", method);
        activity.startActivity(i);
    }

    /** The last photo's small RGB copy, handed over once: the reference here is dropped. */
    public static synchronized byte[] takePixels() {
        byte[] p = pixels;
        pixels = null;
        return p;
    }

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        Intent in = getIntent();
        camera = in.getBooleanExtra("camera", false);
        receiver = in.getStringExtra("receiver");
        method = in.getStringExtra("method");
        shot = new File(new File(getCacheDir(), "photo"), "face.jpg");
        if (state != null) {
            return; // recreated while the camera or picker is open; its result still arrives
        }

        try {
            Intent pick;
            if (camera) {
                File dir = shot.getParentFile();
                if (dir != null) {
                    dir.mkdirs();
                }

                Uri out = FileProvider.getUriForFile(this, getPackageName() + ".photo", shot);
                pick = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
                pick.putExtra(MediaStore.EXTRA_OUTPUT, out);
                pick.setClipData(ClipData.newRawUri("", out));
                pick.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } else if (Build.VERSION.SDK_INT >= 33) {
                pick = new Intent(MediaStore.ACTION_PICK_IMAGES);
            } else {
                pick = new Intent(Intent.ACTION_GET_CONTENT);
                pick.setType("image/*");
                pick.addCategory(Intent.CATEGORY_OPENABLE);
            }

            startActivityForResult(pick, REQUEST);
        } catch (Exception e) {
            finishWith(camera ? "error:no camera app on this phone" : "error:no photo picker on this phone");
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST) {
            return;
        }

        try {
            if (resultCode != RESULT_OK) {
                finishWith("cancelled");
                return;
            }

            Uri uri = camera ? Uri.fromFile(shot) : (data != null ? data.getData() : null);
            if (uri == null) {
                finishWith("cancelled");
                return;
            }

            finishWith(analyse(uri));
        } catch (Throwable e) {
            finishWith("error:" + e.getMessage());
        } finally {
            if (shot.exists() && !shot.delete()) {
                shot.deleteOnExit();
            }
        }
    }

    private String analyse(Uri uri) throws Exception {
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        try (InputStream s = open(uri)) {
            BitmapFactory.decodeStream(s, null, bounds);
        }

        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) {
            return "error:that isn't a photo";
        }

        BitmapFactory.Options opts = new BitmapFactory.Options();
        opts.inSampleSize = 1;
        while (Math.max(bounds.outWidth, bounds.outHeight) / (opts.inSampleSize * 2) >= MAX_SIDE) {
            opts.inSampleSize *= 2;
        }

        Bitmap decoded;
        try (InputStream s = open(uri)) {
            decoded = BitmapFactory.decodeStream(s, null, opts);
        }

        if (decoded == null) {
            return "error:the photo couldn't be read";
        }

        int rotate = 0;
        try (InputStream s = open(uri)) {
            int o = new ExifInterface(s).getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
            rotate = o == ExifInterface.ORIENTATION_ROTATE_90 ? 90 : o == ExifInterface.ORIENTATION_ROTATE_180 ? 180 : o == ExifInterface.ORIENTATION_ROTATE_270 ? 270 : 0;
        } catch (Exception ignored) {
            // No orientation: use the photo as it is.
        }

        // Scale so the longer side is at most MAX_SIDE, with an even width (the face detector needs one), and rotate upright.
        float scale = Math.min(1f, MAX_SIDE / (float) Math.max(decoded.getWidth(), decoded.getHeight()));
        Matrix m = new Matrix();
        m.postScale(scale, scale);
        m.postRotate(rotate);
        Bitmap upright = Bitmap.createBitmap(decoded, 0, 0, decoded.getWidth(), decoded.getHeight(), m, true);
        if (upright != decoded) {
            decoded.recycle();
        }

        int w = upright.getWidth() & ~1;
        int h = upright.getHeight();
        Bitmap rgb565 = Bitmap.createBitmap(upright, 0, 0, w, h).copy(Bitmap.Config.RGB_565, false);
        FaceDetector.Face[] faces = new FaceDetector.Face[3];
        int found = new FaceDetector(w, h, faces.length).findFaces(rgb565, faces);
        rgb565.recycle();

        FaceDetector.Face best = null;
        for (int i = 0; i < found; i++) {
            if (faces[i] != null && faces[i].confidence() >= 0.3f && (best == null || faces[i].eyesDistance() > best.eyesDistance())) {
                best = faces[i];
            }
        }

        if (best == null) {
            upright.recycle();
            return "noface";
        }

        int[] argb = new int[w * h];
        upright.getPixels(argb, 0, upright.getWidth(), 0, 0, w, h);
        upright.recycle();
        byte[] out = new byte[w * h * 3];
        for (int i = 0; i < argb.length; i++) {
            int c = argb[i];
            out[i * 3] = (byte) ((c >> 16) & 0xff);
            out[i * 3 + 1] = (byte) ((c >> 8) & 0xff);
            out[i * 3 + 2] = (byte) (c & 0xff);
            argb[i] = 0;
        }

        synchronized (PhotoFaceActivity.class) {
            pixels = out;
        }

        PointF mid = new PointF();
        best.getMidPoint(mid);
        return "ok:" + w + "," + h + "," + mid.x + "," + mid.y + "," + best.eyesDistance();
    }

    private InputStream open(Uri uri) throws Exception {
        InputStream s = getContentResolver().openInputStream(uri);
        if (s == null) {
            throw new Exception("the photo couldn't be opened");
        }

        return s;
    }

    private void finishWith(String result) {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            player.getMethod("UnitySendMessage", String.class, String.class, String.class).invoke(null, receiver, method, result);
        } catch (Exception ignored) {
            // Unity isn't there (never in the game).
        }

        finish();
        overridePendingTransition(0, 0);
    }
}
