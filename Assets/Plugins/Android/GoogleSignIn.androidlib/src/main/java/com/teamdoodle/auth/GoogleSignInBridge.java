package com.teamdoodle.auth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.os.Handler;
import android.os.Looper;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;

/**
 * Credential Manager의 "Google 계정으로 로그인" 창을 띄워 Google ID 토큰을 받는다.
 * Unity(C#)의 AndroidGoogleSignIn이 signIn을 부르고, 결과는 Callback으로 돌려준다.
 * 콜백은 Android 메인 스레드에서 불리므로 C# 쪽에서 Unity 메인 스레드로 옮겨 쓴다.
 */
public final class GoogleSignInBridge {
    /** 로그인 결과. 셋 중 하나만 한 번 불린다 */
    public interface Callback {
        void onSuccess(String idToken);

        /** 사용자가 창을 닫았다 */
        void onCancel();

        /** code: NO_CREDENTIAL(기기에 Google 계정 없음), UNEXPECTED_CREDENTIAL, 그 밖은 Credential Manager 예외 타입 */
        void onError(String code, String message);
    }

    private GoogleSignInBridge() {
    }

    /**
     * @param serverClientId Google Cloud의 "웹 애플리케이션" OAuth 클라이언트 ID. ID 토큰의 aud가 이 값이 되고, 서버가 이 값으로 검증한다
     */
    public static void signIn(Activity activity, String serverClientId, Callback callback) {
        GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(serverClientId).build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();
        Handler main = new Handler(Looper.getMainLooper());
        Executor executor = main::post;

        CredentialManager.create(activity).getCredentialAsync(
                activity,
                request,
                new CancellationSignal(),
                executor,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        Credential credential = response.getCredential();
                        if (credential instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            try {
                                callback.onSuccess(GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken());
                            } catch (Exception e) {
                                callback.onError("UNEXPECTED_CREDENTIAL", e.getMessage());
                            }
                            return;
                        }

                        callback.onError("UNEXPECTED_CREDENTIAL", credential.getType());
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (e instanceof GetCredentialCancellationException) {
                            callback.onCancel();
                        } else if (e instanceof NoCredentialException) {
                            callback.onError("NO_CREDENTIAL", e.getMessage());
                        } else {
                            callback.onError(e.getType(), e.getMessage());
                        }
                    }
                });
    }
}
