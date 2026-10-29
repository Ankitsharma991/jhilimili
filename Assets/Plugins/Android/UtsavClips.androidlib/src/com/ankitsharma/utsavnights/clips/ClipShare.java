package com.ankitsharma.utsavnights.clips;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.util.Log;

import androidx.core.content.FileProvider;

import java.io.File;

/**
 * Phase 9 step 6 (F14): shares a clip from the app's cache through the FileProvider. With a package (WhatsApp,
 * Instagram) the clip goes straight to that app; otherwise, or when it isn't installed, the system share sheet opens.
 */
public final class ClipShare {
    private static final String TAG = "UtsavClips";

    private ClipShare() {
    }

    /** The folder clips are written to (the FileProvider's "clips" path). */
    public static String clipsDir(Activity activity) {
        File dir = new File(activity.getCacheDir(), "clips");
        if (!dir.exists() && !dir.mkdirs()) {
            Log.w(TAG, "could not create " + dir);
        }

        return dir.getAbsolutePath();
    }

    public static boolean isInstalled(Activity activity, String packageName) {
        try {
            activity.getPackageManager().getPackageInfo(packageName, 0);
            return true;
        } catch (PackageManager.NameNotFoundException e) {
            return false;
        }
    }

    /** Returns "ok", "fallback" (the app wasn't there, the share sheet opened instead) or an error. */
    public static String share(Activity activity, String path, String packageName, String text) {
        try {
            File file = new File(path);
            if (!file.exists()) {
                return "the clip is missing";
            }

            Uri uri = FileProvider.getUriForFile(activity, activity.getPackageName() + ".clips", file);
            Intent send = new Intent(Intent.ACTION_SEND);
            send.setType("video/mp4");
            send.putExtra(Intent.EXTRA_STREAM, uri);
            if (text != null && !text.isEmpty()) {
                send.putExtra(Intent.EXTRA_TEXT, text);
            }

            send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            boolean direct = packageName != null && !packageName.isEmpty() && isInstalled(activity, packageName);
            if (direct) {
                send.setPackage(packageName);
                try {
                    activity.startActivity(send);
                    return "ok";
                } catch (ActivityNotFoundException e) {
                    send.setPackage(null);
                }
            }

            Intent chooser = Intent.createChooser(send, "Share your clip");
            chooser.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.startActivity(chooser);
            return packageName != null && !packageName.isEmpty() && !direct ? "fallback" : "ok";
        } catch (Exception e) {
            Log.w(TAG, "share failed: " + e);
            return "share failed: " + e.getMessage();
        }
    }
}
