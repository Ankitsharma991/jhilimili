package com.ankitsharma.utsavnights.signin;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.core.content.ContextCompat;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;

/**
 * Google sign-in for Firebase Auth (Phase 10 step 6). Shows Android's own account picker (Credential Manager) and
 * answers with a Google ID token only: UnitySendMessage(receiver, method, "ok:<token>" | "cancelled" | "error:<why>").
 * The game never reads the account's name, email or picture. The OAuth client is the project's web client
 * (default_web_client_id, written by the Firebase Unity SDK from google-services.json).
 */
public final class GoogleSignIn {
    private GoogleSignIn() {
    }

    public static void signIn(final Activity activity, final String receiver, final String method) {
        int id = activity.getResources().getIdentifier("default_web_client_id", "string", activity.getPackageName());
        if (id == 0) {
            send(receiver, method, "error:no web client id (google-services.json missing)");
            return;
        }

        GetGoogleIdOption option = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(false)
                .setServerClientId(activity.getString(id))
                .setAutoSelectEnabled(true)
                .build();
        GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
        Executor main = ContextCompat.getMainExecutor(activity);
        CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(), main,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        Credential c = response.getCredential();
                        if (c instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(c.getType())) {
                            try {
                                GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(c.getData());
                                send(receiver, method, "ok:" + google.getIdToken());
                            } catch (Exception e) {
                                send(receiver, method, "error:" + e.getMessage());
                            }
                        } else {
                            send(receiver, method, "error:not a Google account");
                        }
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (e instanceof GetCredentialCancellationException) {
                            send(receiver, method, "cancelled");
                        } else if (e instanceof NoCredentialException) {
                            send(receiver, method, "error:no Google account on this phone");
                        } else {
                            send(receiver, method, "error:" + e.getMessage());
                        }
                    }
                });
    }

    /** Forgets the chosen account, so the next sign-in shows the picker again. */
    public static void signOut(Activity activity) {
        CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), new CancellationSignal(),
                ContextCompat.getMainExecutor(activity), new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override
                    public void onResult(Void result) {
                    }

                    @Override
                    public void onError(ClearCredentialException e) {
                    }
                });
    }

    private static void send(String receiver, String method, String result) {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            player.getMethod("UnitySendMessage", String.class, String.class, String.class).invoke(null, receiver, method, result);
        } catch (Exception ignored) {
            // Unity isn't there (never in the game).
        }
    }
}
