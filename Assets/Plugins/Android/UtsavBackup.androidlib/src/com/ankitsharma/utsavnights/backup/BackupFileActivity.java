package com.ankitsharma.utsavnights.backup;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Saves or opens one backup file through the system file picker (Phase 10 step 1). Invisible: it opens the picker,
 * copies the bytes and reports back to Unity with UnitySendMessage(receiver, method, result), where the result is
 * "ok:<path>", "cancelled" or "error:<message>". A save copies a file the game wrote to its cache; an open copies the
 * picked file into the cache for the game to read.
 */
public final class BackupFileActivity extends Activity {
    private static final int REQUEST = 4711;
    private static final long MAX_BYTES = 8L * 1024 * 1024;

    private String mode;
    private String path;
    private String receiver;
    private String method;

    /** Opens "save as" for a file the game wrote; the player picks where (Downloads, Drive and so on). */
    public static void save(Activity activity, String path, String suggestedName, String receiver, String method) {
        Intent i = new Intent(activity, BackupFileActivity.class);
        i.putExtra("mode", "save").putExtra("path", path).putExtra("name", suggestedName)
                .putExtra("receiver", receiver).putExtra("method", method);
        activity.startActivity(i);
    }

    /** Opens the picker for a backup file; it is copied to path. */
    public static void open(Activity activity, String path, String receiver, String method) {
        Intent i = new Intent(activity, BackupFileActivity.class);
        i.putExtra("mode", "open").putExtra("path", path).putExtra("receiver", receiver).putExtra("method", method);
        activity.startActivity(i);
    }

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        Intent in = getIntent();
        mode = in.getStringExtra("mode");
        path = in.getStringExtra("path");
        receiver = in.getStringExtra("receiver");
        method = in.getStringExtra("method");
        if (state != null) {
            return; // recreated while the picker is open; its result still arrives
        }

        Intent pick;
        if ("save".equals(mode)) {
            pick = new Intent(Intent.ACTION_CREATE_DOCUMENT);
            pick.setType("application/octet-stream");
            pick.putExtra(Intent.EXTRA_TITLE, in.getStringExtra("name"));
        } else {
            pick = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            pick.setType("*/*");
        }

        pick.addCategory(Intent.CATEGORY_OPENABLE);
        try {
            startActivityForResult(pick, REQUEST);
        } catch (Exception e) {
            finishWith("error:no file picker on this phone");
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST) {
            return;
        }

        Uri uri = data != null ? data.getData() : null;
        if (resultCode != RESULT_OK || uri == null) {
            finishWith("cancelled");
            return;
        }

        try {
            if ("save".equals(mode)) {
                try (InputStream from = new FileInputStream(new File(path));
                     OutputStream to = getContentResolver().openOutputStream(uri, "wt")) {
                    copy(from, to);
                }
            } else {
                File target = new File(path);
                File parent = target.getParentFile();
                if (parent != null) {
                    parent.mkdirs();
                }

                try (InputStream from = getContentResolver().openInputStream(uri);
                     OutputStream to = new FileOutputStream(target)) {
                    copy(from, to);
                }
            }

            finishWith("ok:" + path);
        } catch (Exception e) {
            finishWith("error:" + e.getMessage());
        }
    }

    private static void copy(InputStream from, OutputStream to) throws Exception {
        if (from == null || to == null) {
            throw new Exception("the file couldn't be opened");
        }

        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        int n;
        while ((n = from.read(buffer)) > 0) {
            total += n;
            if (total > MAX_BYTES) {
                throw new Exception("the file is too large to be a backup");
            }

            to.write(buffer, 0, n);
        }
    }

    private void finishWith(String result) {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            player.getMethod("UnitySendMessage", String.class, String.class, String.class).invoke(null, receiver, method, result);
        } catch (Exception ignored) {
            // Unity isn't there (never in the game); nothing to tell.
        }

        finish();
        overridePendingTransition(0, 0);
    }
}
